using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Yes2SDK
{
    internal static class ControlPlaneStrictJson
    {
        public const long MaxSafeInteger = 9007199254740991;
        public const int MaxDepth = 64;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        private static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            LineInfoHandling = LineInfoHandling.Ignore
        };

        public static JToken Parse(byte[] utf8)
        {
            if (utf8 == null)
            {
                throw new ArgumentNullException(nameof(utf8));
            }
            string json;
            try
            {
                json = Utf8.GetString(utf8);
            }
            catch (ArgumentException e)
            {
                throw new FormatException("JSON is not valid UTF-8.", e);
            }
            return Parse(json);
        }

        public static JToken Parse(string json)
        {
            new Grammar(json).Validate();
            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;
                reader.MaxDepth = null;
                try
                {
                    return JToken.ReadFrom(reader, LoadSettings);
                }
                catch (JsonException e)
                {
                    throw new FormatException(e.Message, e);
                }
            }
        }

        public static bool TryGetSafeInteger(JToken token, out long value)
        {
            value = 0;
            if (token == null)
            {
                return false;
            }
            if (token.Type == JTokenType.Integer)
            {
                if (((JValue)token).Value is long integer && integer >= -MaxSafeInteger && integer <= MaxSafeInteger)
                {
                    value = integer;
                    return true;
                }
                return false;
            }
            if (token.Type == JTokenType.Float)
            {
                var number = token.Value<double>();
                if (Math.Floor(number) == number && Math.Abs(number) <= MaxSafeInteger)
                {
                    value = (long)number;
                    return true;
                }
            }
            return false;
        }

        private sealed class Grammar
        {
            private readonly string _s;
            private int _i;

            public Grammar(string s)
            {
                _s = s ?? throw new ArgumentNullException(nameof(s));
            }

            public void Validate()
            {
                SkipWhitespace();
                Value(1);
                SkipWhitespace();
                if (_i != _s.Length)
                {
                    throw Fail("unexpected content after the JSON value");
                }
            }

            private void Value(int depth)
            {
                switch (Peek())
                {
                    case '{':
                        ReadObject(depth);
                        break;
                    case '[':
                        ReadArray(depth);
                        break;
                    case '"':
                        ReadString();
                        break;
                    case 't':
                        ReadLiteral("true");
                        break;
                    case 'f':
                        ReadLiteral("false");
                        break;
                    case 'n':
                        ReadLiteral("null");
                        break;
                    default:
                        ReadNumber();
                        break;
                }
            }

            private void ReadObject(int depth)
            {
                Enter(depth);
                SkipWhitespace();
                if (Peek() == '}')
                {
                    _i++;
                    return;
                }
                while (true)
                {
                    if (Peek() != '"')
                    {
                        throw Fail("expected a double-quoted property name");
                    }
                    ReadString();
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    Value(depth + 1);
                    SkipWhitespace();
                    if (Peek() == ',')
                    {
                        _i++;
                        SkipWhitespace();
                        continue;
                    }
                    Expect('}');
                    return;
                }
            }

            private void ReadArray(int depth)
            {
                Enter(depth);
                SkipWhitespace();
                if (Peek() == ']')
                {
                    _i++;
                    return;
                }
                while (true)
                {
                    Value(depth + 1);
                    SkipWhitespace();
                    if (Peek() == ',')
                    {
                        _i++;
                        SkipWhitespace();
                        continue;
                    }
                    Expect(']');
                    return;
                }
            }

            private void Enter(int depth)
            {
                if (depth > MaxDepth)
                {
                    throw Fail("nesting deeper than " + MaxDepth);
                }
                _i++;
            }

            private void ReadString()
            {
                _i++;
                while (true)
                {
                    var c = Peek();
                    _i++;
                    if (c == '"')
                    {
                        return;
                    }
                    if (c == '\\')
                    {
                        var e = Peek();
                        _i++;
                        if (e == 'u')
                        {
                            for (var k = 0; k < 4; k++, _i++)
                            {
                                if (!Uri.IsHexDigit(Peek()))
                                {
                                    throw Fail("invalid \\u escape");
                                }
                            }
                        }
                        else if ("\"\\/bfnrt".IndexOf(e) < 0)
                        {
                            throw Fail("invalid escape");
                        }
                    }
                    else if (c < ' ')
                    {
                        throw Fail("unescaped control character in string");
                    }
                }
            }

            private void ReadLiteral(string literal)
            {
                if (string.CompareOrdinal(_s, _i, literal, 0, literal.Length) != 0)
                {
                    throw Fail("invalid literal");
                }
                _i += literal.Length;
            }

            private void ReadNumber()
            {
                var start = _i;
                if (Peek() == '-')
                {
                    _i++;
                }
                if (Peek() == '0')
                {
                    _i++;
                }
                else
                {
                    Digits();
                }
                if (_i < _s.Length && _s[_i] == '.')
                {
                    _i++;
                    Digits();
                }
                if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    _i++;
                    if (Peek() == '+' || Peek() == '-')
                    {
                        _i++;
                    }
                    Digits();
                }

                var literal = _s.Substring(start, _i - start);
                if (!double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    || double.IsInfinity(value) || double.IsNaN(value))
                {
                    _i = start;
                    throw Fail("number is not finite");
                }
            }

            private void Digits()
            {
                if (!IsDigit(Peek()))
                {
                    throw Fail("expected a digit");
                }
                while (_i < _s.Length && IsDigit(_s[_i]))
                {
                    _i++;
                }
            }

            private static bool IsDigit(char c)
            {
                return c >= '0' && c <= '9';
            }

            private void Expect(char c)
            {
                if (Peek() != c)
                {
                    throw Fail("expected '" + c + "'");
                }
                _i++;
            }

            private char Peek()
            {
                if (_i >= _s.Length)
                {
                    throw Fail("unexpected end of JSON");
                }
                return _s[_i];
            }

            private void SkipWhitespace()
            {
                while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t' || _s[_i] == '\n' || _s[_i] == '\r'))
                {
                    _i++;
                }
            }

            private FormatException Fail(string reason)
            {
                return new FormatException("Invalid JSON at offset " + _i + ": " + reason + ".");
            }
        }
    }
}
