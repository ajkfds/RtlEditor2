using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Statements
{
    public class ForceStatement : IStatement
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
        public Expressions.Expression Value;

        protected ForceStatement() { }
        public void DisposeSubReference()
        {
            LValue.DisposeSubRefrence(true);
            Value.DisposeSubRefrence(true);
        }
        public static ForceStatement ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            ForceStatement ret = new ForceStatement();

            if (word.Text != "force") System.Diagnostics.Debugger.Break();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            ret.LValue = Expressions.Expression.ParseCreateVariableLValue(word, nameSpace);
            if(ret.LValue == null)
            {
                word.SkipToKeyword(";");
                if (word.Text == ";") word.MoveNext();
                return null;
            }

            if (word.Text != "=")
            {
                word.SkipToKeyword(";");
                if (word.Text == ";") word.MoveNext();
                return null;
            }
            word.MoveNext();

            ret.Value = Expressions.Expression.ParseCreate(word, nameSpace);

            if (word.Text != ";")
            {
                word.AddError("; required");
                word.SkipToKeyword(";");
                if (word.Text == ";") word.MoveNext();
            }
            else
            {
                word.MoveNext();
            }

            return ret;
        }
    }
}
