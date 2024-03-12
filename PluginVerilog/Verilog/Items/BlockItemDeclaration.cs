using pluginVerilog.Verilog.DataObjects.Nets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items
{
    public class BlockItemDeclaration
    {
        /*
        ## SystemVerilog 2012

        block_item_declaration ::= 
                  { attribute_instance } data_declaration 
                | { attribute_instance } local_parameter_declaration ;
                | { attribute_instance } parameter_declaration ;
                | { attribute_instance } let_declaration 

        data_declaration ::= 
                [ const ] [ var ] [ lifetime ] data_type_or_implicit list_of_variable_decl_assignments ;10
                | type_declaration 
                | package_import_declaration
                | net_type_declaration
        */

        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            // data_declaration
            if (DataObjects.Variables.Variable.ParseDeclaration(word, nameSpace)) return true;

            if (word.Text == "typedef")
            {
                return DataObjects.Typedef.ParseDeclaration(word, nameSpace);
            }

            // TODO package_import_declaration

            switch (word.Text)
            {
                // net_declaration
                case "supply0":
                case "supply1":
                case "tri":
                case "triand":
                case "trior":
                case "trireg":
                case "tri0":
                case "tri1":
                case "uwire":
                case "wire":
                case "wand":
                case "wor":
                    Net.ParseDeclaration(word, nameSpace);
                    break;
                //              struct_union["packed"[signing]] { struct_union_member { struct_union_member } }{ packed_dimension }
                // local_parameter_declaration;
                // parameter_declaration;
                case "parameter":
                case "localparam":
                    Verilog.DataObjects.Constants.Parameter.ParseCreateDeclaration(word, nameSpace, null);
                    break;

                // etc
                case "(*":
                    Attribute attribute = Attribute.ParseCreate(word);
                    break;
                // errpr trap
                case "endgenerate":
                    return false;
                case "end":
                    return false;
                case "endmodule":
                    return false;

                default:
                    return false;
            }

            return true;



        }

    }
}
