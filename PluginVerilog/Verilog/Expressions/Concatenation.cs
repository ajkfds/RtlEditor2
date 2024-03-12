using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Expressions
{
    public class Concatenation : Primary
    {
        protected Concatenation() { }
        public List<Expression> Expressions = new List<Expression>();

        public static Primary ParseCreateConcatenationOrMultipleConcatenation(WordScanner word, NameSpace nameSpace)
        {
            return ParseCreateConcatenationOrMultipleConcatenation(word, nameSpace, false);
        }

        public static Primary ParseCreateConcatenationOrMultipleConcatenation(WordScanner word, NameSpace nameSpace, bool lValue)
        {
            WordReference reference = word.GetReference();
            word.MoveNext(); // {

            Expression exp1;
            if (lValue)
            {
                exp1 = Expression.ParseCreateVariableLValue(word, nameSpace);
            }
            else
            {
                exp1 = Expression.ParseCreate(word, nameSpace);
            }
            if (exp1 == null)
            {
                word.AddError("illegal concatenation");
                return null;
            }
            if (word.GetCharAt(0) == '{')
            {
                return MultipleConcatenation.ParseCreate(word, nameSpace, exp1, reference);
            }
            Concatenation concatenation = new Concatenation();
            concatenation.Expressions.Add(exp1);
            concatenation.Constant = exp1.Constant;
            concatenation.BitWidth = exp1.BitWidth;

            while (word.GetCharAt(0) != '}')
            {
                if (word.GetCharAt(0) != ',')
                {
                    return null;
                }
                word.MoveNext();

                if (word.Eof)
                {
                    word.AddError("illegal concatenation");
                    return null;
                }
                if (lValue)
                {
                    exp1 = Expression.ParseCreateVariableLValue(word, nameSpace);
                }
                else
                {
                    exp1 = Expression.ParseCreate(word, nameSpace);
                }
                if (exp1 != null)
                {
                    concatenation.Expressions.Add(exp1);
                    concatenation.Constant = concatenation.Constant & exp1.Constant;
                    concatenation.BitWidth = concatenation.BitWidth + exp1.BitWidth;
                }
                if (exp1 == null)
                {
                    word.AddError("illegal concatenation");
                    return null;
                }
            }
            concatenation.Reference = word.GetReference().CreateReferenceFrom(reference);
            word.MoveNext(); // }
            return concatenation;
        }
        /*
        concatenation           ::= { expression { , expression } }
        multiple_concatenation  ::= { constant_expression concatenation }

        constant_concatenation  ::= { constant_expression { , constant_expression } }
        constant_multiple_concatenation ::= { constant_expression constant_concatenation }

        module_path_concatenation ::= { module_path_expression { , module_path_expression } }
        module_path_multiple_concatenation ::= { constant_expression module_path_concatenation }


        net_concatenation       ::= { net_concatenation_value { , net_concatenation_value } }
        net_concatenation_value ::= hierarchical_net_identifier
                                    | hierarchical_net_identifier [ expression ] { [ expression ] }
                                    | hierarchical_net_identifier [ expression ] { [ expression ] } [ range_expression ]
                                    | hierarchical_net_identifier [ range_expression ]
                                    | net_concatenation  

        variable_concatenation ::= { variable_concatenation_value { , variable_concatenation_value } }  
        variable_concatenation_value    ::= hierarchical_variable_identifier
                                        | hierarchical_variable_identifier [ expression ] { [ expression ] }
                                        | hierarchical_variable_identifier [ expression ] { [ expression ] } [ range_expression ]
                                        | hierarchical_variable_identifier [ range_expression ]          
                                        | variable_concatenation  
        */
    }

    public class MultipleConcatenation : Primary
    {
        protected MultipleConcatenation() { }

        public Expression MultipleExpression { get; protected set; }
        public Expression Expression { get; protected set; }

        public static MultipleConcatenation ParseCreate(WordScanner word, NameSpace nameSpace, Expression multipleExpression,WordReference reference)
        {
            word.MoveNext(); // {

            Expression exp = Expression.ParseCreate(word, nameSpace);
            if (word.Eof || word.GetCharAt(0) != '}')
            {
                word.AddError("illegal multiple concatenation");
                return null;
            }
            word.MoveNext(); // }

            if (word.Eof || word.GetCharAt(0) != '}')
            {
                word.AddError("illegal multiple concatenation");
                return null;
            }
            MultipleConcatenation multipleConcatenation = new MultipleConcatenation();
            multipleConcatenation.MultipleExpression = multipleExpression;
            multipleConcatenation.Expression = exp;
            multipleConcatenation.Reference = word.GetReference().CreateReferenceFrom(reference);
            if(exp != null) multipleConcatenation.Constant = exp.Constant & multipleConcatenation.Constant;

            if(exp != null && multipleExpression != null)
            {
                if (exp.BitWidth != null && multipleExpression.Value != null)
                {
                    multipleConcatenation.BitWidth = (int)exp.BitWidth * (int)multipleExpression.Value;
                }
                if(exp.Constant && multipleExpression.Constant)
                {
                    multipleConcatenation.Constant = true;
                }

            }

            word.MoveNext(); // }

            return multipleConcatenation;
        }

    }
}
