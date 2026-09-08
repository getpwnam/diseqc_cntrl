using System;

namespace CubleyControl
{
    /// <summary>
    /// Bounded reader for the flat JSON object subset defined by
    /// docs/software/DEVICE_API_V2.md: string, integer, boolean and null
    /// members only, with no nesting. The limits are part of the wire
    /// contract rather than an implementation detail -- they are what keeps
    /// inbound parsing allocation-bounded on a 192 KB target.
    /// </summary>
    internal sealed class JsonObject
    {
        internal const int MaxMembers = 12;
        internal const int MaxKeyLength = 24;
        internal const int MaxStringLength = 128;

        internal const int KindString = 0;
        internal const int KindInt = 1;
        internal const int KindBool = 2;
        internal const int KindNull = 3;

        private readonly string[] _keys = new string[MaxMembers];
        private readonly string[] _strings = new string[MaxMembers];
        private readonly int[] _numbers = new int[MaxMembers];
        private readonly int[] _kinds = new int[MaxMembers];
        private int _count;

        internal int Count
        {
            get { return _count; }
        }

        internal string KeyAt(int index)
        {
            return index < 0 || index >= _count ? null : _keys[index];
        }

        internal bool Add(string key, int kind, string text, int number)
        {
            if (_count >= MaxMembers || IndexOf(key) >= 0)
            {
                return false;
            }

            _keys[_count] = key;
            _kinds[_count] = kind;
            _strings[_count] = text;
            _numbers[_count] = number;
            _count++;
            return true;
        }

        internal int IndexOf(string key)
        {
            for (int index = 0; index < _count; index++)
            {
                if (_keys[index] == key)
                {
                    return index;
                }
            }

            return -1;
        }

        internal bool Has(string key)
        {
            return IndexOf(key) >= 0;
        }

        /// <summary>Null members read as absent, so "x":null and a missing x behave alike.</summary>
        internal bool TryGetString(string key, out string value)
        {
            value = null;
            int index = IndexOf(key);
            if (index < 0 || _kinds[index] != KindString)
            {
                return false;
            }

            value = _strings[index];
            return true;
        }

        internal bool TryGetInt(string key, out int value)
        {
            value = 0;
            int index = IndexOf(key);
            if (index < 0 || _kinds[index] != KindInt)
            {
                return false;
            }

            value = _numbers[index];
            return true;
        }

        internal bool TryGetBool(string key, out bool value)
        {
            value = false;
            int index = IndexOf(key);
            if (index < 0 || _kinds[index] != KindBool)
            {
                return false;
            }

            value = _numbers[index] != 0;
            return true;
        }
    }

    internal static class Json
    {
        internal static bool TryParseObject(string text, out JsonObject result, out string error)
        {
            result = null;
            error = string.Empty;

            if (text == null || text.Length == 0)
            {
                error = "empty payload";
                return false;
            }

            JsonObject obj = new JsonObject();
            int index = SkipWhitespace(text, 0);
            if (index >= text.Length || text[index] != '{')
            {
                error = "payload must be a JSON object";
                return false;
            }

            index++;
            index = SkipWhitespace(text, index);
            if (index < text.Length && text[index] == '}')
            {
                index = SkipWhitespace(text, index + 1);
                if (index != text.Length)
                {
                    error = "trailing content after object";
                    return false;
                }

                result = obj;
                return true;
            }

            while (true)
            {
                index = SkipWhitespace(text, index);
                string key;
                if (index >= text.Length || text[index] != '"')
                {
                    error = "expected a member name";
                    return false;
                }

                if (!TryParseString(text, ref index, JsonObject.MaxKeyLength, out key, out error))
                {
                    return false;
                }

                if (key.Length == 0)
                {
                    error = "member name must not be empty";
                    return false;
                }

                index = SkipWhitespace(text, index);
                if (index >= text.Length || text[index] != ':')
                {
                    error = "expected ':' after member name " + key;
                    return false;
                }

                index = SkipWhitespace(text, index + 1);
                if (index >= text.Length)
                {
                    error = "missing value for member " + key;
                    return false;
                }

                int kind;
                string valueText = null;
                int valueNumber = 0;
                char lead = text[index];
                if (lead == '"')
                {
                    if (!TryParseString(text, ref index, JsonObject.MaxStringLength, out valueText, out error))
                    {
                        return false;
                    }

                    kind = JsonObject.KindString;
                }
                else if (lead == 't' || lead == 'f')
                {
                    bool boolValue;
                    if (!TryParseLiteralBool(text, ref index, out boolValue))
                    {
                        error = "invalid value for member " + key;
                        return false;
                    }

                    kind = JsonObject.KindBool;
                    valueNumber = boolValue ? 1 : 0;
                }
                else if (lead == 'n')
                {
                    if (!TryParseLiteral(text, ref index, "null"))
                    {
                        error = "invalid value for member " + key;
                        return false;
                    }

                    kind = JsonObject.KindNull;
                }
                else if (lead == '-' || (lead >= '0' && lead <= '9'))
                {
                    if (!TryParseInteger(text, ref index, out valueNumber, out error))
                    {
                        return false;
                    }

                    kind = JsonObject.KindInt;
                }
                else if (lead == '{' || lead == '[')
                {
                    error = "member " + key + " must be a scalar; objects and arrays are not accepted";
                    return false;
                }
                else
                {
                    error = "invalid value for member " + key;
                    return false;
                }

                if (!obj.Add(key, kind, valueText, valueNumber))
                {
                    error = obj.Has(key)
                        ? "duplicate member " + key
                        : "too many members; limit is " + JsonObject.MaxMembers.ToString();
                    return false;
                }

                index = SkipWhitespace(text, index);
                if (index < text.Length && text[index] == ',')
                {
                    index++;
                    continue;
                }

                if (index < text.Length && text[index] == '}')
                {
                    index++;
                    break;
                }

                error = "expected ',' or '}' after member " + key;
                return false;
            }

            index = SkipWhitespace(text, index);
            if (index != text.Length)
            {
                error = "trailing content after object";
                return false;
            }

            result = obj;
            return true;
        }

