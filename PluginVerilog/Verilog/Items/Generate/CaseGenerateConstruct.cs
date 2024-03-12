using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items.Generate
{
    public class CaseGenerateConstruct
    {
        public static bool Parse(WordScanner word, NameSpace module)
        {
            if (word.Text != "case") return false;

            // generate_case_statement::=  case (constant_expression ) genvar_case_item { genvar_case_item } endcase
            // genvar_case_item ::=  constant_expression  { , constant_expression } : generate_item_or_null
            //                      | default [ : ] generate_item_or_null
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            if (word.Text != "(")
            {
                word.AddError("( required");
                return true;
            }
            word.MoveNext();

            Expressions.Expression conditionExpression = Expressions.Expression.ParseCreate(word, module as NameSpace);
            if (conditionExpression == null)
            {
                word.AddError("illegal constant_expression");
                return true;
            }
            if (!conditionExpression.Constant)
            {
                word.AddError("should be constant");
            }

            if (word.Text != ")")
            {
                word.AddError(") required");
                return true;
            }
            word.MoveNext();

            bool caseSelected = false;
            while (!word.Eof)
            {
                Expressions.Expression expression = null;
                if (word.Text == "endcase")
                {
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
                }
                else if (word.Text == "default")
                {
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();

                    if (word.Text == ":") word.MoveNext();

                    if (caseSelected && word.Active)
                    {
                        word.StartNonGenenerated();
                        while (!word.Eof)
                        {
                            if (!GenerateItem.Parse(word, module)) break;
                        }
                        word.EndNonGeneratred();
                    }
                    else
                    {
                        while (!word.Eof)
                        {
                            if (!GenerateItem.Parse(word, module)) break;
                        }
                    }
                }
                else
                {
                    while (!word.Eof)
                    {
                        expression = Expressions.Expression.ParseCreate(word, module as NameSpace);
                        if (expression == null)
                        {
                            word.AddError("illegal constant expression");
                            return true;
                        }
                        if (!expression.Constant)
                        {
                            word.AddError("should be constant");
                        }
                        if (word.Text != ",") break;
                        word.MoveNext();
                    }
                    if (word.Text != ":")
                    {
                        word.AddError(": required");
                        return true;
                    }
                    word.MoveNext();

                    if (word.Active && expression != null && conditionExpression != null && expression.Constant && conditionExpression.Constant && expression.Value != conditionExpression.Value)
                    {
                        // false
                        word.StartNonGenenerated();
                        while (!word.Eof)
                        {
                            if (!GenerateItem.Parse(word, module)) break;
                        }
                        word.EndNonGeneratred();
                    }
                    else
                    {
                        while (!word.Eof)
                        {
                            if (!GenerateItem.Parse(word, module)) break;
                        }
                        caseSelected = true;
                    }
                }
            }

            return true;
        }
    }
}
