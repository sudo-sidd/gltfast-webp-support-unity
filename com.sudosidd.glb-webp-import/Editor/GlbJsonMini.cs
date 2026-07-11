// Minimal, dependency-free JSON parser/serializer.
// Scope is deliberately narrow: just enough to losslessly round-trip a glTF JSON
// chunk (objects, arrays, strings, numbers, bool, null) so GlbWebpSanitizer can
// patch a handful of fields without pulling in a Newtonsoft.Json package reference.
// This is not a general-purpose JSON library.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SudoSidd.GlbWebpImport
{
    static class GlbJsonMini
    {
        // Object -> Dictionary<string, object>
        // Array  -> List<object>
        // String -> string
        // Number -> long (no '.', 'e'/'E') or double (otherwise)
        // Bool   -> bool
        // Null   -> null

        public static object Parse(string json)
        {
            int i = 0;
            return ParseValue(json, ref i);
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't':
                case 'f': return ParseBool(s, ref i);
                case 'n': ExpectLiteral(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var obj = new Dictionary<string, object>();
            i++; // consume '{'
            SkipWhitespace(s, ref i);
            if (s[i] == '}') { i++; return obj; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (s[i] != ':') throw new FormatException($"GlbJsonMini: expected ':' at {i}");
                i++;
                object val = ParseValue(s, ref i);
                obj[key] = val;
                SkipWhitespace(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; break; }
                throw new FormatException($"GlbJsonMini: expected ',' or '}}' at {i}");
            }
            return obj;
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var arr = new List<object>();
            i++; // consume '['
            SkipWhitespace(s, ref i);
            if (s[i] == ']') { i++; return arr; }
            while (true)
            {
                object val = ParseValue(s, ref i);
                arr.Add(val);
                SkipWhitespace(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; break; }
                throw new FormatException($"GlbJsonMini: expected ',' or ']' at {i}");
            }
            return arr;
        }

        static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException($"GlbJsonMini: expected '\"' at {i}");
            i++;
            var sb = new StringBuilder();
            while (s[i] != '"')
            {
                char c = s[i];
                if (c == '\\')
                {
                    i++;
                    char esc = s[i];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            string hex = s.Substring(i + 1, 4);
                            sb.Append((char)Convert.ToInt32(hex, 16));
                            i += 4;
                            break;
                        default: throw new FormatException($"GlbJsonMini: bad escape at {i}");
                    }
                    i++;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            i++; // consume closing quote
            return sb.ToString();
        }

        static bool ParseBool(string s, ref int i)
        {
            if (s[i] == 't') { ExpectLiteral(s, ref i, "true"); return true; }
            ExpectLiteral(s, ref i, "false");
            return false;
        }

        static void ExpectLiteral(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
                throw new FormatException($"GlbJsonMini: expected '{literal}' at {i}");
            i += literal.Length;
        }

        static object ParseNumber(string s, ref int i)
        {
            int start = i;
            bool isFloat = false;
            if (s[i] == '-') i++;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i < s.Length && s[i] == '.')
            {
                isFloat = true;
                i++;
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                isFloat = true;
                i++;
                if (s[i] == '+' || s[i] == '-') i++;
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            string numStr = s.Substring(start, i - start);
            return isFloat
                ? (object)double.Parse(numStr, CultureInfo.InvariantCulture)
                : (object)long.Parse(numStr, CultureInfo.InvariantCulture);
        }

        static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string s: WriteString(sb, s); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case Dictionary<string, object> obj: WriteObject(sb, obj); break;
                case List<object> arr: WriteArray(sb, arr); break;
                default: throw new FormatException($"GlbJsonMini: unsupported value type {value.GetType()}");
            }
        }

        static void WriteObject(StringBuilder sb, Dictionary<string, object> obj)
        {
            sb.Append('{');
            bool first = true;
            foreach (var kv in obj)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(sb, kv.Key);
                sb.Append(':');
                WriteValue(sb, kv.Value);
            }
            sb.Append('}');
        }

        static void WriteArray(StringBuilder sb, List<object> arr)
        {
            sb.Append('[');
            for (int i = 0; i < arr.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteValue(sb, arr[i]);
            }
            sb.Append(']');
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
