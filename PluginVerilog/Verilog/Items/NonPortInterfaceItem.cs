using pluginVerilog.Verilog.Items.Generate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items
{
    public class NonPortInterfaceItem
    {
        /*
        non_port_interface_item ::=  generate_region 
                                    | interface_or_generate_item 
                                    | program_declaration 
                                    | modport_declaration
                                    | interface_declaration 
                                    | timeunits_declaration3
       */
        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            switch (word.Text)
            {
                // generate_region
                case "generate":
                    return GenerateRegion.Parse(word, nameSpace);
                // specify_block
                case "specify":
                    // TODO
                    word.MoveNext();
                    return true;
                // { attribute_instance }specparam_declaration
                // program_declaration
                // module_declaration

                // interface_declaration
                // timeunits_declaration
                // module_or_generate_item
                default:
                    break;

            }
            if (InterfaceOrGenerateItem.Parse(word, nameSpace)) return true;
            return false;
        }
    }
}
