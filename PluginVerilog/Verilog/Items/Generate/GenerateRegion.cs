using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items.Generate
{
    public class GenerateRegion
    {
        // generate_region ::=
        //      "generate" { generate_item } "endgenerate"

        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            if (word.Text != "generate") return false;
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            while (!word.Eof)
            {
                if (!GenerateItem.Parse(word, nameSpace)) break;
            }

            if (word.Text == "endgenerate")
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
            }
            else
            {
                word.AddError("endgenerate missing");
            }

            return true;
        }
    }
}
