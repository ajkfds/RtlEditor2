using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Statements
{
    public class DeassignStatement : IStatement
    {
        /*
        procedural_continuous_assignments ::=
            assign variable_assignment 
            | deassign variable_lvalue 
            | force variable_assignment 
            | force net_assignment 
            | release variable_lvalue 
            | release net_lvalue
        */
        public Expressions.Expression LValue;

        public void DisposeSubReference()
        {
            LValue.DisposeSubRefrence(true);
        }

        protected DeassignStatement() { }
        public static DeassignStatement ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            DeassignStatement ret = new DeassignStatement();

            if (word.Text != "deassign") System.Diagnostics.Debugger.Break();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            ret.LValue = Expressions.Expression.ParseCreateVariableLValue(word, nameSpace);

            if (word.Text != ";")
            {
                word.AddError("; required");
            }
            else
            {
                word.MoveNext();
            }

            return ret;
        }
    }
}
