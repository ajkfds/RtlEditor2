using pluginVerilog.Verilog.DataObjects.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects
{
    public class Typedef
    {
        public DataType VariableType;
        public string Name;

        public static bool ParseDeclaration(WordScanner word, NameSpace nameSpace)
        {

            /* ## SyetemVerilog2012
             type_declaration ::= 
                  "typedef" data_type type_identifier { variable_dimension } ;
                | "typedef" interface_instance_identifier constant_bit_select . type_identifier type_identifier ;
                | "typedef" [ "enum" | "struct" | "union" | "class" | "interface class" ] type_identifier ;
             */
            if (word.Text != "typedef") System.Diagnostics.Debugger.Break();
            word.Color(CodeDrawStyle.ColorType.Identifier);
            word.MoveNext();

            Typedef typeDef = new Typedef();

            word.AddSystemVerilogError();

            typeDef.VariableType = DataType.ParseCreate(word, nameSpace, null);
            if(typeDef.VariableType == null)
            {
                word.AddError("datat type expected");
                word.SkipToKeyword(";");
                return true;
            }

            if (!General.IsIdentifier(word.Text))
            {
                word.AddError("illegal type_identifier");
                word.SkipToKeyword(";");
                return true;
            }

            word.Color(CodeDrawStyle.ColorType.Identifier);
            typeDef.Name = word.Text;
            word.MoveNext();


            if (word.Active)
            {
                if (word.Prototype)
                {
                    if (nameSpace.Typedefs.ContainsKey(typeDef.Name))
                    {
                        //                            nameRef.AddError("duplicated name");
                    }
                    else
                    {
                        nameSpace.Typedefs.Add(typeDef.Name, typeDef);
                    }
                }
                else
                {
                    if (nameSpace.Typedefs.ContainsKey(typeDef.Name))
                    {
                        if (nameSpace.Typedefs[typeDef.Name] is Typedef)
                        {
                            typeDef = nameSpace.Typedefs[typeDef.Name] as Typedef;
                        }
                    }
                    else
                    {
                        nameSpace.Typedefs.Add(typeDef.Name, typeDef);
                    }
                }
            }

            if(word.Text != ";")
            {
                word.AddError("; expedted");
                return true;
            }
            word.MoveNext();    // ;

            return true;
        }



    }
}
