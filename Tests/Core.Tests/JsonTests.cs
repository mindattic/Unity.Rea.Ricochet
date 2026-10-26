using System;
using NUnit.Framework;
using Ricochet.Core.Data;

namespace Ricochet.Core.Tests
{
    // U.RICO-T40: Core's own JSON parser, covering every value kind and every way text can fail to be JSON.
    public class JsonTests
    {
        [Test]
        public void ParsesEveryValueKind()
        {
            var doc = Json.Parse("{\"n\": -1.5e2, \"t\": true, \"f\": false, \"z\": null, \"s\": \"a\", \"a\": [1, 2], \"o\": {}}");
            Assert.That(doc.Kind, Is.EqualTo(JsonKind.Object));
            Assert.That(doc.Get("n").Number, Is.EqualTo(-150d));
            Assert.That(doc.Get("t").Bool, Is.True);
            Assert.That(doc.Get("f").Bool, Is.False);
            Assert.That(doc.Get("z").Kind, Is.EqualTo(JsonKind.Null));
            Assert.That(doc.Get("s").Text, Is.EqualTo("a"));
            Assert.That(doc.Get("a").Items, Has.Count.EqualTo(2));
            Assert.That(doc.Get("o").Members, Is.Empty);
            Assert.That(doc.Get("missing"), Is.Null);
            Assert.That(doc.Get("s").Get("x"), Is.Null, "a non-object has no members");
        }

        [Test]
        public void KeepsDocumentOrderAndLineNumbers()
        {
            var doc = Json.Parse("{\n\"b\": 1,\n\"a\": [\n]\n}");
            Assert.That(doc.Members[0].Key, Is.EqualTo("b"));
            Assert.That(doc.Members[1].Key, Is.EqualTo("a"));
            Assert.That(doc.Get("b").Line, Is.EqualTo(2));
            Assert.That(doc.Get("a").Line, Is.EqualTo(3));
            Assert.That(doc.Get("a").Items, Is.Empty);
        }

        [Test]
        public void ReadsEscapesAndSkipsAByteOrderMark()
        {
            var doc = Json.Parse("\ufeff\"q\\\" \\\\ \\/ \\b \\f \\n \\r \\t \\u0041\"");
            Assert.That(doc.Text, Is.EqualTo("q\" \\ / \b \f \n \r \t A"));
        }

        [Test]
        public void ReadsNumberForms()
        {
            Assert.That(Json.Parse("0").Number, Is.EqualTo(0d));
            Assert.That(Json.Parse("12.25").Number, Is.EqualTo(12.25));
            Assert.That(Json.Parse("1E+2").Number, Is.EqualTo(100d));
            Assert.That(Json.Parse("5e-1").Number, Is.EqualTo(0.5));
        }

        [Test]
        public void NullTextThrows()
        {
            Assert.Throws<ArgumentNullException>(() => Json.Parse(null));
        }

        [TestCase("", "unexpected end")]
        [TestCase("   ", "unexpected end")]
        [TestCase("1 2", "after the document")]
        [TestCase("?", "unexpected character")]
        [TestCase("tru", "expected 'true'")]
        [TestCase("{1: 2}", "key in double quotes")]
        [TestCase("{\"a\": 1, \"a\": 2}", "appears twice")]
        [TestCase("{\"a\" 1}", "expected ':'")]
        [TestCase("{\"a\": 1", "document ended")]
        [TestCase("{\"a\": 1 \"b\"", "expected '}'")]
        [TestCase("[1, 2", "document ended")]
        [TestCase("[1 2]", "expected ']'")]
        [TestCase("[1,]", "unexpected character")]
        [TestCase("\"open", "not closed")]
        [TestCase("\"a\nb\"", "past the end of its line")]
        [TestCase("\"a\\", "not closed")]
        [TestCase("\"\\x\"", "unknown escape")]
        [TestCase("\"\\u12\"", "four hex digits")]
        [TestCase("\"\\uZZZZ\"", "four hex digits")]
        [TestCase("-", "needs digits")]
        [TestCase("1.", "decimal point")]
        [TestCase("1e", "exponent")]
        [TestCase("1e+", "exponent")]
        public void RejectsText(string text, string reason)
        {
            var ex = Assert.Throws<JsonException>(() => Json.Parse(text));
            Assert.That(ex.Message, Does.Contain(reason));
        }

        [Test]
        public void ErrorsNameTheLineAndColumn()
        {
            var ex = Assert.Throws<JsonException>(() => Json.Parse("{\n  \"a\": ?\n}"));
            Assert.That(ex.Line, Is.EqualTo(2));
            Assert.That(ex.Column, Is.EqualTo(8));
            Assert.That(ex.Message, Does.StartWith("line 2, column 8"));
        }
    }
}
