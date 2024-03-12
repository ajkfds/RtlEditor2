using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Statements
{
    public class ProceduralContinuousAssignment : IStatement
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

        protected ProceduralContinuousAssignment() { }

        public void DisposeSubReference()
        {
            LValue.DisposeSubRefrence(true);
            Value.DisposeSubRefrence(true);
        }

        public static ProceduralContinuousAssignment ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            ProceduralContinuousAssignment ret = new ProceduralContinuousAssignment();

            if (word.Text != "assign") System.Diagnostics.Debugger.Break();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            ret.LValue = Expressions.Expression.ParseCreateVariableLValue(word, nameSpace);

            if (word.Text != "=") return null;
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
