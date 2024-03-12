using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog
{
    public class Attribute
    {
        /*
        A.9.1 Attributes
        attribute_instance ::= (* attr_spec { , attr_spec }  *)
        attr_spec ::=            attr_name = constant_expression          | attr_name
        attr_name ::= identifier 
            */
        protected Attribute() { }

        public static Attribute ParseCreate(Verilog.WordScanner word)
        {
            if (word.Text != "(*") return null;

            Attribute att = new Attribute();
            while ( !word.Eof )
            {
                word.MoveNext();
                if(word.Text == "*)")
                {
                    word.MoveNext();
                    break;
                }
            }
            return att;
        }
    }


}
