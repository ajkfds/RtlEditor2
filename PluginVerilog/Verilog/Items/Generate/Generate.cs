using pluginVerilog.Verilog.BuildingBlocks;
using pluginVerilog.Verilog.Items.Generate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog
{
    public class Generate
    {
        /*
        generate_region ::=
            "generate" { generate_item } "endgenerate"

        loop_generate_construct ::= 
            "for" "(" genvar_initialization ; genvar_expression ; genvar_iteration ")"generate_block 
        genvar_initialization ::= 
            [ "genvar" ] genvar_identifier = constant_expression 
        genvar_iteration ::= 
              genvar_identifier assignment_operator genvar_expression 
            | inc_or_dec_operator genvar_identifier 
            | genvar_identifier inc_or_dec_operator 
        conditional_generate_construct ::= 
            if_generate_construct 
            | case_generate_construct 
        if_generate_construct ::= 
            if ( constant_expression ) generate_block [ else generate_block ] 
        case_generate_construct ::= 
            case ( constant_expression ) case_generate_item { case_generate_item } endcase
        case_generate_item ::= 
              constant_expression { , constant_expression } : generate_block 
            | default [ : ] generate_block 
        generate_block ::= 
            generate_item 
            | [ generate_block_identifier : ] begin [ : generate_block_identifier ] { generate_item }  end [ : generate_block_identifier ] 
        generate_item ::= 
              module_or_generate_item 
            | interface_or_generate_item 
            | checker_or_generate_item          
        
        30) Within an interface_declaration, it shall only be legal for a generate_item to be an interface_or_generate_item. 
        Within a module_declaration, except when also within an interface_declaration, it shall only be legal for a 
        generate_item to be a module_or_generate_item. Within a checker_declaration, it shall only be legal for a 
        generate_item to be a checker_or_generate_item.         
         */


        private static bool parseGenvarAssignment(WordScanner word, NameSpace nameSpace)
        {
            //    genvar_assignment::= genvar_identifier = constant_expression
            Expressions.VariableReference genvar = Expressions.VariableReference.ParseCreate(word, nameSpace, true);
            if (genvar == null) return false;
            if (!(genvar.Variable is DataObjects.Variables.Genvar))
            {
                word.AddError("should be genvar");
            }
            if (word.Text != "=")
            {
                word.AddError("( expected");
                return true;
            }
            word.MoveNext();
            Expressions.Expression constant = Expressions.Expression.ParseCreate(word, nameSpace);
            if (constant == null) return false;
            if (!constant.Constant)
            {
                word.AddError("should be constant");
            }
            return true;
        }



        public static bool ParseGenerateBlockStatement(WordScanner word, NameSpace nameSpace)
        {
            if (word.Text != "begin") return false;

            // generate_block ::= begin[ : generate_block_identifier]  { generate_item } end
            word.Color(CodeDrawStyle.ColorType.Keyword);
            WordReference beginRef = word.GetReference();
            word.MoveNext();

            NamedGeneratedBlock block = nameSpace as NamedGeneratedBlock;

            if (word.Text == ":")
            {
                word.MoveNext();

                if (!General.IsIdentifier(word.Text))
                {
                    word.AddError("identifier required");
                    return true;
                }
                word.Color(CodeDrawStyle.ColorType.Identifier);
                block = NamedGeneratedBlock.Create(nameSpace, word.Text);
                if (word.Prototype)
                {
                    if (nameSpace.NameSpaces.ContainsKey(word.Text))
                    {
                        word.AddPrototypeError("duplicated identifier");
                    }
                    else
                    {
                        nameSpace.NameSpaces.Add(word.Text, block as NamedGeneratedBlock);
                    }
                }
                else
                {
                    if (nameSpace.NameSpaces.ContainsKey(word.Text))
                    {
//                        word.AddPrototypeError("duplicated identifier");
                    }
                    else
                    {
                        word.AddPrototypeError("identifier prototype not found");
                        nameSpace.NameSpaces.Add(word.Text, block as NamedGeneratedBlock);
                    }
                }
                word.MoveNext();
            }

            if (word.Active)
            {
                while (!word.Eof)
                {
                    while (!word.Eof)
                    {
                        if (!GenerateItem.Parse(word, nameSpace)) break;
                    }
                }
            }
            else
            {
                int beginCount = 0;
//                word.AddError("illegal sequential block");
                while (!word.Eof && word.Text != "endgenerate")
                {
                    if (word.Text == "begin")
                    {
                        beginCount++;
                    }
                    else if (word.Text == "end")
                    {
                        if (beginCount == 0)
                        {
                            break;
                        }
                        beginCount--;
                    }
                    word.MoveNext();
                }
            }

            if (word.Text == "end")
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
                return true;
            }
            else
            {
                word.AddError("end required");
            }
            return true;
        }

        public static bool ParseGenerateBlockStatementWithName(WordScanner word, NameSpace nameSpace)
        {
            if (word.Text != "begin") return false;

            // generate_block ::= begin[ : generate_block_identifier]  { generate_item } end
            word.Color(CodeDrawStyle.ColorType.Keyword);
            WordReference beginRef = word.GetReference();
            word.MoveNext();

            if (word.Text != ":")
            {
                beginRef.AddError(": required");
            }
            else
            {
                word.MoveNext();

                if (!General.IsIdentifier(word.Text))
                {
                    word.AddError("identifier required");
                    return true;
                }
                word.Color(CodeDrawStyle.ColorType.Identifier);
                word.MoveNext();
            }

            if (word.Active)
            {
                while (!word.Eof)
                {
                    if (!GenerateItem.Parse(word, nameSpace)) break;
                }
            }
            else
            {
                int beginCount = 0;
                word.AddError("illegal sequential block");
                while (!word.Eof && word.Text != "endgenerate")
                {
                    if (word.Text == "begin")
                    {
                        beginCount++;
                    }
                    else if (word.Text == "end")
                    {
                        if (beginCount == 0)
                        {
                            break;
                        }
                        beginCount--;
                    }
                    word.MoveNext();
                }
            }

            if (word.Text == "end")
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
                return true;
            }
            else
            {
                word.AddError("end required");
            }
            return true;
        }


        /*
        generate_region ::=
            "generate" { generate_item } "endgenerate"
        loop_generate_construct ::=
            for ( genvar_initialization ; genvar_expression ; genvar_iteration ) generate_block 
        genvar_initialization ::= 
            [ genvar ] genvar_identifier "=" constant_expression 
        genvar_iteration ::= 
              genvar_identifier assignment_operator genvar_expression 
            | inc_or_dec_operator genvar_identifier 
            | genvar_identifier inc_or_dec_operator
        
        onditional_generate_construct ::= 
              if_generate_construct 
            | case_generate_construct 
        if_generate_construct ::= 
            if ( constant_expression ) generate_block [ else generate_block ] 
        case_generate_construct ::= 
            case ( constant_expression ) case_generate_item { case_generate_item } endcase
        case_generate_item ::= 
              constant_expression { , constant_expression } : generate_block 
            | default [ : ] generate_block 
        generate_block ::= 
              generate_item 
            | [ generate_block_identifier : ] begin [ : generate_block_identifier ] { generate_item } end [ : generate_block_identifier ] 
        generate_item ::= 
              module_or_generate_item (module)
            | interface_or_generate_item (interface)
            | checker_or_generate_item (checker)
        30) Within an interface_declaration, it shall only be legal for a generate_item to be an interface_or_generate_item.
            Within a module_declaration, except when also within an interface_declaration, it shall only be legal for a generate_item to be a module_or_generate_item. 
            Within a checker_declaration, it shall only be legal for a generate_item to be a checker_or_generate_item. 

        program_generate_item ::=
              loop_generate_construct 
            | conditional_generate_construct 
            | generate_region 
            | elaboration_system_task

        module_or_generate_item ::=
              { attribute_instance } parameter_override 
            | { attribute_instance } gate_instantiation 
            | { attribute_instance } udp_instantiation 
            | { attribute_instance } module_instantiation 
            | { attribute_instance } module_common_item
        interface_or_generate_item ::=
              { attribute_instance } module_common_item 
            | { attribute_instance } modport_declaration 
            | { attribute_instance } extern_tf_declaration        
*/
    }
}
