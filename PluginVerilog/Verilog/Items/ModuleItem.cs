using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items
{
    public class ModuleItem
    {
        /*
        module_item ::= 
              port_declaration ";"
            | non_port_module_item

        port_declaration::=
              { attribute_instance } inout_declaration
            | { attribute_instance } input_declaration
            | { attribute_instance } output_declaration
            | { attribute_instance } ref_declaration
            | { attribute_instance } interface_port_declaration

       */
        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            switch (word.Text)
            {
                case "input":
                case "output":
                case "inout":
                    Verilog.DataObjects.Port.ParsePortDeclarations(word, nameSpace);
                    if (word.GetCharAt(0) != ';')
                    {
                        word.AddError("; expected");
                    }
                    else
                    {
                        word.MoveNext();
                    }
                    break;
                default:
                    return NonPortModuleItem.Parse(word, nameSpace);
            }
            return true;
        }

    }
}
