using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items
{
    public class ModuleOrGenerateItem
    {
        /*
        module_or_generate_item ::= 
              { attribute_instance } parameter_override 
            | { attribute_instance } gate_instantiation 
            | { attribute_instance } udp_instantiation 
            | { attribute_instance } module_instantiation 
            | { attribute_instance } module_common_item         
         */
        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            switch (word.Text)
            {
                // parameter_override
                // parameter_override::= ""defparam"" list_of_defparam_assignments;
                case "defparam":
                    return ModuleItems.ParameterOverride.Parse(word, nameSpace);
                // gate_instantiation
                case "cmos":
                case "rcmos":
                case "bufif0":
                case "bufif1":
                case "notif0":
                case "notif1":
                case "nmos":
                case "pmos":
                case "rnmos":
                case "rpmos":
                case "and":
                case "nand":
                case "or":
                case "nor":
                case "xor":
                case "xnor":
                case "buf":
                case "not":
                case "tranif0":
                case "tranif1":
                case "rtranif0":
                case "rtranif1":
                case "tran":
                case "rtran":
                case "pullup":
                case "pulldown":
                    return ModuleItems.GateInstantiation.Parse(word, nameSpace);
            }

            // module_common_item
            if (ModuleCommonItem.Parse(word, nameSpace)) return true;


            // udp_instantiation
            // module_instantiation
            if (ModuleItems.ModuleInstantiation.Parse(word, nameSpace)) return true;



            return false;
        }
    }
}
