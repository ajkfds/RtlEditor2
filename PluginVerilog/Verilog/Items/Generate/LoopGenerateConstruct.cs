using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items.Generate
{
    public class LoopGenerateConstruct
    {
        // ## Verilog 2001
        // generate_loop_statement::=
        //  for (genvar_assignment; constant_expression; genvar_assignment) begin : generate_block_identifier { generate_item } end

        // ## SystemVerilog 2012
        // loop_generate_construct ::=
        //      "for" "(" genvar_initialization ";" genvar_expression ";" genvar_iteration ")" generate_block
        // genvar_initialization ::= 
        //      ["genvar"] genvar_identifier = constant_expression

        // genvar_expression ::= constant_expression
        // 
        // genvar_iteration ::= 
        //        genvar_identifier assignment_operator genvar_expression 
        //      | inc_or_dec_operator genvar_identifier 
        //      | genvar_identifier inc_or_dec_operator

        public static bool Parse(WordScanner word, NameSpace module)
        {
            if (word.Text != "for") return false;

            // generate_loop_statement::=  for (genvar_assignment; constant_expression; genvar_assignment) begin : generate_block_identifier { generate_item } end
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            if (word.Text != "(")
            {
                word.AddError("( expected");
                return true;
            }
            word.MoveNext();

            if (! GenvarInitialization.Parse(word, module))
            {
                return true;
            }

            if (word.Text != ";")
            {
                word.AddError("; expected");
                return true;
            }
            word.MoveNext();

            Expressions.Expression constant = Expressions.Expression.ParseCreate(word, module as NameSpace);
            if (constant == null) return true;
            if (!constant.Constant)
            {
                //constant.Reference.AddError("should be constant");
            }

            if (word.Text != ";")
            {
                word.AddError("; expected");
                return true;
            }
            word.MoveNext();

            if (!GenvarIteration.Parse(word, module as NameSpace))
            {
                return true;
            }

            if (word.Text != ")")
            {
                word.AddError(") expected");
                return true;
            }
            word.MoveNext();

            GenerateBlock.Parse(word, module);
            return true;
        }
    }
}
