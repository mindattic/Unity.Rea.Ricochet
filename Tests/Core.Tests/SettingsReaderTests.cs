using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Ricochet.Core.Data;

namespace Ricochet.Core.Tests
{
    // U.RICO-T40: the validating reader of the data files. Each accessor gives a fallback and records a message, never throws.
    public class SettingsReaderTests
    {
        private enum Colour { Red, Blue }

        private List<string> _errors;

        [SetUp]
        public void SetUp() => _errors = new List<string>();

        private SettingsReader Read(string json, string path = "t.json")
        {
            var value = json == null ? null : Json.Parse(json);
            // The constructor is internal to Core (GameData is its only caller), so the tests reach it by reflection.
            return (SettingsReader)Activator.CreateInstance(typeof(SettingsReader),
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null, new object[] { value, path, _errors }, null);
        }

        [Test]
        public void ReadsGoodValuesWithoutErrors()
        {
            var r = Read("{\"n\":2.5,\"i\":3,\"f\":true,\"s\":\"x\",\"ss\":[\"a\",\"b\"],\"c\":\"Blue\",\"_note\":1,\"nums\":[1,2,3,4],\"rows\":[[1,2],[3,4]],\"objs\":[{},{}],\"sec\":{\"k\":1}}");
            Assert.That(r.Number("n", 0, 10), Is.EqualTo(2.5));
            Assert.That(r.Integer("i", 1, 5), Is.EqualTo(3));
            Assert.That(r.Flag("f"), Is.True);
            Assert.That(r.Text("s"), Is.EqualTo("x"));
            Assert.That(r.Texts("ss", 2), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(r.Choice<Colour>("c"), Is.EqualTo(Colour.Blue));
            Assert.That(r.Numbers("nums", 2, multiple: true), Has.Length.EqualTo(4));
            Assert.That(r.Rows("rows", 2, minRows: 2), Has.Count.EqualTo(2));
            Assert.That(r.Objects("objs", 2), Has.Count.EqualTo(2));
            Assert.That(r.Section("sec").Integer("k"), Is.EqualTo(1));
            r.Done();
            Assert.That(_errors, Is.Empty);
            Assert.That(r.Exists, Is.True);
            Assert.That(r.Path, Is.EqualTo("t.json"));
            Assert.That(r.Line, Is.EqualTo(1));
        }

        [Test]
        public void MissingObjectReportsOnceAndEverythingUnderItIsQuiet()
        {
            var r = Read("{}");
            var sec = r.Section("sec");
            Assert.That(sec.Exists, Is.False);
            Assert.That(sec.Line, Is.EqualTo(0));
            Assert.That(sec.Number("n", 1), Is.EqualTo(1));
            Assert.That(sec.Integer("i", 4), Is.EqualTo(4));
            Assert.That(sec.Flag("f"), Is.False);
            Assert.That(sec.Text("s"), Is.Empty);
            Assert.That(sec.Texts("t"), Is.Empty);
            Assert.That(sec.Objects("o"), Is.Empty);
            Assert.That(sec.Rows("r", 2), Is.Empty);
            Assert.That(sec.Numbers("n", 3), Has.Length.EqualTo(3));
            Assert.That(sec.Choice<Colour>("c"), Is.EqualTo(Colour.Red));
            sec.Done();
            Assert.That(_errors, Has.Count.EqualTo(1));
            Assert.That(_errors[0], Does.Contain("'sec' is missing"));
        }

        [Test]
        public void MissingAndMistypedKeysAreReported()
        {
            var r = Read("{\"n\":\"x\"}");
            Assert.That(r.Number("n"), Is.EqualTo(0));
            Assert.That(r.Integer("gone", 3), Is.EqualTo(3));
            Assert.That(_errors[0], Does.Contain("'n' must be a number (is \"x\")"));
            Assert.That(_errors[1], Does.Contain("'gone' is missing"));
        }

        [Test]
        public void NumberRangeAndFallbacks()
        {
            var r = Read("{\"low\":-5,\"high\":50,\"ok\":5}");
            Assert.That(r.Number("low", 0, 10), Is.EqualTo(0));
            Assert.That(r.Number("high", 0, 10), Is.EqualTo(10));
            Assert.That(r.Number("ok", 0, 10), Is.EqualTo(5));
            Assert.That(_errors, Has.Count.EqualTo(2));
            Assert.That(_errors[0], Does.Contain("between 0 and 10 (is -5)"));
        }

        [Test]
        public void NonFiniteNumbersAreRejected()
        {
            var r = Read("{\"big\":1e999}");
            Assert.That(r.Number("big"), Is.EqualTo(0));
            Assert.That(_errors.Single(), Does.Contain("finite"));
        }

        [Test]
        public void IntegerMustBeWholeAndInRange()
        {
            var r = Read("{\"frac\":1.5,\"huge\":1e12,\"low\":0,\"high\":9}");
            Assert.That(r.Integer("frac", 2), Is.EqualTo(2));
            Assert.That(r.Integer("huge"), Is.EqualTo(0));
            Assert.That(r.Integer("low", 1, 5), Is.EqualTo(1));
            Assert.That(r.Integer("high", 1, 5), Is.EqualTo(5));
            Assert.That(_errors, Has.Count.EqualTo(4));
            Assert.That(_errors[0], Does.Contain("whole number"));
            Assert.That(_errors[2], Does.Contain("between 1 and 5"));
        }

        [Test]
        public void RangeWordingForOneSidedLimits()
        {
            var r = Read("{\"a\":1,\"b\":9}");
            r.Number("a", min: 2);
            r.Number("b", max: 5);
            Assert.That(_errors[0], Does.Contain("at least 2"));
            Assert.That(_errors[1], Does.Contain("at most 5"));
        }

        [Test]
        public void EmptyTextAndBadTextListsAreReported()
        {
            var r = Read("{\"s\":\"\",\"ss\":[\"a\",\"\",3],\"few\":[\"a\"]}");
            Assert.That(r.Text("s"), Is.Empty);
            Assert.That(r.Texts("ss"), Is.EqualTo(new[] { "a" }));
            r.Texts("few", 2);
            Assert.That(_errors, Has.Count.EqualTo(4));
            Assert.That(_errors[0], Does.Contain("must not be empty"));
            Assert.That(_errors[3], Does.Contain("at least 2"));
        }

        [Test]
        public void NumbersChecksSizeKindAndRange()
        {
            var r = Read("{\"two\":[1,2,3],\"mult\":[1,2,3],\"str\":[1,\"x\"],\"rng\":[1,99],\"zero\":[]}");
            Assert.That(r.Numbers("two", 2), Is.EqualTo(new double[2]));
            Assert.That(r.Numbers("mult", 2, multiple: true), Is.Empty);
            Assert.That(r.Numbers("str", 2), Is.EqualTo(new double[2]));
            Assert.That(r.Numbers("rng", 2, max: 10), Is.EqualTo(new[] { 1d, 99d }));
            Assert.That(r.Numbers("zero", 2, multiple: true), Is.Empty);
            Assert.That(_errors, Has.Count.EqualTo(5));
            Assert.That(_errors[0], Does.Contain("exactly 2"));
            Assert.That(_errors[1], Does.Contain("multiple of 2"));
            Assert.That(_errors[2], Does.Contain("only numbers"));
            Assert.That(_errors[3], Does.Contain("at most 10"));
        }

        [Test]
        public void RowsCheckShapeKindAndRange()
        {
            var r = Read("{\"rows\":[1,[1],[1,\"x\"],[1,99],[1,2]],\"mult\":[[1,2,3]],\"few\":[[1,2]]}");
            var rows = r.Rows("rows", 2, max: 10);
            Assert.That(rows.Select(x => x.Length), Is.EqualTo(new[] { 2, 2 }), "the good-shaped rows are kept, the range error still reports");
            Assert.That(r.Rows("mult", 2, multiple: true), Is.Empty);
            r.Rows("few", 2, minRows: 3);
            Assert.That(_errors.Any(e => e.Contains("row 0 must be an array")));
            Assert.That(_errors.Any(e => e.Contains("row 1 must hold exactly 2")));
            Assert.That(_errors.Any(e => e.Contains("row 2 must hold only numbers")));
            Assert.That(_errors.Any(e => e.Contains("row 3 must have every number at most 10")));
            Assert.That(_errors.Any(e => e.Contains("multiple of 2")));
            Assert.That(_errors.Any(e => e.Contains("at least 3 row(s)")));
        }

        [Test]
        public void ObjectsReportsNonObjectsAndShortLists()
        {
            var r = Read("{\"objs\":[{},1]}");
            Assert.That(r.Objects("objs", 3), Has.Count.EqualTo(1));
            Assert.That(_errors.Any(e => e.Contains("item 1 must be an object")));
            Assert.That(_errors.Any(e => e.Contains("at least 3 item(s)")));
        }

        [Test]
        public void ChoiceRejectsUnknownNames()
        {
            var r = Read("{\"c\":\"Green\",\"n\":1}");
            Assert.That(r.Choice<Colour>("c"), Is.EqualTo(Colour.Red));
            Assert.That(r.Choice<Colour>("n"), Is.EqualTo(Colour.Red));
            Assert.That(_errors[0], Does.Contain("one of Red, Blue"));
        }

        [Test]
        public void CheckAndErrorRecordMessages()
        {
            var r = Read("{\"a\":1}");
            r.Check(true, "a", "never shown");
            r.Check(false, "a", "must be bigger");
            r.Error("custom");
            Assert.That(_errors, Has.Count.EqualTo(2));
            Assert.That(_errors[0], Does.Contain("'a' must be bigger (line 1)"));
            Assert.That(_errors[1], Is.EqualTo("t.json: custom (line 1)"));
        }

        [Test]
        public void ErrorOnAMissingObjectHasNoLine()
        {
            var r = Read(null);
            r.Error("gone");
            r.Check(false, "k", "bad");
            Assert.That(_errors[0], Is.EqualTo("t.json: gone"));
            Assert.That(_errors[1], Does.Contain("'k' bad"));
        }

        [Test]
        public void DoneFlagsUnreadKeysButNotNotes()
        {
            var r = Read("{\"used\":1,\"typo\":2,\"_note\":3}");
            r.Integer("used");
            r.Done();
            Assert.That(_errors.Single(), Does.Contain("unknown key 'typo'"));
        }
    }
}
