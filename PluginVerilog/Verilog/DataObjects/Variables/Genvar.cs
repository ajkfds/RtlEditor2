using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.Variables
{
    public class Genvar : Variable
    {
        protected Genvar() { }


        public Genvar(string Name)
        {
            this.Name = Name;
        }

        public static void ParseCreateFromDeclaration(WordScanner word, NameSpace nameSpace)
        {
            //            genvar_declaration::= genvar list_of_genvar_identifiers;

            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            while (!word.Eof)
            {
                if (!General.IsSimpleIdentifier(word.Text))
                {
                    word.AddError("illegal real identifier");
                    return;
                }
                Genvar val = new Genvar();
                val.DefinedReference = word.GetReference();
                val.Name = word.Text;


                if (word.Active)
                {
                    if (word.Prototype)
                    {
                        if (nameSpace.DataObjects.ContainsKey(val.Name))
                        {
//                            nameRef.AddError("duplicated net name");
                        }
                        else
                        {
                            nameSpace.DataObjects.Add(val.Name, val);
                        }
                    }
                    else
                    {
                        if (nameSpace.DataObjects.ContainsKey(val.Name) && nameSpace.DataObjects[val.Name] is Genvar)
                        {
                            val = nameSpace.DataObjects[val.Name] as Genvar;
                        }
                    }
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
