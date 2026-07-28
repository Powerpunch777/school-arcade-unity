using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SchoolArcade.Editor.PixelLab
{
    internal enum PixelLabJsonKind
    {
        Null,
        Boolean,
        Number,
        String,
        Object,
        Array
    }

    internal sealed class PixelLabJsonValue
    {
        private PixelLabJsonValue(PixelLabJsonKind kind)
        {
            Kind = kind;
        }

        internal PixelLabJsonKind Kind { get; private set; }
        internal bool BooleanValue { get; private set; }
        internal double NumberValue { get; private set; }
        internal string StringValue { get; private set; }
        internal Dictionary<string, PixelLabJsonValue> ObjectValue { get; private set; }
        internal List<PixelLabJsonValue> ArrayValue { get; private set; }

        internal static PixelLabJsonValue Null()
        {
            return new PixelLabJsonValue(PixelLabJsonKind.Null);
        }

        internal static PixelLabJsonValue Boolean(bool value)
        {
            return new PixelLabJsonValue(PixelLabJsonKind.Boolean) { BooleanValue = value };
        }

        internal static PixelLabJsonValue Number(double value)
        {
            return new PixelLabJsonValue(PixelLabJsonKind.Number) { NumberValue = value };
        }

        internal static PixelLabJsonValue String(string value)
        {
            return new PixelLabJsonValue(PixelLabJsonKind.String) { StringValue = value };
        }

        internal static PixelLabJsonValue Object(Dictionary<string, PixelLabJsonValue> value)
        {
            return new PixelLabJsonValue(PixelLabJsonKind.Object) { ObjectValue = value };
        }

        internal static PixelLabJsonValue Array(List<PixelLabJsonValue> value)
        {
            return new PixelLabJsonValue(PixelLabJsonKind.Array) { ArrayValue = value };
        }

        internal string ToScalarString()
        {
            switch (Kind)
            {
                case PixelLabJsonKind.String:
                    return StringValue;
                case PixelLabJsonKind.Number:
                    return NumberValue.ToString("R", CultureInfo.InvariantCulture);
                case PixelLabJsonKind.Boolean:
                    return BooleanValue ? "true" : "false";
                default:
                    return null;
            }
        }
    }

    internal static class PixelLabJson
    {
        internal static bool TryParse(string json, out PixelLabJsonValue value, out string error)
        {
            value = null;
            error = string.Empty;
            if (json == null)
            {
                error = "JSON content is null.";
                return false;
            }

            try
            {
                var parser = new Parser(json);
                value = parser.ParseDocument();
                return true;
            }
            catch (FormatException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private sealed class Parser
        {
            private const int MaxDepth = 128;
            private readonly string text;
            private int index;

            internal Parser(string text)
            {
                this.text = text;
            }

            internal PixelLabJsonValue ParseDocument()
            {
                SkipWhitespace();
                PixelLabJsonValue result = ParseValue(0);
                SkipWhitespace();
                if (index != text.Length)
                {
                    Fail("Unexpected content after the root JSON value.");
                }

                return result;
            }

            private PixelLabJsonValue ParseValue(int depth)
            {
                if (depth > MaxDepth)
                {
                    Fail("JSON nesting exceeds the supported depth of " + MaxDepth + ".");
                }

                SkipWhitespace();
                if (index >= text.Length)
                {
                    Fail("Unexpected end of JSON.");
                }

                switch (text[index])
                {
                    case '{':
                        return ParseObject(depth + 1);
                    case '[':
                        return ParseArray(depth + 1);
                    case '"':
                        return PixelLabJsonValue.String(ParseString());
                    case 't':
                        ConsumeLiteral("true");
                        return PixelLabJsonValue.Boolean(true);
                    case 'f':
                        ConsumeLiteral("false");
                        return PixelLabJsonValue.Boolean(false);
                    case 'n':
                        ConsumeLiteral("null");
                        return PixelLabJsonValue.Null();
                    default:
                        if (text[index] == '-' || char.IsDigit(text[index]))
                        {
                            return PixelLabJsonValue.Number(ParseNumber());
                        }

                        Fail("Unexpected character '" + text[index] + "'.");
                        return null;
                }
            }

            private PixelLabJsonValue ParseObject(int depth)
            {
                Expect('{');
                SkipWhitespace();
                var values = new Dictionary<string, PixelLabJsonValue>(StringComparer.Ordinal);
                if (TryConsume('}'))
                {
                    return PixelLabJsonValue.Object(values);
                }

                while (true)
                {
                    SkipWhitespace();
                    if (index >= text.Length || text[index] != '"')
                    {
                        Fail("Expected a quoted object key.");
                    }

                    string key = ParseString();
                    if (values.ContainsKey(key))
                    {
                        Fail("Duplicate object key '" + key + "'.");
                    }

                    SkipWhitespace();
                    Expect(':');
                    values.Add(key, ParseValue(depth));
                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return PixelLabJsonValue.Object(values);
                    }

                    Expect(',');
                }
            }

            private PixelLabJsonValue ParseArray(int depth)
            {
                Expect('[');
                SkipWhitespace();
                var values = new List<PixelLabJsonValue>();
                if (TryConsume(']'))
                {
                    return PixelLabJsonValue.Array(values);
                }

                while (true)
                {
                    values.Add(ParseValue(depth));
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return PixelLabJsonValue.Array(values);
                    }

                    Expect(',');
                }
            }

            private string ParseString()
            {
                Expect('"');
                var builder = new StringBuilder();
                while (index < text.Length)
                {
                    char current = text[index++];
                    if (current == '"')
                    {
                        return builder.ToString();
                    }

                    if (current < 0x20)
                    {
                        Fail("Unescaped control character in a JSON string.");
                    }

                    if (current != '\\')
                    {
                        builder.Append(current);
                        continue;
                    }

                    if (index >= text.Length)
                    {
                        Fail("Unterminated JSON escape sequence.");
                    }

                    char escaped = text[index++];
                    switch (escaped)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u': builder.Append(ParseUnicodeEscape()); break;
                        default:
                            Fail("Unsupported JSON escape sequence '\\" + escaped + "'.");
                            break;
                    }
                }

                Fail("Unterminated JSON string.");
                return null;
            }

            private char ParseUnicodeEscape()
            {
                if (index + 4 > text.Length)
                {
                    Fail("Incomplete JSON unicode escape.");
                }

                int value = 0;
                for (int i = 0; i < 4; i++)
                {
                    char digit = text[index++];
                    value <<= 4;
                    if (digit >= '0' && digit <= '9') value += digit - '0';
                    else if (digit >= 'a' && digit <= 'f') value += digit - 'a' + 10;
                    else if (digit >= 'A' && digit <= 'F') value += digit - 'A' + 10;
                    else Fail("Invalid hexadecimal digit in a JSON unicode escape.");
                }

                return (char)value;
            }

            private double ParseNumber()
            {
                int start = index;
                if (TryConsume('-') && index >= text.Length)
                {
                    Fail("Incomplete JSON number.");
                }

                if (TryConsume('0'))
                {
                    if (index < text.Length && char.IsDigit(text[index]))
                    {
                        Fail("A JSON number cannot contain a leading zero.");
                    }
                }
                else
                {
                    if (index >= text.Length || text[index] < '1' || text[index] > '9')
                    {
                        Fail("Invalid JSON number.");
                    }

                    while (index < text.Length && char.IsDigit(text[index])) index++;
                }

                if (TryConsume('.'))
                {
                    int fractionStart = index;
                    while (index < text.Length && char.IsDigit(text[index])) index++;
                    if (fractionStart == index) Fail("Expected digits after the decimal point.");
                }

                if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
                {
                    index++;
                    if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
                    int exponentStart = index;
                    while (index < text.Length && char.IsDigit(text[index])) index++;
                    if (exponentStart == index) Fail("Expected digits in the JSON number exponent.");
                }

                string token = text.Substring(start, index - start);
                double value;
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    || double.IsInfinity(value)
                    || double.IsNaN(value))
                {
                    Fail("JSON number is outside the supported range.");
                }

                return value;
            }

            private void ConsumeLiteral(string literal)
            {
                if (index + literal.Length > text.Length
                    || !string.Equals(text.Substring(index, literal.Length), literal, StringComparison.Ordinal))
                {
                    Fail("Expected JSON literal '" + literal + "'.");
                }

                index += literal.Length;
            }

            private void SkipWhitespace()
            {
                while (index < text.Length)
                {
                    char current = text[index];
                    if (current != ' ' && current != '\t' && current != '\r' && current != '\n')
                    {
                        break;
                    }

                    index++;
                }
            }

            private bool TryConsume(char expected)
            {
                if (index >= text.Length || text[index] != expected)
                {
                    return false;
                }

                index++;
                return true;
            }

            private void Expect(char expected)
            {
                if (!TryConsume(expected))
                {
                    Fail("Expected '" + expected + "'.");
                }
            }

            private void Fail(string message)
            {
                throw new FormatException(message + " (character " + index + ")");
            }
        }
    }
}
