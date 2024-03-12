using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items.Generate
{
    public class GenvarInitialization
    {
        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            //    genvar_assignment::= genvar_identifier = constant_expression
            // genvar_initialization ::= 
            //      ["genvar"] genvar_identifier = constant_expression

            Expressions.VariableReference genvar;
            if (word.Text == "genvar")
            {
                if (!word.SystemVerilog) word.AddSystemVerilogError();
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();

                if (word.Eof) return true;
                DataObjects.Variables.Genvar gvar = new DataObjects.Variables.Genvar(word.Text);
                gvar.DefinedReference = word.GetReference();
                if (nameSpace.DataObjects.ContainsKey(gvar.Name))
                {
                    word.AddError("iillegal genvar name");
                }
                else
                {
                    nameSpace.DataObjects.Add(gvar.Name, gvar);
                }
                word.MoveNext();
                genvar = Expressions.VariableReference.Create(gvar, nameSpace);
                if (genvar == null) return true;
            }
            else
            {
                genvar = Expressions.VariableReference.ParseCreate(word, nameSpace, true);
                if (genvar == null) return false;
            }

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
