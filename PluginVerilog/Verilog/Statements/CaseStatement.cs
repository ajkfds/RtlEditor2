using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Statements
{
    public class CaseStatement : IStatement
    {
        protected CaseStatement() { }
        public Expressions.Expression Expression;
        public List<CaseItem> CaseItems = new List<CaseItem>();

        public void DisposeSubReference()
        {
            Expression.DisposeSubRefrence(true);
            foreach(CaseItem caseItem in CaseItems)
            {
                caseItem.DisposeSubRefrence();
            }
        }
        public static CaseStatement ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            /*
            case_statement ::=  case ( expression ) case_item { case_item } endcase          
                                | casez ( expression ) case_item { case_item } endcase
                                | casex ( expression ) case_item { case_item } endcase
            case_item ::=       expression { , expression } : statement_or_null 
                                | default [ : ] statement_or_null
            function_case_statement ::= case ( expression ) function_case_item { function_case_item } endcase      
                                        | casez ( expression ) function_case_item { function_case_item } endcase   
                                        | casex ( expression ) function_case_item { function_case_item } endcase
            function_case_item ::=      expression { , expression } : function_statement_or_null
                                        | default [ : ] function_statement_or_null  
            */
            switch (word.Text)
            {
                case "case":
                    break;
                case "casez":
                    break;
                case "casex":
                    break;
                default:
                    word.AddError("illegal case statement");
                    return null;
            }
            CaseStatement caseStatement = new CaseStatement();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            if(word.GetCharAt(0) == '(')
            {
                word.MoveNext();
            }
            else
            {
                word.AddError("( expected");
            }
            caseStatement.Expression = Expressions.Expression.ParseCreate(word, nameSpace);
            if (word.GetCharAt(0) == ')')
            {
                word.MoveNext();
            }
            else
            {
                word.AddError(") expected");
            }


            while (!word.Eof && word.Text != "endcase" && word.Text != "endmodule" && word.Text != "endfunction")
            {
                CaseItem caseItem = CaseItem.ParseCreate(word, nameSpace);
                if (caseItem == null)
                {
                    break;
                }
                caseStatement.CaseItems.Add(caseItem);
            }

            if (word.Text != "endcase")
            {
                word.AddError("illegal case statement");
                return null;
            }
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            return caseStatement;
        }

        public class CaseItem
        {
            protected CaseItem() { }
            public List<Expressions.Expression> Expressions = new List<Expressions.Expression>();
            public IStatement Statement;

            public void DisposeSubRefrence()
            {
                foreach(pluginVerilog.Verilog.Expressions.Expression expression in Expressions)
                {
                    expression.DisposeSubRefrence(true);
                }
                Statement.DisposeSubReference();
            }
            public static CaseItem ParseCreate(WordScanner word,NameSpace nameSpace)
            {
                //            case_item ::=       expression { , expression } : statement_or_null 
                //                                | default[ : ] statement_or_null
                CaseItem caseItem = new CaseItem();

                if (word.Text == "default")
                {
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    if (word.GetCharAt(0) == ':')
                    {
                        word.MoveNext();
                    }
                    caseItem.Statement = Statements.ParseCreateStatementOrNull(word, nameSpace);
                    return caseItem;
                }

                Expressions.Expression expression = Verilog.Expressions.Expression.ParseCreate(word, nameSpace);
                if(expression == null)
                {
                    word.AddError("illegal expression item");
                    return null;
                }
                caseItem.Expressions.Add(expression);

                while(!word.Eof && word.GetCharAt(0) == ',')
                {
                    word.MoveNext();
                    expression = Verilog.Expressions.Expression.ParseCreate(word, nameSpace);
                    if (expression == null)
                    {
                        word.AddError("illegal expression item");
                        return null;
                    }
                    caseItem.Expressions.Add(expression);
                }

                if (word.GetCharAt(0) == ':')
                {
                    word.MoveNext();
                }else{
                    word.AddError(": exptected");
                    return null;
                }

                caseItem.Statement = Statements.ParseCreateStatementOrNull(word, nameSpace);
                return caseItem;
            }
        }
    }
}
