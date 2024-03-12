using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items
{
    public class InterfaceOrGenerateItem
    {
        /*
        interface_or_generate_item  ::=   { attribute_instance } module_common_item 
                                        | { attribute_instance } extern_tf_declaration      
         */
        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {

            // module_common_item
            if (ModuleCommonItem.Parse(word, nameSpace)) return true;


            return false;
        }
    }
}
