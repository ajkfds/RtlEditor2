using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ajkControls.Json
{
    public class JsonWriter : IDisposable,IJson
    {
        public JsonWriter(System.IO.StreamWriter streamWriter)
        {
            this.streamWriter = streamWriter;
            this.streamWriter.Write("{\r\n");
            tabs++;
        }

        protected JsonWriter(System.IO.StreamWriter streamWriter, int tabs)
        {
            this.streamWriter = streamWriter;
            this.tabs = tabs;
            this.streamWriter.Write("{\r\n");
            this.tabs++;
        }

        public void Dispose()
        {
            streamWriter.Write("\r\n");
            tabs--;
            if (tabs < 0) throw new Exception("illegal operation");
            streamWriter.Write(new String('\t', tabs));
            streamWriter.Write("}");

            if (tabs == 0) streamWriter.Write("\r\n");
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

        protected System.IO.StreamWriter streamWriter;
        protected int tabs = 0;
        protected bool firstElement = true;
        

        public void writeKeyValue(string key, int value)
        {
            if (isChildOpened) throw new Exception("internal value is not closed");
            if (!firstElement)
            {
                streamWriter.Write(",\r\n");
            }
            firstElement = false;
            streamWriter.Write(new String('\t', tabs));
            streamWriter.Write(getEscapeString(key));
            streamWriter.Write(" : ");
            streamWriter.Write(value.ToString());
        }

        public void writeKeyValue(string key, string value)
        {
            if (isChildOpened) throw new Exception("internal value is not closed");
            if (!firstElement)
            {
                streamWriter.Write(",\r\n");
            }
            firstElement = false;
            streamWriter.Write(new String('\t', tabs));
            streamWriter.Write(getEscapeString(key));
            streamWriter.Write(" : ");
            streamWriter.Write(getEscapeString(value));

        }

        public void writeKeyValue(string key, double value)
        {
            if (isChildOpened) throw new Exception("internal value is not closed");
            if (!firstElement)
            {
                streamWriter.Write(",\r\n");
            }
            firstElement = false;
            streamWriter.Write(new String('\t', tabs));
            streamWriter.Write(getEscapeString(key));
            streamWriter.Write(" : ");
            streamWriter.Write(value.ToString());
        }

        public void writeKeyValue(string key, bool value)
        {
            if (isChildOpened) throw new Exception("internal value is not closed");
            if (!firstElement)
            {
                streamWriter.Write(",\r\n");
            }
            firstElement = false;
            streamWriter.Write(new String('\t', tabs));
            streamWriter.Write(getEscapeString(key));
            streamWriter.Write(" : ");
            streamWriter.Write(value.ToString());
        }

        public JsonWriter GetObjectWriter(string key)
        {
            if (isChildOpened) throw new Exception("internal value is not closed");
            if (!firstElement)
            {
                streamWriter.Write(",\r\n");
            }
            firstElement = false;
            streamWriter.Write(new String('\t', tabs));
            streamWriter.Write(getEscapeString(key));
            streamWriter.Write(" : ");
            var json = new JsonWriter(streamWriter, tabs);
            childJson = json;
            return json;
        }

        public JsonArrayWriter GetArrayWriter(string key)
        {
            if (isChildOpened) throw new Exception("internal value is not closed");
            if (!firstElement)
            {
                streamWriter.Write(",\r\n");
            }
            firstElement = false;
            streamWriter.Write(new String('\t', tabs));
            streamWriter.Write(getEscapeString(key));
            streamWriter.Write(" : ");
            var json = new JsonArrayWriter(streamWriter, tabs);
            childJson = json;
            return json;
        }

        public class JsonArrayWriter : IDisposable,IJson
        {
            internal JsonArrayWriter(System.IO.StreamWriter streamWriter, int tabs)
            {
                this.streamWriter = streamWriter;
                this.tabs = tabs;

                this.streamWriter.Write("[\r\n");
                this.tabs++;
            }

            public void Dispose()
            {
                streamWriter.Write("\r\n");
                tabs--;
                if (tabs < 0) throw new Exception("illegal operation");
                streamWriter.Write(new String('\t', tabs));
                streamWriter.Write("]");
                if (tabs == 0) streamWriter.Write("\r\n");
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

            protected System.IO.StreamWriter streamWriter;
            protected int tabs = 0;
            protected bool firstElement = true;

            public void writeValue(int value)
            {
                if (isChildOpened) throw new Exception("internal value is not closed");
                if (!firstElement)
                {
                    streamWriter.Write(",\r\n");
                }
                firstElement = false;
                streamWriter.Write(new String('\t', tabs));
                streamWriter.Write(value.ToString());
            }

            public void writeValue(string value)
            {
                if (isChildOpened) throw new Exception("internal value is not closed");
                if (!firstElement)
                {
                    streamWriter.Write(",\r\n");
                }
                firstElement = false;
                streamWriter.Write(new String('\t', tabs));
                streamWriter.Write(getEscapeString(value));

            }

            public void writeKeyValue(double value)
            {
                if (isChildOpened) throw new Exception("internal value is not closed");
                if (!firstElement)
                {
                    streamWriter.Write(",\r\n");
                }
                firstElement = false;
                streamWriter.Write(new String('\t', tabs));
                streamWriter.Write(value.ToString());
            }

            public JsonWriter GetObjectWriter()
            {
                if (isChildOpened) throw new Exception("internal value is not closed");
                if (!firstElement)
                {
                    streamWriter.Write(",\r\n");
                }
                firstElement = false;
                streamWriter.Write(new String('\t', tabs));
                var json = new JsonWriter(streamWriter, tabs);
                childJson = json;
                return json;
            }

            public JsonArrayWriter GetArrayWriter()
            {
                if (isChildOpened) throw new Exception("internal value is not closed");
                if (!firstElement)
                {
                    streamWriter.Write(",\r\n");
                }
                firstElement = false;
                streamWriter.Write(new String('\t', tabs));
                var json = new JsonArrayWriter(streamWriter, tabs);
                childJson = json;
                return json;
            }
        }

        protected static string getEscapeString(string text)
        {
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

            StringBuilder sb = new StringBuilder();
            sb.Append('\"');

            for (int i = 0; i < text.Length; i++)
            {
                switch (text[i])
                {
                    case '\u0022':
                        sb.Append("\\\"");
                        break;
                    case '\u005c':
                        sb.Append("\\\\");
                        break;
                    case '\u002f':
                        sb.Append("\\/");
                        break;
                    case '\u0008':
                        sb.Append("\\b");
                        break;
                    case '\u000c':
                        sb.Append("\\f");
                        break;
                    case '\u000a':
                        sb.Append("\\n");
                        break;
                    case '\u000d':
                        sb.Append("\\r");
                        break;
                    case '\u0009':
                        sb.Append("\\t");
                        break;
                    default:
                        if (text[i] >= 0x20)
                        {
                            sb.Append(text[i]);
                        }
                        else
                        {
                            sb.Append("\\U");
                            sb.Append(((int)text[i]).ToString("X4"));
                        }
                        break;
                }
            }

            sb.Append('\"');
            return sb.ToString();
        }
    }
}
