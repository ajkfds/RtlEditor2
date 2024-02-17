using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ajkControls.Json
{
    public class JsonReader : IDisposable,IJson
    {
        public JsonReader(System.IO.StreamReader streamReader)
        {
            this.streamReader = streamReader;
            if(nextToken == null) nextToken = getToken();
            if (nextToken != "{") throw new Exception();
            nextToken = null;
        }

        System.IO.StreamReader streamReader;
        string nextToken = null;

        public void Dispose()
        {
            if (nextToken != "}") throw new Exception("illegal exception");
            disposed = true;
        }

        private bool disposed = false;
        public bool IsDisposed
        {
            get
            {
                return disposed;
            }
        }

        IJson childJson = null;
        private bool isChildOpened
        {
            get
            {
                if (childJson == null) return false;
                if (childJson.IsDisposed)
                {
                    childJson = null;
                    return false;
                }
                return true;
            }
        }

        public string GetNextKey()
        {
            if (childJson != null)
            {
                if (childJson.IsDisposed)
                {
                    childJson = null;
                    nextToken = getToken();
                    if(nextToken ==",") nextToken = getToken();
                }
                else throw new Exception("illegal format");
            }


            if (nextToken == null) nextToken = getToken();
            if (nextToken == null || nextToken == "}")
            {
                Dispose();
                return null;
            }

            if(nextToken.Length < 2 || nextToken.First() != '\"' || nextToken.Last() != '\"')
            {
                System.Diagnostics.Debugger.Break();
                throw new Exception("illegal format");
            }
            string token = nextToken;
            nextToken = getToken();
            if(nextToken != ":")
            {
                throw new Exception("illegal format");
            }

            nextToken = null;
            Key = token.Substring(1, token.Length - 2);
            return Key;
        }

        public string Key { get; protected set; }

        public int GetNextIntValue()
        {
            if (nextToken == null) nextToken = getToken();
            if (nextToken == null) throw new Exception("illegal format");
            int value;
            if (!int.TryParse(nextToken, out value)) throw new Exception("illegal format");
            nextToken = getToken();
            if(nextToken == ",") nextToken = getToken();
            return value;
        }

        public int GetNextDoubleValue()
        {
            if (nextToken == null) nextToken = getToken();
            if (nextToken == null) throw new Exception("illegal format");
            int value;
            if (!int.TryParse(nextToken, out value)) throw new Exception("illegal format");
            if (nextToken == ",") nextToken = getToken();
            return value;
        }

        public string GetNextStringValue()
        {
            if (nextToken == null) nextToken = getToken();
            if (nextToken == null) throw new Exception("illegal format");
            if (nextToken.Length < 2 || nextToken.First() != '\"' || nextToken.Last() != '\"')
            {
                throw new Exception("illegal format");
            }
            string value = nextToken.Substring(1, nextToken.Length - 2);
            nextToken = getToken();
            if (nextToken == ",") nextToken = getToken();
            return value;
        }

        public void SkipValue()
        {
            if (nextToken == null) nextToken = getToken();
            if (nextToken == null) throw new Exception("illegal format");
            if(nextToken != "[" && nextToken != "{")
            {
                nextToken = getToken();
                if (nextToken == ",") nextToken = getToken();
            }

            int bracketCount = 0;
            while (true)
            {
                if(nextToken == "[" || nextToken == "{"){
                    bracketCount++;
                }else if(nextToken == "]" || nextToken == "}")
                {
                    bracketCount--;
                }
                nextToken = getToken();
                if (bracketCount <= 0) break;
            }
            // nextToken = getToken();
            if (nextToken == ",") nextToken = getToken();
        }


        public JsonReader GetNextObjectReader()
        {
            JsonReader ret = new JsonReader(streamReader);
            childJson = ret;
            return ret;
        }

        char? nextChar = null;
        private string getToken()
        {
            if (nextChar == null)
            {
                if (streamReader.EndOfStream) return null;
                nextChar = (char)streamReader.Read();
            }

            // skip word separator
            while (!streamReader.EndOfStream &&
                (nextChar == '\t' || nextChar == '\r' || nextChar == '\n' || nextChar == ' ')
            )
            {
                nextChar = (char)streamReader.Read();
            }

            switch (nextChar)
            {
                case '\"':
                    return unEscapeString();
                case '{':
                case '}':
                case '[':
                case ']':
                case ':':
                case ',':
                    char token = (char)nextChar;
                    nextChar = null;
                    return token.ToString();
            }

            StringBuilder sb = new StringBuilder();
            while (true)
            {
                switch (nextChar)
                {
                    case '\t':
                    case '\r':
                    case '\n':
                    case ' ':
                    case '\"':
                    case '{':
                    case '}':
                    case '[':
                    case ']':
                    case ':':
                    case ',':
                        return sb.ToString();
                    default:
                        sb.Append(nextChar);
                        nextChar = null;

                        if(streamReader.EndOfStream) sb.ToString();
                        nextChar = (char)streamReader.Read();
                        break;
                }
            }
        }

        private string unEscapeString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(nextChar);

            if (streamReader.EndOfStream) throw new Exception("illegal format");
            nextChar = (char)streamReader.Read();

            //      string = quotation - mark * char quotation - mark
            //
            //      char = unescaped /
            //      escape(
            //      % x22 /          ; "    quotation mark  U+0022
            //      % x5C /          ; \    reverse solidus U+005C
            //      % x2F /          ; / solidus            U+002F
            //      % x62 /          ; b backspace          U+0008
            //      % x66 /          ; f form feed          U+000C
            //      % x6E /          ; n line feed          U+000A
            //      % x72 /          ; r carriage return    U+000D
            //      % x74 /          ; t tab                U+0009
            //      % x75 4HEXDIG )  ; uXXXX U+XXXX
            //
            //      escape = % x5C; \
            //
            //      quotation - mark = % x22; "
            //
            //      unescaped = % x20 - 21 / % x23 - 5B / % x5D - 10FFFF

            while (nextChar !='\"')
            {
                if(nextChar == '\\') // escape
                {
                    if (streamReader.EndOfStream) throw new Exception("illegal format");
                    nextChar = (char)streamReader.Read();
                    switch (nextChar)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            sb.Append(nextChar);
                            break;
                        case 'b':
                            sb.Append('\u0008');
                            break;
                        case 'f':
                            sb.Append('\u000c');
                            break;
                        case 'n':
                            sb.Append('\u000a');
                            break;
                        case 'r':
                            sb.Append('\u000d');
                            break;
                        case 't':
                            sb.Append('\u0009');
                            break;
                        case 'u':
                            if (streamReader.EndOfStream) throw new Exception("illegal format");
                            char d0 = (char)streamReader.Read();
                            if (streamReader.EndOfStream) throw new Exception("illegal format");
                            char d1 = (char)streamReader.Read();
                            if (streamReader.EndOfStream) throw new Exception("illegal format");
                            char d2 = (char)streamReader.Read();
                            if (streamReader.EndOfStream) throw new Exception("illegal format");
                            char d3 = (char)streamReader.Read();
                            int value;
                            if (!int.TryParse(d0.ToString() + d1 + d2 + nextChar, System.Globalization.NumberStyles.HexNumber, System.Threading.Thread.CurrentThread.CurrentCulture, out value)){
                                if (streamReader.EndOfStream) throw new Exception("illegal format");
                            }
                            sb.Append((char)value);
                            break;
                        default:
                            throw new Exception("illegal format");
                    }
                }
                else
                {
                    sb.Append(nextChar);
                }
                if (streamReader.EndOfStream) throw new Exception("illegal format");
                nextChar = (char)streamReader.Read();
            }
            sb.Append(nextChar);
            nextChar = null;

            return sb.ToString();
        }

    }
}
