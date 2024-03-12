using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.CodeEditor
{
    public class CodeDocument : codeEditor.CodeEditor.CodeDocument
    {
        public CodeDocument(Data.IVerilogRelatedFile file) :base(file as codeEditor.Data.TextFile) { }
        public CodeDocument(string text) : base(null,text)
        {

        }

        public Data.IVerilogRelatedFile VerilogFile
        {
            get { return TextFile as Data.IVerilogRelatedFile; }
        }

        // get word boundery for editor word selection

        public override void GetWord(int index, out int headIndex, out int length)
        {
            lock (this)
            {
                int line = GetLineAt(index);
                headIndex = GetLineStartIndex(line);
    //            headIndex = index;
                //length = 0;
                //char ch = GetCharAt(index);
                //if (ch == ' ' || ch == '\r' || ch == '\n' || ch == '\t') return;

                //while (headIndex >= 0)
                //{
                //    ch = GetCharAt(headIndex);
                //    if (ch == ' ' || ch == '\r' || ch == '\n' || ch == '\t')
                //    {
                //        break;
                //    }
                //    headIndex--;
                //}
                //headIndex++;
                //if (index < headIndex) headIndex = index;

                int nextIndex;
                Verilog.WordPointer.WordTypeEnum wordType;
                string sectionName = "";
                Verilog.WordPointer.FetchNext(this, ref headIndex, out length, out nextIndex, out wordType,ref sectionName);

                while(nextIndex <= index && index < Length)
                {
                    headIndex = nextIndex;
                    Verilog.WordPointer.FetchNext(this, ref headIndex, out length, out nextIndex, out wordType,ref sectionName);
                }
            }
        }

        public List<string> GetHierWords(int index,out bool endWithDot)
        {
            lock (this)
            {

                List<string> ret = new List<string>();
                int headIndex = GetLineStartIndex(GetLineAt(index));
                int length;
                int nextIndex = headIndex;
                Verilog.WordPointer.WordTypeEnum wordType;
                endWithDot = true;

                // return blank if on space char
                if (index != 0)
                {
                    char ch = GetCharAt(index - 1);
                    if (ch == ' ' || ch == '\t')
                    {
                        endWithDot = false;
                        return new List<string>();
                    }
                }

                string sectioName = "";
                // get words on the index line until index
                while (headIndex < Length)
                {
                    Verilog.WordPointer.FetchNext(this, ref headIndex, out length, out nextIndex, out wordType,ref sectioName);
                    if (length == 0) break;
                    if (headIndex >= index) break;
                    ret.Add(CreateString(headIndex, length));
                    headIndex = nextIndex;
                }

                // search wors from end
                int i= ret.Count - 1;
                if (i >= 0 && ret[i] != ".")
                {
                    endWithDot = false;
                    i--; // skip last non . word
                }

                while (i>=0)
                {
                    if (ret[i] != ".") break; // end if not .
                    ret.RemoveAt(i);
                    i--;

    //                if (i == 0) break;
                    i--;
                }

                for(int j = 0; j <= i; j++) // remove before heir description
                {
                    ret.RemoveAt(0);
                }

                return ret;
            }
        }

        public string GetIndentString(int index)
        {
            lock (this)
            {
                StringBuilder sb = new StringBuilder();
                int line = GetLineAt(index);
                int headIndex = GetLineStartIndex(GetLineAt(index));
                int lineLength = GetLineLength(line);

                int i = headIndex;
                while( i < headIndex + lineLength)
                {
                    if (GetCharAt(i) != '\t' && GetCharAt(i) != ' ') break;
                    sb.Append(GetCharAt(i));
                    i++;
                }
                return sb.ToString();
            }
        }

    }
}
