using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ricochet.Core.Data
{
    /// <summary>
    /// Reads one JSON object of a data file and validates it as it goes. Every key must be present, of the right type
    /// and in range; a key nobody asked for is an error too (a typo would otherwise be silently ignored). Keys that
    /// start with an underscore are notes for people ("_source") and are skipped. Problems are collected, not thrown,
    /// so one load reports all of them, each as "file: key ... (line n)". A value that fails gives back a fallback so
    /// reading can go on.
    /// </summary>
    public sealed class SettingsReader
    {
        private readonly JsonValue _object;
        private readonly string _path;
        private readonly List<string> _errors;
        private readonly HashSet<string> _read = new HashSet<string>();

        internal SettingsReader(JsonValue value, string path, List<string> errors)
        {
            _object = value;
            _path = path;
            _errors = errors;
        }

        /// <summary>Where this object is, for messages ("maps.json.maps[2]").</summary>
        public string Path => _path;

        /// <summary>True when the object exists (a missing or mistyped one was already reported).</summary>
        public bool Exists => _object != null;

        /// <summary>The line this object starts on (0 when it is missing).</summary>
        public int Line => _object?.Line ?? 0;

        /// <summary>Records a problem against this object.</summary>
        public void Error(string message)
        {
            _errors.Add(_path + ": " + message + (_object != null ? " (line " + _object.Line + ")" : string.Empty));
        }

        public SettingsReader Section(string key)
        {
            var value = Take(key, JsonKind.Object, "an object");
            return new SettingsReader(value, _path + "." + key, _errors);
        }

        /// <summary>The objects of the array <paramref name="key"/>, each a reader (an empty list when it is missing).</summary>
        public List<SettingsReader> Objects(string key, int minCount = 0)
        {
            var list = new List<SettingsReader>();
            var value = Take(key, JsonKind.Array, "an array of objects");
            if (value == null)
            {
                return list;
            }

            for (int i = 0; i < value.Items.Count; i++)
            {
                var item = value.Items[i];
                if (item.Kind != JsonKind.Object)
                {
                    Fail(key, item, "item " + i + " must be an object (is " + Describe(item) + ")");
                    continue;
                }

                list.Add(new SettingsReader(item, _path + "." + key + "[" + i + "]", _errors));
            }

            if (value.Items.Count < minCount)
            {
                Fail(key, value, "must have at least " + minCount + " item(s) (has " + value.Items.Count + ")");
            }

            return list;
        }

        public double Number(string key, double min = double.MinValue, double max = double.MaxValue)
        {
            var value = Take(key, JsonKind.Number, "a number");
            if (value == null)
            {
                return Math.Max(0, min);
            }

            var number = value.Number;
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                Fail(key, value, "must be a finite number (is " + Show(number) + ")");
                return Math.Max(0, min);
            }

            if (number < min || number > max)
            {
                Fail(key, value, "must be " + Range(min, max) + " (is " + Show(number) + ")");
                return Math.Max(min, Math.Min(max, number));
            }

            return number;
        }

        public int Integer(string key, int min = int.MinValue, int max = int.MaxValue)
        {
            var value = Take(key, JsonKind.Number, "a whole number");
            if (value == null)
            {
                return min > 0 ? min : 0;
            }

            var number = value.Number;
            if (Math.Floor(number) != number || number < int.MinValue || number > int.MaxValue)
            {
                Fail(key, value, "must be a whole number (is " + Show(number) + ")");
                return min > 0 ? min : 0;
            }

            if (number < min || number > max)
            {
                Fail(key, value, "must be " + Range(min, max) + " (is " + Show(number) + ")");
                return Math.Max(min, Math.Min(max, (int)number));
            }

            return (int)number;
        }

        public bool Flag(string key)
        {
            var value = Take(key, JsonKind.Bool, "true or false");
            return value != null && value.Bool;
        }

        /// <summary>A non-empty string.</summary>
        public string Text(string key)
        {
            var value = Take(key, JsonKind.String, "a string");
            if (value == null)
            {
                return string.Empty;
            }

            if (value.Text.Length == 0)
            {
                Fail(key, value, "must not be empty");
            }

            return value.Text;
        }

        /// <summary>An array of non-empty strings, with at least <paramref name="minCount"/>.</summary>
        public List<string> Texts(string key, int minCount = 0)
        {
            var list = new List<string>();
            var value = Take(key, JsonKind.Array, "an array of strings");
            if (value == null)
            {
                return list;
            }

            foreach (var item in value.Items)
            {
                if (item.Kind != JsonKind.String || item.Text.Length == 0)
                {
                    Fail(key, item, "must hold only non-empty strings (has " + Describe(item) + ")");
                    continue;
                }

                list.Add(item.Text);
            }

            if (value.Items.Count < minCount)
            {
                Fail(key, value, "must have at least " + minCount + " item(s) (has " + value.Items.Count + ")");
            }

            return list;
        }

        /// <summary>
        /// An array of exactly <paramref name="count"/> numbers, or (with <paramref name="multiple"/>) a non-empty multiple
        /// of <paramref name="count"/> of them, each in range. A bad array reports and gives back zeros.
        /// </summary>
        public double[] Numbers(string key, int count, bool multiple = false, double min = double.MinValue, double max = double.MaxValue)
        {
            var value = Take(key, JsonKind.Array, "an array of numbers");
            if (value == null)
            {
                return new double[multiple ? 0 : count];
            }

            bool sizeOk = multiple ? value.Items.Count > 0 && value.Items.Count % count == 0 : value.Items.Count == count;
            if (!sizeOk)
            {
                Fail(key, value, (multiple ? "must hold a multiple of " + count + " numbers" : "must hold exactly " + count + " numbers") + " (has " + value.Items.Count + ")");
                return new double[multiple ? 0 : count];
            }

            var numbers = new double[value.Items.Count];
            for (int i = 0; i < numbers.Length; i++)
            {
                var item = value.Items[i];
                if (item.Kind != JsonKind.Number || double.IsNaN(item.Number) || double.IsInfinity(item.Number))
                {
                    Fail(key, item, "must hold only numbers (has " + Describe(item) + ")");
                    return new double[multiple ? 0 : count];
                }

                if (item.Number < min || item.Number > max)
                {
                    Fail(key, item, "must have every number " + Range(min, max) + " (has " + Show(item.Number) + ")");
                }

                numbers[i] = item.Number;
            }

            return numbers;
        }

        /// <summary>
        /// An array of number arrays (outlines, segments): each row exactly <paramref name="length"/> numbers (or, with
        /// <paramref name="multiple"/>, a non-empty multiple of it), every number in range, with at least <paramref name="minRows"/> rows.
        /// </summary>
        public List<double[]> Rows(string key, int length, bool multiple = false, double min = double.MinValue, double max = double.MaxValue, int minRows = 0)
        {
            var rows = new List<double[]>();
            var value = Take(key, JsonKind.Array, "an array of number arrays");
            if (value == null)
            {
                return rows;
            }

            for (int r = 0; r < value.Items.Count; r++)
            {
                var row = value.Items[r];
                if (row.Kind != JsonKind.Array)
                {
                    Fail(key, row, "row " + r + " must be an array of numbers (is " + Describe(row) + ")");
                    continue;
                }

                bool sizeOk = multiple ? row.Items.Count > 0 && row.Items.Count % length == 0 : row.Items.Count == length;
                if (!sizeOk)
                {
                    Fail(key, row, "row " + r + (multiple ? " must hold a multiple of " + length + " numbers" : " must hold exactly " + length + " numbers") + " (has " + row.Items.Count + ")");
                    continue;
                }

                var numbers = new double[row.Items.Count];
                bool good = true;
                for (int i = 0; i < numbers.Length; i++)
                {
                    var item = row.Items[i];
                    if (item.Kind != JsonKind.Number || double.IsNaN(item.Number) || double.IsInfinity(item.Number))
                    {
                        Fail(key, item, "row " + r + " must hold only numbers (has " + Describe(item) + ")");
                        good = false;
                        break;
                    }

                    if (item.Number < min || item.Number > max)
                    {
                        Fail(key, item, "row " + r + " must have every number " + Range(min, max) + " (has " + Show(item.Number) + ")");
                    }

                    numbers[i] = item.Number;
                }

                if (good)
                {
                    rows.Add(numbers);
                }
            }

            if (value.Items.Count < minRows)
            {
                Fail(key, value, "must have at least " + minRows + " row(s) (has " + value.Items.Count + ")");
            }

            return rows;
        }

        /// <summary>One of <typeparamref name="T"/>'s names, written as in the code ("Abstract").</summary>
        public T Choice<T>(string key) where T : struct
        {
            var value = Take(key, JsonKind.String, "a name");
            if (value == null)
            {
                return default;
            }

            if (Enum.TryParse<T>(value.Text, false, out var parsed) && Enum.IsDefined(typeof(T), parsed))
            {
                return parsed;
            }

            Fail(key, value, "must be one of " + string.Join(", ", Enum.GetNames(typeof(T))) + " (is \"" + value.Text + "\")");
            return default;
        }

        /// <summary>A cross-field check: records <paramref name="message"/> against <paramref name="key"/> when <paramref name="ok"/> is false.</summary>
        public void Check(bool ok, string key, string message)
        {
            if (!ok)
            {
                Fail(key, _object?.Get(key), message);
            }
        }

        /// <summary>Reports every key of this object that nothing read. Call once its keys have all been read.</summary>
        public void Done()
        {
            if (_object == null)
            {
                return;
            }

            foreach (var member in _object.Members)
            {
                if (!_read.Contains(member.Key) && !member.Key.StartsWith("_", StringComparison.Ordinal))
                {
                    _errors.Add(_path + ": unknown key '" + member.Key + "' (line " + member.Value.Line + "); check its spelling");
                }
            }
        }

        private JsonValue Take(string key, JsonKind kind, string what)
        {
            _read.Add(key);
            if (_object == null)
            {
                return null; // the enclosing object is missing, already reported
            }

            var value = _object.Get(key);
            if (value == null)
            {
                _errors.Add(_path + ": '" + key + "' is missing (expected " + what + ") (line " + _object.Line + ")");
                return null;
            }

            if (value.Kind != kind)
            {
                Fail(key, value, "must be " + what + " (is " + Describe(value) + ")");
                return null;
            }

            return value;
        }

        private void Fail(string key, JsonValue value, string message)
        {
            var where = value != null ? " (line " + value.Line + ")" : string.Empty;
            _errors.Add(_path + ": '" + key + "' " + message + where);
        }

        private static string Describe(JsonValue value)
        {
            switch (value.Kind)
            {
                case JsonKind.Null: return "null";
                case JsonKind.Bool: return value.Bool ? "true" : "false";
                case JsonKind.Number: return Show(value.Number);
                case JsonKind.String: return "\"" + value.Text + "\"";
                case JsonKind.Array: return "an array";
                default: return "an object";
            }
        }

        private static string Range(double min, double max)
        {
            var hasMin = min > double.MinValue;
            var hasMax = max < double.MaxValue;
            if (hasMin && hasMax)
            {
                return "between " + Show(min) + " and " + Show(max);
            }

            return hasMin ? "at least " + Show(min) : "at most " + Show(max);
        }

        private static string Show(double number) => number.ToString("G6", CultureInfo.InvariantCulture);
    }
}
