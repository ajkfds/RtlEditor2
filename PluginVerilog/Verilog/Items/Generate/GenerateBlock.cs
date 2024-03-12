using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items.Generate
{
    public class GenerateBlock
    {

        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            if (word.Text != "begin") return false;

            // generate_block ::= begin[ : generate_block_identifier]  { generate_item } end
            word.Color(CodeDrawStyle.ColorType.Keyword);
            WordReference beginRef = word.GetReference();
            word.MoveNext();

            if (word.Text != ":")
            {
                beginRef.AddError(": required");
            }
            else
            {
                word.MoveNext();

                if (!General.IsIdentifier(word.Text))
                {
                    word.AddError("identifier required");
                    return true;
                }
                word.Color(CodeDrawStyle.ColorType.Identifier);
                word.MoveNext();
            }

            if (word.Active)
            {
                while (!word.Eof)
                {
                    if (!GenerateItem.Parse(word, nameSpace)) break;
                }
            }
            else
            {
                int beginCount = 0;
                while (!word.Eof && word.Text != "endgenerate")
                {
                    if (word.Text == "begin")
                    {
                        beginCount++;
                    }
                    else if (word.Text == "end")
                    {
                        if (beginCount == 0)
                        {
                            break;
                        }
                        beginCount--;
                    }
                    else
                    {
                        word.Color(CodeDrawStyle.ColorType.Inactivated);
                    }
                    word.MoveNext();
                }
            }

            if (word.Text == "end")
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
                return true;
            }
            else
            {
                word.AddError("end required");
            }
            return true;
        }

    }
}
