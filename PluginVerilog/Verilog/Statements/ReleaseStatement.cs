using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Statements
{
    public class ReleaseStatement : IStatement
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
        public Expressions.Expression Value;

        public void DisposeSubReference()
        {
            Value.DisposeSubRefrence(true);
        }
        protected ReleaseStatement() { }
        public static ReleaseStatement ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            ReleaseStatement ret = new ReleaseStatement();

            if (word.Text != "release") System.Diagnostics.Debugger.Break();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            ret.Value = Expressions.Expression.ParseCreate(word, nameSpace);

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
