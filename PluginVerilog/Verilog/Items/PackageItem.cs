using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items
{
    /*
    package_item ::=
          package_or_generate_item_declaration 
        | anonymous_program 
        | package_export_declaration 
        | timeunits_declaration
    
    A timeunits_declaration shall be legal as a non_port_module_item, non_port_interface_item,
    non_port_program_item, or package_item only if it repeats and matches a previous timeunits_declaration within
    the same time scope.
     */


    public class PackageItem
    {
        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            switch (word.Text)
            {
                // anonymous_program
                // package_export_declaration
                // timeunits_declaration
                default:
                    return PackageOrGenerateItemDeclaration.Parse(word, nameSpace);
            }
        }
    }
}
