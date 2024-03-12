using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects
{
    public class VariableAssignment
    {
        protected VariableAssignment() { }

        public void DisposeSubReference()
        {
            Expression.DisposeSubRefrence(true);
            NetLValue.DisposeSubRefrence(true);
        }
        public Expressions.Expression NetLValue { get; protected set; }
        public Expressions.Expression Expression { get; protected set; }
 
        public static VariableAssignment ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            // variable_assignment  ::= variable_lvalue = expression
            // variable_lvalue      ::= hierarchical_variable_identifier
            //                          | hierarchical_variable_identifier[expression] { [expression] }
            //                          | hierarchical_variable_identifier[expression] { [expression] } [range_expression]    
            //                          | hierarchical_variable_identifier[range_expression]   
            //                          | variable_concatenation

            VariableAssignment variableAssign = new VariableAssignment();
            variableAssign.NetLValue = Expressions.Expression.ParseCreate(word, nameSpace);

            if (variableAssign.NetLValue == null)
            {
                return null;
            }
            if (variableAssign.NetLValue.IncrementDecrement) return variableAssign;
            if (word.Text != "=")
            {
                word.AddError("= expected.");
                return null;
            }
            WordScanner equalPointer = word.Clone();
            word.MoveNext();

            variableAssign.Expression = Expressions.Expression.ParseCreate(word, nameSpace);
            if (variableAssign.Expression == null) return null;

            if (!word.Prototype)
            {
                if (
                    variableAssign.NetLValue != null &&
                    variableAssign.NetLValue.BitWidth != null &&
                    variableAssign.Expression.BitWidth != null &&
                    variableAssign.NetLValue.BitWidth != variableAssign.Expression.BitWidth
                    )
                {
                    variableAssign.Expression.Reference.CreateReferenceFrom(variableAssign.NetLValue.Reference)
                        .AddWarning("bitwidth mismatch " + variableAssign.NetLValue.BitWidth + " vs " + variableAssign.Expression.BitWidth);
                }
            }

            return variableAssign;
        }

    }
}
