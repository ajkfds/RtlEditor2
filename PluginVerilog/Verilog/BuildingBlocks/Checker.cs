using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.BuildingBlocks
{
    public class Checker
    {
        /*
        ## checker
        checker_declaration ::=
            "checker" checker_identifier [ ( [ checker_port_list ] ) ] ; { { attribute_instance } checker_or_generate_item } "endchecker" [ : checker_identifier ] 
        checker_port_list ::= 
            checker_port_item {, checker_port_item}

        checker_port_item ::= 
            { attribute_instance } [ checker_port_direction ] property_formal_type formal_port_identifier {variable_dimension} [ "=" property_actual_arg ]

        checker_port_direction ::= 
            "input" | "output"

        checker_or_generate_item ::= 
              checker_or_generate_item_declaration 
            | initial_construct
            | always_construct 
            | final_construct
            | assertion_item
            | continuous_assign 
            | checker_generate_item

        checker_or_generate_item_declaration ::=
              [ rand ] data_declaration
            | function_declaration 
            | checker_declaration 
            | assertion_item_declaration
            | covergroup_declaration 
            | overload_declaration
            | genvar_declaration
            | clocking_declaration
            | default clocking clocking_identifier ;
            | default disable iff expression_or_dist ;
            | ;

        checker_generate_item ::= 
              loop_generate_construct
            | conditional_generate_construct
            | generate_region
            | elaboration_system_task
        checker_identifier ::= 
            identifier
         
         */
    }
}
