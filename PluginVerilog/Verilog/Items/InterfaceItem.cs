using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items
{
    public class InterfaceItem
    {
        /*
        interface_item  ::=   port_declaration ;
                            | non_port_interface_item 

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
                    return NonPortInterfaceItem.Parse(word, nameSpace);
            }
            return true;
        }

    }
}
