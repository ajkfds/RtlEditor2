using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Statements
{
    public static class Statements
    {
        /*
        A.6.4 Statements
        statement   ::= { attribute_instance } blocking_assignment ;
                        | { attribute_instance } case_statement
                        | { attribute_instance } conditional_statement
                        | { attribute_instance } disable_statement
                        | { attribute_instance } event_trigger
                        | { attribute_instance } loop_statement
                        | { attribute_instance } nonblocking_assignment ;
                        | { attribute_instance } par_block
                        | { attribute_instance } procedural_continuous_assignments ;
                        | { attribute_instance } procedural_timing_control_statement
                        | { attribute_instance } seq_block
                        | { attribute_instance } system_task_enable
                        | { attribute_instance } task_enable
                        | { attribute_instance } wait_statement

        statement_or_null   ::= statement
                                | { attribute_instance } ;

        function_statement  ::= { attribute_instance } function_blocking_assignment ;
                                | { attribute_instance } function_case_statement
                                | { attribute_instance } function_conditional_statement
                                | { attribute_instance } function_loop_statement
                                | { attribute_instance } function_seq_block
                                | { attribute_instance } disable_statement
                                | { attribute_instance } system_task_enable  
        */
        public static IStatement ParseCreateStatement(WordScanner word, NameSpace nameSpace)
        {
            /*
            A.6.4 Statements
            statement   ::= 
                            | { attribute_instance } conditional_statement

                            /// done
                            | { attribute_instance } blocking_assignment ;
                            | { attribute_instance } nonblocking_assignment ;
                            | { attribute_instance } procedural_timing_control_statement
                            | { attribute_instance } loop_statement
                            | { attribute_instance } case_statement
                            | { attribute_instance } disable_statement
                            | { attribute_instance } seq_block
                            | { attribute_instance } par_block
                            | { attribute_instance } procedural_continuous_assignments ;
                            | { attribute_instance } system_task_enable
                            | { attribute_instance } task_enable

                            | { attribute_instance } event_trigger
                            | { attribute_instance } wait_statement
            procedural_timing_control_statement ::= delay_or_event_control statement_or_null 
            */
            /* # SystemVerilog
            statement_item ::=    blocking_assignment ;
	                            | nonblocking_assignment ;
	                            | procedural_continuous_assignment ;
	                            | case_statement 
	                            | conditional_statement 
	                            | inc_or_dec_expression ;
	                            | subroutine_call_statement 
	                            | disable_statement 
	                            | event_trigger 
	                            | loop_statement 
	                            | jump_statement 
	                            | par_block 
	                            | procedural_timing_control_statement 
	                            | seq_block 
	                            | wait_statement 
	                            | procedural_assertion_statement 
	                            | clocking_drive ;
	                            | randsequence_statement 
	                            | randcase_statement 
	                            | expect_property_statement             
            */

            // inc_or_dec_expression ;
            // wait_statement 
            // procedural_assertion_statement 
            // clocking_drive ;
            // randsequence_statement 
            // randcase_statement 
            // expect_property_statement 

            switch (word.Text)
            {
                case "(*":
                    Attribute attribute = Attribute.ParseCreate(word);
                    return Statements.ParseCreateStatement(word, nameSpace);

                // conditional_statement 
                case "if":
                    return ConditionalStatement.ParseCreate(word, nameSpace);

                // procedural_timing_control_statement 
                case "#":
                case "@":
                    return ProceduralTimingControlStatement.ParseCreate(word, nameSpace);

                // seq_block 
                case "begin":
                    return SequentialBlock.ParseCreate(word, nameSpace);

                // par_block 
                case "fork":
                    return ParallelBlock.ParseCreate(word, nameSpace);

                // loop_statement 
                case "forever":
                    return ForeverStatement.ParseCreate(word, nameSpace);
                case "repeat":
                    return RepeatStatement.ParseCreate(word, nameSpace);
                case "while":
                    return WhileStatememt.ParseCreate(word, nameSpace);
                case "for":
                    return ForStatememt.ParseCreate(word, nameSpace);

                // case_statement 
                case "case":
                case "casex":
                case "casez":
                    return CaseStatement.ParseCreate(word, nameSpace);

                // disable_statement 
                case "disable":
                    return DisableStatement.ParseCreate(word,nameSpace);

                case "force":
                    return ForceStatement.ParseCreate(word,nameSpace);
                case "release":
                    return ReleaseStatement.ParseCreate(word, nameSpace);

                // jump_statement 
                case "return":
                    return ReturnStatement.ParseCreate(word, nameSpace);
                case "break":
                    return BreakStatement.ParseCreate(word, nameSpace);
                case "continue":
                    return ContinueStatement.ParseCreate(word, nameSpace);

                // procedural_continuous_assignment ;
                case "assign":
                    return ProceduralContinuousAssignment.ParseCreate(word, nameSpace);

                case "deassign":
                    return DeassignStatement.ParseCreate(word, nameSpace);
                // event_trigger 
                case "->":
                    return EventTrigger.ParseCreate(word, nameSpace);
                case ";":
                    word.AddError("illegal module item");
                    word.MoveNext();
                    return null;
                default:

                    // subroutine_call_statement 
                    string nextText = word.NextText;
                    if (nextText == "(" || nextText == ";")
                    {
                        if (word.Text.StartsWith("$"))
                        {
                            if (!word.RootParsedDocument.ProjectProperty.SystemTaskParsers.ContainsKey(word.Text))
                            {
                                word.AddError("unsupported system task");
                                return SystemTask.SkipArguments.ParseCreate(word, nameSpace);
                            }else if(word.RootParsedDocument.ProjectProperty.SystemTaskParsers[word.Text] != null)
                            {
                                return word.RootParsedDocument.ProjectProperty.SystemTaskParsers[word.Text](word, nameSpace);
                            }
                            else
                            {
                                return SystemTask.SystemTask.ParseCreate(word, nameSpace);
                            }
                        }
                        else if (General.IsIdentifier(word.Text)){
                            return TaskEnable.ParseCreate(word, nameSpace,nameSpace);
                        }
                    }

                    Expressions.Expression expression = Expressions.Expression.ParseCreateVariableLValue(word, nameSpace);
                    if(expression != null && expression is Expressions.TaskReference)// Expressions.TaskReference)
                    {
                        Expressions.TaskReference taskReference = expression as Expressions.TaskReference;
                        return TaskEnable.ParseCreate(taskReference, word, nameSpace);
                    }

                    IStatement statement;
                    if(expression == null)
                    {
                        word.MoveNext();
                    }
                    switch (word.Text)
                    {
                        // blocking_assignment ;
                        case "=":
                            statement = BlockingAssignment.ParseCreate(word, nameSpace, expression);
                            break;
                        // nonblocking_assignment ;
                        case "<=":
                            statement = NonBlockingAssignment.ParseCreate(word, nameSpace, expression);
                            break;
                        default:
                            word.AddError("illegal module item");
                            return null;
                    }
                    if(word.GetCharAt(0) != ';')
                    {
                        word.AddError("; expected");
                    }
                    else
                    {
                        word.MoveNext();
                    }
                    return statement;
            }
        }

        private static NameSpace getSpace(string identifier, NameSpace nameSpace)
        {
            if (nameSpace.NameSpaces.ContainsKey(identifier))
            {
                return nameSpace.NameSpaces[identifier];
            }
            if (nameSpace.Parent == null) return null;

            return getSpace(identifier, nameSpace.Parent);
        }

        public static IStatement ParseCreateStatementOrNull(WordScanner word, NameSpace nameSpace)
        {
            if(word.GetCharAt(0) == ';')
            {
                word.MoveNext();
                return null;
            }
            return ParseCreateStatement(word, nameSpace);
        }


        public static IStatement ParseCreateFunctionStatement(WordScanner word, NameSpace nameSpace)
        {
            return ParseCreateStatement(word,nameSpace);
        }
    }


}
