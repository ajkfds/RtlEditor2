using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items.Generate
{
    public class GenvarIteration
    {

        //genvar_iteration::= 
        //    genvar_identifier assignment_operator genvar_expression 
        //    | inc_or_dec_operator genvar_identifier 
        //    | genvar_identifier inc_or_dec_operator

        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            //    genvar_assignment::= genvar_identifier = constant_expression
            Expressions.VariableReference genvar = Expressions.VariableReference.ParseCreate(word, nameSpace, true);
            if (genvar == null) return false;
            if (!(genvar.Variable is DataObjects.Variables.Genvar))
            {
                word.AddError("should be genvar");
            }
            if (word.Text != "=")
            {
                word.AddError("( expected");
                return true;
            }
            word.MoveNext();
            Expressions.Expression constant = Expressions.Expression.ParseCreate(word, nameSpace);
            if (constant == null) return false;
            if (!constant.Constant)
            {
                word.AddError("should be constant");
            }
            return true;
        }

    }
}