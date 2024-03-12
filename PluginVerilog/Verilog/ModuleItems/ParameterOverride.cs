using pluginVerilog.Verilog.BuildingBlocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.ModuleItems
{
    public class ParameterOverride
    {
        protected ParameterOverride() { }


        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            //  always_construct::= always statement
            System.Diagnostics.Debug.Assert(word.Text == "defparam");
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            while (!word.Eof)
            {
                ParameterOverride defparam = new ParameterOverride();
                Expressions.Expression param = Expressions.Expression.ParseCreate(word, nameSpace);
                if (word.Text != "=")
                {
                    word.AddError("= required");
                    return true;
                }
                word.MoveNext();
                Expressions.Expression expression = Expressions.Expression.ParseCreate(word, nameSpace);
                if(param != null && expression != null)
                {

                }
                if (word.Text != ",") break;
                word.MoveNext();
            }

            if(word.Text!= ";")
            {
                word.AddError("; required");
            }
            return true;
        }
    }
}
