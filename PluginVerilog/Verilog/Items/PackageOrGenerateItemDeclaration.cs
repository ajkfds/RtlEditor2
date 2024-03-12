using pluginVerilog.Verilog.DataObjects.Nets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items
{
    public class PackageOrGenerateItemDeclaration
    {
        /*
        package_or_generate_item_declaration ::= 
              net_declaration 
            | data_declaration 
            | task_declaration 
            | function_declaration 
            | checker_declaration 
            | dpi_import_export 
            | extern_constraint_declaration 
            | class_declaration 
            | class_constructor_declaration 
            | local_parameter_declaration ;
            | parameter_declaration ;
            | covergroup_declaration 
            | overload_declaration 
            | assertion_item_declaration 
            | ;
         */
        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            // data_declaration
            if (DataObjects.Variables.Variable.ParseDeclaration(word, nameSpace)) return true;
            /*
            data_declaration ::=      [ const ] [ var ] [ lifetime ] data_type_or_implicit list_of_variable_decl_assignments ;10
                                    | type_declaration
                                    | package_import_declaration
                                    | net_type_declaration       
             */
            if(word.Text == "typedef")
            {
                return DataObjects.Typedef.ParseDeclaration(word, nameSpace);
            }


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
                case "event":
                    DataObjects.Variables.Event.ParseCreateFromDeclaration(word, nameSpace);
                    break;
                //              ps_covergroup_identifier
                //              type_reference
                //          implicit_data_type
                //      type_declaration

                //      package_import_declaration

                //      net_type_declaration

                // task_declaration
                case "task":
                    Task.Parse(word, nameSpace);
                    break;

                // function_declaration
                case "function":
                    Function.Parse(word, nameSpace);
                    break;

                // checker_declaration
                // TODO

                // dpi_import_export
                // TODO

                // extern_constraint_declaration
                // TODO

                // class_declaration
                //BuildingBlocks.Class.P

                // class_constructor_declaration
                
                // local_parameter_declaration;
                // parameter_declaration;
                case "parameter":
                case "localparam":
                    Verilog.DataObjects.Constants.Parameter.ParseCreateDeclaration(word, nameSpace, null);
                    break;

                // covergroup_declaration
                // overload_declaration
                // assertion_item_declaration

                // ;
                case ";":
                    word.AddSystemVerilogError();
                    word.MoveNext();
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
