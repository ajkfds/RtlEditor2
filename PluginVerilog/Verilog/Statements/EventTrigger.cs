using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Statements
{
    public class EventTrigger : IStatement
    {
        protected EventTrigger() { }

        public void DisposeSubReference()
        {
        }

        public string Identifier { get; protected set; }

        // disable_statement::= (From Annex A - A.6.5)  
        //                    disable hierarchical_task_identifier; 
        //                    | disable hierarchical_block_identifier;
        public static EventTrigger ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            EventTrigger eventTrigger = new EventTrigger();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            Expressions.Expression ex = Expressions.Expression.ParseCreate(word, nameSpace);

            if(ex is Verilog.Expressions.VariableReference)
            {
                var valRef = ex as Expressions.VariableReference;
                if (valRef.Variable is DataObjects.Variables.Event)
                {
                    // event expression
                }
                else
                {
                    word.AddError("event expected");
                    word.SkipToKeyword(";");
                }
            }
            else
            {
                word.AddError("event expected");
                word.SkipToKeyword(";");
            }

            if (word.Text != ";")
            {
                word.AddError("; required");
                return null;
            }
            word.MoveNext();

            return eventTrigger;
        }
    }

}
