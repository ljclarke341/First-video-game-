using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GarageTycoon.Core.Save
{
    /// <summary>The kinds of value a <see cref="JsonValue"/> can hold.</summary>
    public enum JsonType
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object
    }

    /// <summary>
    /// A very small JSON value type, plus a parser and writer.
    ///
    /// Why not JsonUtility? Because JsonUtility lives in UnityEngine, and the whole point of the Core
    /// assembly is that it compiles and runs without Unity. This is about 200 lines, handles everything
    /// the save file needs, and is covered by the save tests.
    /// </summary>
    public sealed class JsonValue
    {
        public JsonType Type { get; private set; }

        private bool _bool;
        private double _number;
        private string _string;
        private List<JsonValue> _array;
        private Dictionary<string, JsonValue> _object;

        private JsonValue(JsonType type)
        {
            Type = type;
        }

        // ---------------- Factories ----------------

        public static JsonValue Null() { return new JsonValue(JsonType.Null); }

        public static JsonValue Bool(bool value)
        {
            JsonValue json = new JsonValue(JsonType.Bool);
            json._bool = value;
            return json;
        }

        public static JsonValue Number(double value)
        {
            JsonValue json = new JsonValue(JsonType.Number);
            json._number = value;
            return json;
        }

        public static JsonValue String(string value)
        {
            JsonValue json = new JsonValue(JsonType.String);
            json._string = value ?? string.Empty;
            return json;
        }

        public static JsonValue Array()
        {
            JsonValue json = new JsonValue(JsonType.Array);
            json._array = new List<JsonValue>();
            return json;
        }

        public static JsonValue Object()
        {
            JsonValue json = new JsonValue(JsonType.Object);
            json._object = new Dictionary<string, JsonValue>();
            return json;
        }

        // ---------------- Readers (all forgiving: a missing or wrong-typed field returns the default) ----------------

        public int Count
        {
            get
            {
                if (Type == JsonType.Array) return _array.Count;
                if (Type == JsonType.Object) return _object.Count;
                return 0;
            }
        }

        public IReadOnlyList<JsonValue> Items
        {
            get { return Type == JsonType.Array ? (IReadOnlyList<JsonValue>)_array : new List<JsonValue>(); }
        }

        public IEnumerable<KeyValuePair<string, JsonValue>> Fields
        {
            get
            {
                if (Type == JsonType.Object) return _object;
                return new Dictionary<string, JsonValue>();
            }
        }

        public bool Has(string key)
        {
            return Type == JsonType.Object && _object.ContainsKey(key);
        }

        public JsonValue this[string key]
        {
            get
            {
                JsonValue value;
                if (Type == JsonType.Object && _object.TryGetValue(key, out value)) return value;
                return Null();
            }
        }

        public JsonValue this[int index]
        {
            get
            {
                if (Type == JsonType.Array && index >= 0 && index < _array.Count) return _array[index];
                return Null();
            }
        }

        public void Add(string key, JsonValue value)
        {
            if (Type != JsonType.Object) return;
            _object[key] = value ?? Null();
        }

        public void Add(string key, double value) { Add(key, Number(value)); }
        public void Add(string key, int value) { Add(key, Number(value)); }
        public void Add(string key, bool value) { Add(key, Bool(value)); }
        public void Add(string key, string value) { Add(key, String(value)); }

        public void Append(JsonValue value)
        {
            if (Type != JsonType.Array) return;
            _array.Add(value ?? Null());
        }

        public bool AsBool(bool fallback = false)
        {
            if (Type == JsonType.Bool) return _bool;
            if (Type == JsonType.Number) return _number != 0d;
            return fallback;
        }

        public double AsDouble(double fallback = 0d)
        {
            return Type == JsonType.Number ? _number : fallback;
        }

        public float AsFloat(float fallback = 0f)
        {
            return Type == JsonType.Number ? (float)_number : fallback;
        }

        public int AsInt(int fallback = 0)
        {
            return Type == JsonType.Number ? (int)Math.Round(_number) : fallback;
        }

        public string AsString(string fallback = "")
        {
            return Type == JsonType.String ? _string : fallback;
        }

        // ---------------- Writing ----------------

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder(512);
            Write(builder);
            return builder.ToString();
        }

        private void Write(StringBuilder builder)
        {
            switch (Type)
            {
                case JsonType.Null:
                    builder.Append("null");
                    break;

                case JsonType.Bool:
                    builder.Append(_bool ? "true" : "false");
                    break;

                case JsonType.Number:
                    // "R" round-trips exactly; InvariantCulture keeps decimal points as dots on every device.
                    builder.Append(_number.ToString("R", CultureInfo.InvariantCulture));
                    break;

                case JsonType.String:
                    WriteEscapedString(builder, _string);
                    break;

                case JsonType.Array:
                    builder.Append('[');
                    for (int i = 0; i < _array.Count; i++)
                    {
                        if (i > 0) builder.Append(',');
                        _array[i].Write(builder);
                    }
                    builder.Append(']');
                    break;

                case JsonType.Object:
                    builder.Append('{');
                    bool first = true;
                    foreach (KeyValuePair<string, JsonValue> pair in _object)
                    {
                        if (!first) builder.Append(',');
                        first = false;
                        WriteEscapedString(builder, pair.Key);
                        builder.Append(':');
                        pair.Value.Write(builder);
                    }
                    builder.Append('}');
                    break;
            }
        }

        private static void WriteEscapedString(StringBuilder builder, string value)
        {
            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
            builder.Append('"');
        }

        // ---------------- Parsing ----------------

        /// <summary>
        /// Parses JSON text. Returns null when the text is not valid JSON, so callers can fall back
        /// to a fresh save rather than crashing on a corrupted file.
        /// </summary>
        public static JsonValue Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            try
            {
                int index = 0;
                JsonValue value = ParseValue(text, ref index);
                SkipWhitespace(text, ref index);
                return value;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static JsonValue ParseValue(string text, ref int index)
        {
            SkipWhitespace(text, ref index);
            if (index >= text.Length) throw new FormatException("Unexpected end of JSON");

            char c = text[index];

            if (c == '{') return ParseObject(text, ref index);
            if (c == '[') return ParseArray(text, ref index);
            if (c == '"') return String(ParseString(text, ref index));

            if (Match(text, ref index, "true")) return Bool(true);
            if (Match(text, ref index, "false")) return Bool(false);
            if (Match(text, ref index, "null")) return Null();

            return Number(ParseNumber(text, ref index));
        }

        private static JsonValue ParseObject(string text, ref int index)
        {
            JsonValue result = Object();
            index++; // consume '{'

            SkipWhitespace(text, ref index);
            if (index < text.Length && text[index] == '}') { index++; return result; }

            while (index < text.Length)
            {
                SkipWhitespace(text, ref index);
                string key = ParseString(text, ref index);

                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != ':') throw new FormatException("Expected ':'");
                index++;

                result.Add(key, ParseValue(text, ref index));

                SkipWhitespace(text, ref index);
                if (index >= text.Length) throw new FormatException("Unterminated object");

                if (text[index] == ',') { index++; continue; }
                if (text[index] == '}') { index++; return result; }

                throw new FormatException("Expected ',' or '}'");
            }

            throw new FormatException("Unterminated object");
        }

        private static JsonValue ParseArray(string text, ref int index)
        {
            JsonValue result = Array();
            index++; // consume '['

            SkipWhitespace(text, ref index);
            if (index < text.Length && text[index] == ']') { index++; return result; }

            while (index < text.Length)
            {
                result.Append(ParseValue(text, ref index));

                SkipWhitespace(text, ref index);
                if (index >= text.Length) throw new FormatException("Unterminated array");

                if (text[index] == ',') { index++; continue; }
                if (text[index] == ']') { index++; return result; }

                throw new FormatException("Expected ',' or ']'");
            }

            throw new FormatException("Unterminated array");
        }

        private static string ParseString(string text, ref int index)
        {
            if (index >= text.Length || text[index] != '"') throw new FormatException("Expected string");
            index++;

            StringBuilder builder = new StringBuilder();

            while (index < text.Length)
            {
                char c = text[index++];

                if (c == '"') return builder.ToString();

                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (index >= text.Length) break;
                char escape = text[index++];

                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length) throw new FormatException("Bad unicode escape");
                        string hex = text.Substring(index, 4);
                        index += 4;
                        builder.Append((char)Convert.ToInt32(hex, 16));
                        break;
                    default:
                        throw new FormatException("Bad escape character");
                }
            }

            throw new FormatException("Unterminated string");
        }

        private static double ParseNumber(string text, ref int index)
        {
            int start = index;

            while (index < text.Length)
            {
                char c = text[index];
                bool isNumberChar = (c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E';
                if (!isNumberChar) break;
                index++;
            }

            if (index == start) throw new FormatException("Expected number");

            string slice = text.Substring(start, index - start);
            return double.Parse(slice, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static bool Match(string text, ref int index, string literal)
        {
            if (index + literal.Length > text.Length) return false;
            if (string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0) return false;
            index += literal.Length;
            return true;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length)
            {
                char c = text[index];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') index++;
                else break;
            }
        }
    }
}
