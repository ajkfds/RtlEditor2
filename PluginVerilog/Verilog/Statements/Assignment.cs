using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Statements
{

    public class NonBlockingAssignment : IStatement
    {
        protected NonBlockingAssignment() { }

        public Expressions.Expression LValue { get; protected set; }
        public Expressions.Expression Expression { get; protected set; }

        /*
        A.6.2 Procedural blocks and assignments
        initial_construct   ::= initial statement
        always_construct    ::= always statement
        blocking_assignment ::= variable_lvalue = [ delay_or_event_control ] expression
        nonblocking_assignment ::= variable_lvalue <= [ delay_or_event_control ] expression
        procedural_continuous_assignments   ::= assign variable_assignment
                                                | deassign variable_lvalue
                                                | force variable_assignment
                                                | force net_assignment
                                                | release variable_lvalue
                                                | release net_lvalue
         */
        public delegate void NonBlockingAssignedAction(WordScanner word, NameSpace nameSpace, NonBlockingAssignment blockingAssignment);
        public static NonBlockingAssignedAction Assigned;

        public void DisposeSubReference()
        {
            LValue.DisposeSubRefrence(true);
            Expression.DisposeSubRefrence(true);
        }
        public static NonBlockingAssignment ParseCreate(WordScanner word,NameSpace nameSpace,Expressions.Expression lExpression)
        {
            if(word.Text != "<=")
            {
                System.Diagnostics.Debugger.Break();
                return null;
            }
            word.MoveNext();    // <=

            if (word.GetCharAt(0) == '#')
            {
                DelayControl delayControl = DelayControl.ParseCreate(word, nameSpace);
            }
            else if (word.GetCharAt(0) == '@')
            {
                EventControl eventControl = EventControl.ParseCreate(word, nameSpace);
            }

            Expressions.Expression expression = Expressions.Expression.ParseCreate(word, nameSpace);
            if(expression == null)
            {
                word.SkipToKeyword(";");
                word.AddError("illegal non blocking assignment");
                return null;
            }

            if (!word.Prototype) {
                if(
                    lExpression != null && 
                    lExpression.BitWidth != null && 
                    expression.BitWidth != null &&
                    lExpression.BitWidth != expression.BitWidth
                    )
                {
                    expression.Reference.CreateReferenceFrom(lExpression.Reference).AddWarning("bitwidth mismatch " + lExpression.BitWidth + " vs " + expression.BitWidth);
                }
            }

            NonBlockingAssignment assignment = new NonBlockingAssignment();
            assignment.LValue = lExpression;
            assignment.Expression = expression;
            if (Assigned != null) Assigned(word, nameSpace, assignment);
            return assignment;
        }
    }
    public class BlockingAssignment : IStatement
    {
        protected BlockingAssignment() { }

        public void DisposeSubReference()
        {
            LValue.DisposeSubRefrence(true);
            Expression.DisposeSubRefrence(true);
        }
        public Expressions.Expression LValue { get; protected set; }
        public Expressions.Expression Expression { get; protected set; }
        /*
        A.6.2 Procedural blocks and assignments
        initial_construct   ::= initial statement
        always_construct    ::= always statement
        blocking_assignment ::= variable_lvalue = [ delay_or_event_control ] expression
        nonblocking_assignment ::= variable_lvalue <= [ delay_or_event_control ] expression
        procedural_continuous_assignments   ::= assign variable_assignment
                                                | deassign variable_lvalue
                                                | force variable_assignment
                                                | force net_assignment
                                                | release variable_lvalue
                                                | release net_lvalue
         */

        public delegate void BlockingAssignedAction(WordScanner word, NameSpace nameSpace, BlockingAssignment blockingAssignment);
        public static BlockingAssignedAction Assigned;
        public static BlockingAssignment ParseCreate(WordScanner word, NameSpace nameSpace, Expressions.Expression lExpression)
        {
            if (word.Text != "=")
            {
                System.Diagnostics.Debugger.Break();
                return null;
            }
            word.MoveNext();    // <=

            if(word.GetCharAt(0) == '#')
            {
                DelayControl delayControl = DelayControl.ParseCreate(word, nameSpace);
            }else if(word.GetCharAt(0) == '@')
            {
                EventControl eventControl = EventControl.ParseCreate(word, nameSpace);
            }

            // delay or event control

            Expressions.Expression expression = Expressions.Expression.ParseCreate(word, nameSpace);
            if (expression == null)
            {
                word.SkipToKeyword(";");
                word.AddError("illegal non blocking assignment");
                return null;
            }

            if (!word.Prototype)
            {
                if (
                    lExpression != null && 
                    lExpression.BitWidth != null && 
                    expression.BitWidth != null &&
                    lExpression.BitWidth != expression.BitWidth
                    )
                {
                    expression.Reference.CreateReferenceFrom(lExpression.Reference).AddWarning("bitwidth mismatch " + lExpression.BitWidth + " vs " + expression.BitWidth);
                }
            }

            BlockingAssignment assignment = new BlockingAssignment();
            assignment.LValue = lExpression;
            assignment.Expression = expression;
            if (Assigned != null) Assigned(word, nameSpace, assignment);
            return assignment;
        }
    }
}