        private static int SkipWhitespace(string text, int index)
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

            return index;
        }

        private static bool TryParseString(string text, ref int index, int maxLength, out string value, out string error)
        {
            value = null;
            error = string.Empty;

            // Caller has already checked for the opening quote.
            int cursor = index + 1;
            string accumulated = string.Empty;
            while (true)
            {
                if (cursor >= text.Length)
                {
                    error = "unterminated string";
                    return false;
                }

                char current = text[cursor];
                if (current == '"')
                {
                    cursor++;
                    break;
                }

                if (current == '\\')
                {
                    cursor++;
                    if (cursor >= text.Length)
                    {
                        error = "unterminated escape sequence";
                        return false;
                    }

                    char escape = text[cursor];
                    if (escape == '"' || escape == '\\' || escape == '/')
                    {
                        accumulated += escape;
                    }
                    else if (escape == 'b')
                    {
                        accumulated += '\b';
                    }
                    else if (escape == 'f')
                    {
                        accumulated += '\f';
                    }
                    else if (escape == 'n')
                    {
                        accumulated += '\n';
                    }
                    else if (escape == 'r')
                    {
                        accumulated += '\r';
                    }
                    else if (escape == 't')
                    {
                        accumulated += '\t';
                    }
                    else if (escape == 'u')
                    {
                        int codePoint = 0;
                        for (int digit = 0; digit < 4; digit++)
                        {
                            cursor++;
                            int nibble;
                            if (cursor >= text.Length || !TryHexDigit(text[cursor], out nibble))
                            {
                                error = "invalid \\u escape sequence";
                                return false;
                            }

                            codePoint = (codePoint << 4) | nibble;
                        }

                        accumulated += (char)codePoint;
                    }
                    else
                    {
                        error = "invalid escape sequence";
                        return false;
                    }

                    cursor++;
                    if (accumulated.Length > maxLength)
                    {
                        error = "string exceeds " + maxLength.ToString() + " characters";
                        return false;
                    }

                    continue;
                }

                if (current < ' ')
                {
                    error = "unescaped control character in string";
                    return false;
                }

                accumulated += current;
                cursor++;
                if (accumulated.Length > maxLength)
                {
                    error = "string exceeds " + maxLength.ToString() + " characters";
                    return false;
                }
            }

            index = cursor;
            value = accumulated;
            return true;
        }

