using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items
{
    public class ModuleCommonItem
    {
        /*
        ## SystemVerilog 2012

        module_common_item ::= 
              module_or_generate_item_declaration 
            | interface_instantiation 
            | program_instantiation 
            | assertion_item 
            | bind_directive 
            | continuous_assign 
            | net_alias 
            | initial_construct 
            | final_construct 
            | always_construct 
            | loop_generate_construct 
            | conditional_generate_construct
            | elaboration_system_task                 
        */

        public static bool Parse(WordScanner word,NameSpace nameSpace)
        {



            // module_or_generate_item_declaration
            if (ModuleOrGenerateItemDeclaration.Parse(word, nameSpace.BuildingBlock )) return true;

            switch (word.Text)
            {
                // interface_instantiation
                // program_instantiation
                // assertion_item
                // bind_directive
                // net_alias
                // final_construct
                // elaboration_system_task

                // continuous_assign
                case "assign":
                    return ModuleItems.ContinuousAssign.Parse(word, nameSpace);
                // initial_construct
                case "initial":
                    return ModuleItems.InitialConstruct.Parse(word, nameSpace);
                // always_construct
                case "always":
                case "always_comb":
                case "always_latch":
                case "always_ff":
                    return ModuleItems.AlwaysConstruct.Parse(word, nameSpace);
                // loop_generate_construct
                case "for":
//                    word.AddSystemVerilogError();
                    return Generate.LoopGenerateConstruct.Parse(word, nameSpace);
                // conditional_generate_construct
                case "if":
                    return Generate.IfGenerateConstruct.Parse(word, nameSpace);
            }

            if (ModuleItems.InterfaceInstantiation.Parse(word, nameSpace)) return true;

            return false;
        }

    }
}
