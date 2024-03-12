using pluginVerilog.Verilog.DataObjects;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Statements
{
    public class ForeverStatement : IStatement
    {
        protected ForeverStatement() { }

        public void DisposeSubReference()
        {
            Statement.DisposeSubReference();
        }

        public IStatement Statement;
        //A.6.8 Looping statements
        //function_loop_statement ::= forever function_statement          
        //                            | repeat(expression ) function_statement
        //                            | while (expression ) function_statement
        //                            | for (variable_assignment ;  expression ; variable_assignment ) function_statement
        //loop_statement   ::= forever statement
        //                            | repeat (expression ) statement
        //                            | while (expression ) statement
        //                            | for (variable_assignment ; expression ; variable_assignment ) statement

        /* # SystemVerilog
        loop_statement ::=    forever statement_or_null 
                            | repeat ( expression ) statement_or_null 
                            | while ( expression ) statement_or_null 
                            | for ( [ for_initialization ] ; [ expression ] ; [ for_step ] ) statement_or_null 
                            | do statement_or_null while ( expression ) ;
                            | foreach ( ps_or_hierarchical_array_identifier [ loop_variables ] ) statement
        */

        public static ForeverStatement ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            ForeverStatement foreverStatement = new ForeverStatement();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            foreverStatement.Statement = Statements.ParseCreateStatement(word, nameSpace);

            return foreverStatement;
        }
    }

    public class RepeatStatement : IStatement
    {
        protected RepeatStatement() { }

        public void DisposeSubReference()
        {
            Expression.DisposeSubRefrence(true);
            Statement.DisposeSubReference();
        }

        public Expressions.Expression Expression;
        public IStatement Statement;
        //A.6.8 Looping statements
        //function_loop_statement ::= forever function_statement          
        //                            | repeat(expression ) function_statement
        //                            | while (expression ) function_statement
        //                            | for (variable_assignment ;  expression ; variable_assignment ) function_statement
        //loop_statement   ::= forever statement
        //                            | repeat (expression ) statement
        //                            | while (expression ) statement
        //                            | for (variable_assignment ; expression ; variable_assignment ) statement
        public static RepeatStatement ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            RepeatStatement repeatStatement = new RepeatStatement();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            if (word.GetCharAt(0) != '(')
            {
                word.AddError("( expected");
                return null;
            }
            word.MoveNext();

            repeatStatement.Expression = Expressions.Expression.ParseCreate(word, nameSpace);

            if (word.GetCharAt(0) != ')')
            {
                word.AddError("( expected");
                return null;
            }
            word.MoveNext();

            repeatStatement.Statement = Statements.ParseCreateStatement(word, nameSpace);

            return repeatStatement;
        }
    }

    public class WhileStatememt : IStatement
    {
        protected WhileStatememt() { }

        public void DisposeSubReference()
        {
            Expression.DisposeSubRefrence(true);
            Statement.DisposeSubReference();
        }

        public Expressions.Expression Expression;
        public IStatement Statement;
        //A.6.8 Looping statements
        //function_loop_statement ::= forever function_statement          
        //                            | repeat(expression ) function_statement
        //                            | while (expression ) function_statement
        //                            | for (variable_assignment ;  expression ; variable_assignment ) function_statement
        //loop_statement   ::= forever statement
        //                            | repeat (expression ) statement
        //                            | while (expression ) statement
        //                            | for (variable_assignment ; expression ; variable_assignment ) statement
        public static WhileStatememt ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            WhileStatememt whileStatement = new WhileStatememt();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            if (word.GetCharAt(0) != '(')
            {
                word.AddError("( expected");
                return null;
            }
            word.MoveNext();

            whileStatement.Expression = Expressions.Expression.ParseCreate(word, nameSpace);

            if (word.GetCharAt(0) != ')')
            {
                word.AddError("( expected");
                return null;
            }
            word.MoveNext();

            whileStatement.Statement = Statements.ParseCreateStatement(word, nameSpace);

            return whileStatement;
        }
    }

    public class ForStatememt : NameSpace, IStatement
    {
        protected ForStatememt(BuildingBlocks.BuildingBlock buildingBlock,NameSpace nameSpace) : base(buildingBlock, nameSpace)
        {

        }

        public void DisposeSubReference()
        {
            Expression.DisposeSubRefrence(true);
            Statement.DisposeSubReference();
        }


        public IStatement Statement;

        public DataObjects.VariableAssignment VariableAssignment;
        public Expressions.Expression Expression;
        public DataObjects.VariableAssignment VariableUpdate;

        //A.6.8 Looping statements
        //function_loop_statement ::= forever function_statement          
        //                            | repeat(expression ) function_statement
        //                            | while (expression ) function_statement
        //                            | for (variable_assignment ;  expression ; variable_assignment ) function_statement
        //loop_statement   ::= forever statement
        //                            | repeat (expression ) statement
        //                            | while (expression ) statement
        //                            | for (variable_assignment ; expression ; variable_assignment ) statement

        // ## SystemVerilog2017
        // for ( [ for_initialization ] ; [ expression ] ; [ for_step ] ) statement_or_null

        // for_initialization           ::=   list_of_variable_assignments
        //                                  | for_variable_declaration { , for_variable_declaration }

        // for_variable_declaration     ::= [ "var" ] data_type variable_identifier = expression { , variable_identifier = expression }
        // for_step                     ::=   for_step_assignment { , for_step_assignment }
        // for_step_assignment          ::=   operator_assignment
        //                                  | inc_or_dec_expression 
        //                                  | function_subroutine_call
        // loop_variables               ::=   [index_variable_identifier] { , [index_variable_identifier] }

        // operator_assignment          ::= variable_lvalue assignment_operator expression
        // assignment_operator          ::= = | += | -= | *= | /= | %= | &= | |= | ^= | <<= | >>= | <<<= | >>>=

        public static ForStatememt ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            ForStatememt forStatement = new ForStatememt(nameSpace.BuildingBlock,nameSpace);

            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            

            if(word.Text == "(")
            {
                word.MoveNext();
            }
            else
            {
                word.AddError("( expected");
                return null;
            }

            Verilog.DataObjects.Variables.Variable.ParseDeclaration(word, forStatement);

            //if(word.Text == ";")
            //{
            //    word.MoveNext();
            //}
            //else
            //{
            //    word.AddError("; expected");
            //    return null;
            //}

            forStatement.Expression = Expressions.Expression.ParseCreate(word, forStatement);

            if (word.Text == ";")
            {
                word.MoveNext();
            }
            else
            {
                word.AddError("; expected");
                return null;
            }

            DataObjects.VariableAssignment assign = Verilog.DataObjects.VariableAssignment.ParseCreate(word, forStatement);
            if(assign == null)
            {
                forStatement.Expression = Expressions.Expression.ParseCreate(word, forStatement);
            }

            if (word.GetCharAt(0) != ')')
            {
                word.AddError("( expected");
                return null;
            }
            word.MoveNext();


            forStatement.Statement = Statements.ParseCreateStatement(word, forStatement);
            return forStatement;
        }


    }

}