        private static bool TryParseInteger(string text, ref int index, out int value, out string error)
        {
            value = 0;
            error = string.Empty;

            int cursor = index;
            bool negative = false;
            if (text[cursor] == '-')
            {
                negative = true;
                cursor++;
                if (cursor >= text.Length || text[cursor] < '0' || text[cursor] > '9')
                {
                    error = "malformed number";
                    return false;
                }
            }

            int digitStart = cursor;
            long accumulated = 0;
            while (cursor < text.Length && text[cursor] >= '0' && text[cursor] <= '9')
            {
                accumulated = (accumulated * 10) + (text[cursor] - '0');
                if (accumulated > 2147483648L)
                {
                    error = "number out of range";
                    return false;
                }

                cursor++;
            }

            int digitCount = cursor - digitStart;
            if (digitCount == 0)
            {
                error = "malformed number";
                return false;
            }

            if (digitCount > 1 && text[digitStart] == '0')
            {
                error = "number must not have a leading zero";
                return false;
            }

            if (cursor < text.Length)
            {
                char trailing = text[cursor];
                if (trailing == '.' || trailing == 'e' || trailing == 'E')
                {
                    error = "number must be an integer";
                    return false;
                }
            }

            if (negative)
            {
                if (accumulated > 2147483648L)
                {
                    error = "number out of range";
                    return false;
                }

                value = (int)(-accumulated);
            }
            else
            {
                if (accumulated > 2147483647L)
                {
                    error = "number out of range";
                    return false;
                }

                value = (int)accumulated;
            }

            index = cursor;
            return true;
        }

        private static bool TryParseLiteralBool(string text, ref int index, out bool value)
        {
            if (TryParseLiteral(text, ref index, "true"))
            {
                value = true;
                return true;
            }

            value = false;
            return TryParseLiteral(text, ref index, "false");
        }

        private static bool TryParseLiteral(string text, ref int index, string literal)
        {
            if (index + literal.Length > text.Length)
            {
                return false;
            }

            for (int offset = 0; offset < literal.Length; offset++)
            {
                if (text[index + offset] != literal[offset])
                {
                    return false;
                }
            }

            index += literal.Length;
            return true;
        }

        private static bool TryHexDigit(char value, out int result)
        {
            if (value >= '0' && value <= '9')
            {
                result = value - '0';
                return true;
            }

            if (value >= 'a' && value <= 'f')
            {
                result = 10 + (value - 'a');
                return true;
            }

            if (value >= 'A' && value <= 'F')
            {
                result = 10 + (value - 'A');
                return true;
            }

            result = 0;
            return false;
        }

        /// <summary>Quotes and escapes a string for inclusion in an outbound payload.</summary>
        internal static string Quote(string value)
        {
            if (value == null)
            {
                return "null";
            }

            string escaped = "\"";
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (current == '"')
                {
                    escaped += "\\\"";
                }
                else if (current == '\\')
                {
                    escaped += "\\\\";
                }
                else if (current == '\n')
                {
                    escaped += "\\n";
                }
                else if (current == '\r')
                {
                    escaped += "\\r";
                }
                else if (current == '\t')
                {
                    escaped += "\\t";
                }
                else if (current == '\b')
                {
                    escaped += "\\b";
                }
                else if (current == '\f')
                {
                    escaped += "\\f";
                }
                else if (current < ' ' || current > '~')
                {
                    // The device contract is ASCII; anything outside it is
                    // escaped rather than emitted raw so the payload stays
                    // valid for consumers that assume UTF-8.
                    escaped += "\\u" + ToHex4(current);
                }
                else
                {
                    escaped += current;
                }
            }

            return escaped + "\"";
        }

        private static string ToHex4(char value)
        {
            const string Digits = "0123456789abcdef";
            int code = value;
            return new string(new char[]
            {
                Digits[(code >> 12) & 0xF],
                Digits[(code >> 8) & 0xF],
                Digits[(code >> 4) & 0xF],
                Digits[code & 0xF]
            });
        }
    }

    /// <summary>Minimal ordered writer for outbound payloads.</summary>
    internal sealed class JsonBuilder
    {
        private string _text = "{";
        private bool _hasMember;

        internal JsonBuilder AddString(string key, string value)
        {
            return AddRaw(key, Json.Quote(value));
        }

        internal JsonBuilder AddInt(string key, int value)
        {
            return AddRaw(key, value.ToString());
        }

        internal JsonBuilder AddLong(string key, long value)
        {
            return AddRaw(key, value.ToString());
        }

        internal JsonBuilder AddBool(string key, bool value)
        {
            return AddRaw(key, value ? "true" : "false");
        }

        /// <summary>Adds a pre-serialized value: a nested object, or "null".</summary>
        internal JsonBuilder AddRaw(string key, string rawValue)
        {
            if (_hasMember)
            {
                _text += ",";
            }

            _text += Json.Quote(key) + ":" + rawValue;
            _hasMember = true;
            return this;
        }

        internal string Build()
        {
            return _text + "}";
        }

        /// <summary>
        /// The member list without enclosing braces, so a cached response can
        /// be republished with an extra member added.
        /// </summary>
        internal string BuildBody()
        {
            return _text.Substring(1);
        }
    }
}
