using pluginVerilog.Verilog.DataObjects.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.DataTypes
{
    public class IntegerAtomType : DataType
    {
        public virtual bool Signed { get; protected set; }

        // data_type            ::=   integer_atom_type[signing]
        //                          | ...
        // integer_atom_type    ::=   "byte" | "shortint" | "int" | "longint" | "integer" | "time"
        // signing              ::=   "signed" | "unsigned"


        public static IntegerAtomType ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            IntegerAtomType dType = new IntegerAtomType();

            switch (word.Text)
            {
                case "byte":
                    return parse(word,nameSpace,DataTypeEnum.Byte);
                case "shortint":
                    return parse(word, nameSpace, DataTypeEnum.Shortint);
                case "int":
                    return parse(word, nameSpace, DataTypeEnum.Int);
                case "longint":
                    return parse(word, nameSpace, DataTypeEnum.Longint);
                case "integer":
                    return parse(word, nameSpace, DataTypeEnum.Integer);
                case "time":
                    return parse(word, nameSpace, DataTypeEnum.Time);
                default:
                    return null;
            }
        }

        public static IntegerAtomType Create(DataTypeEnum dataType,bool signed)
        {
            IntegerAtomType integerAtomType = new IntegerAtomType();
            integerAtomType.Type = dataType;
            integerAtomType.Signed = signed;
            return integerAtomType;
        }

        protected static IntegerAtomType parse(WordScanner word, NameSpace nameSpace, DataTypeEnum dataType)
        {
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            IntegerAtomType integerAtomType = new IntegerAtomType();
            integerAtomType.Type = dataType;

            integerAtomType.Signed = false;

            if (word.Eof)
            {
                word.AddError("illegal reg declaration");
                return null;
            }
            if (word.Text == "signed")
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
                integerAtomType.Signed = true;
            }else if (word.Text == "unsigned")
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
                integerAtomType.Signed = false;
            }


            if (word.Eof)
            {
                word.AddError("illegal reg declaration");
                return null;
            }

            return integerAtomType;
        }
    }
}
