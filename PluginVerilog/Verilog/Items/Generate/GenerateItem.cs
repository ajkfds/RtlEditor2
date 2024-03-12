using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items.Generate
{
    public class GenerateItem
    {
        /*
        generate_item ::= module_or_generate_item 
        | interface_or_generate_item 
        | checker_or_generate_item          

        30) Within an interface_declaration, it shall only be legal for a generate_item to be an interface_or_generate_item. 
            Within a module_declaration, except when also within an interface_declaration, it shall only be legal for a 
            generate_item to be a module_or_generate_item. Within a checker_declaration, it shall only be legal for a 
            generate_item to be a checker_or_generate_item. 
         */


        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            if (nameSpace.BuildingBlock is BuildingBlocks.BuildingBlock)
            {
                return ModuleOrGenerateItem.Parse(word, nameSpace);
            }

            return false;
        }

    }
}
