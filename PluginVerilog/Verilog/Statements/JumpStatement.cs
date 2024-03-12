using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Statements
{
    // jump_statement   ::= "return" [expression] ;
    //                      | "break" ;
    //                      | "continue" ;
    public class ReturnStatement : IStatement
    {
        protected ReturnStatement() { }
        public void DisposeSubReference()
        {
            Expression.DisposeSubRefrence(true);
        }

        public Expressions.Expression Expression;

        public static ReturnStatement ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            if (word.Text != "return") System.Diagnostics.Debugger.Break();
            ReturnStatement jumpStatement = new ReturnStatement();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            if (word.Text == ";")
            {
                return jumpStatement;
            }

            jumpStatement.Expression = Expressions.Expression.ParseCreate(word, nameSpace);

            if (word.Text == ";")
            {
                word.MoveNext();
            }
            else
            {
                word.AddError("; required");
            }
            return jumpStatement;
        }
    }

    public class BreakStatement : IStatement
    {
        protected BreakStatement() { }
        public void DisposeSubReference()
        {
        }

        public static BreakStatement ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            if (word.Text != "break") System.Diagnostics.Debugger.Break();
            BreakStatement jumpStatement = new BreakStatement();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            if (word.Text == ";")
            {
                word.MoveNext();
            }
            else
            {
                word.AddError("; required");
            }
            return jumpStatement;
        }

    }

    public class ContinueStatement : IStatement
    {
        protected ContinueStatement() { }
        public void DisposeSubReference()
        {
        }

        public static ContinueStatement ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            if (word.Text != "continue") System.Diagnostics.Debugger.Break();
            ContinueStatement jumpStatement = new ContinueStatement();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            if(word.Text == ";")
            {
                word.MoveNext();
            }
            else
            {
                word.AddError("; required");
            }

            return jumpStatement;
        }

    }

}
