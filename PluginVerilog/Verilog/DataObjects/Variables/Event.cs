using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.Variables
{
    public class Event : Variable
    {
        protected Event() { }


        public Event(string Name)
        {
            this.Name = Name;
        }

        public static void ParseCreateFromDeclaration(WordScanner word, NameSpace nameSpace)
        {
            //            event_declaration::= event list_of_event_identifiers ;

            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            while (!word.Eof)
            {
                if (!General.IsSimpleIdentifier(word.Text))
                {
                    word.AddError("illegal event identifier");
                    return;
                }
                Event val = new Event();
                val.Name = word.Text;
                if (nameSpace.DataObjects.ContainsKey(val.Name))
                {
                    if (nameSpace.DataObjects[val.Name] is Event)
                    {
                        nameSpace.DataObjects.Remove(val.Name);
                        nameSpace.DataObjects.Add(val.Name, val);
                    }
                    else
                    {
                        word.AddError("duplicated event name");
                    }
                }
                else
                {
                    nameSpace.DataObjects.Add(val.Name, val);
                }

                word.Color(CodeDrawStyle.ColorType.Variable);
                word.MoveNext();

                if (word.GetCharAt(0) != ',') break;
                word.MoveNext();
            }

            if (word.Eof || word.GetCharAt(0) != ';')
            {
                word.AddError("; expected");
            }
            else
            {
                word.MoveNext();
            }

            return;
        }
    }
}
