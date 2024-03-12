using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items.Generate
{
    /*
    if_generate_construct ::= 
        "if" "(" constant_expression ")" generate_block [ else generate_block ] 
     */
    public class IfGenerateConstruct
    {
        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            if (word.Text != "if") return false;

            // generate_conditional_statement::= if (constant_expression ) generate_item_or_null[ else generate_item_or_null]
            // true (that is, has a nonzero known value)
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            if (word.Text != "(")
            {
                word.AddError("( required");
                return true;
            }
            word.MoveNext();

            Expressions.Expression expression = Expressions.Expression.ParseCreate(word, nameSpace as NameSpace);
            if (word.Active && expression != null && !expression.Constant)
            {
                word.AddError("should be constant");
            }

            if (word.Text != ")")
            {
                word.AddError(") required");
                return true;
            }
            word.MoveNext();

            if (word.Active && expression != null && expression.Constant && expression.Value == 0)
            {
                // false
                word.StartNonGenenerated();
                while (!word.Eof)
                {
                    if (!GenerateBlock.Parse(word, nameSpace)) break;
                }
                word.EndNonGeneratred();
            }
            else
            {
                while (!word.Eof)
                {
                    if (!GenerateBlock.Parse(word, nameSpace)) break;
                }
            }

            if (word.Text == "else")
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();

                if (word.Active && expression != null && expression.Constant && expression.Value != 0)
                {
                    // false
                    word.StartNonGenenerated();
                    while (!word.Eof)
                    {
                        if (!GenerateBlock.Parse(word, nameSpace)) break;
                    }
                    word.EndNonGeneratred();
                }
                else
                {
                    while (!word.Eof)
                    {
                        if (!GenerateBlock.Parse(word, nameSpace)) break;
                    }
                }
            }

            return true;
        }
    }
}
