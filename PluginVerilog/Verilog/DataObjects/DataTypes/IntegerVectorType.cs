using pluginVerilog.Verilog.DataObjects.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.DataTypes
{
    public class IntegerVectorType : DataType
    {
        public virtual List<Range> PackedDimensions { get; protected set; } = new List<Range>();
        public virtual bool Signed { get; protected set; }

        //      data_type::= integer_vector_type[signing] { packed_dimension }
        //                   ...
        //      integer_vector_type::= "bit" | "logic" | "reg"

        // reg          4state  >=1bit      
        // logic        4state  >=1bit      
        // bit          2state  >=1bit      

        public virtual int BitWidth { get; }

        public static IntegerVectorType ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            switch (word.Text)
            {
                case "bit":
                    return parse(word, nameSpace, DataTypeEnum.Bit);
                case "logic":
                    return parse(word, nameSpace, DataTypeEnum.Logic);
                case "reg":
                    return parse(word, nameSpace, DataTypeEnum.Reg);
                default:
                    return null;
            }
        }

        public static IntegerVectorType Create(DataTypeEnum dataType, bool signed,List<Range> packedDimensions)
        {
            IntegerVectorType integerVectorType = new IntegerVectorType();
            integerVectorType.Type = dataType;
            integerVectorType.Signed = signed;
            integerVectorType.PackedDimensions = packedDimensions;
            return integerVectorType;
        }

        public static IntegerVectorType parse(WordScanner word,NameSpace nameSpace,DataTypeEnum dataType)
        {
            var integerVectorType = new IntegerVectorType();
            integerVectorType.Type = dataType;

            word.Color(CodeDrawStyle.ColorType.Keyword);
            if(dataType == DataTypeEnum.Bit | dataType == DataTypeEnum.Logic)
            {
                word.AddSystemVerilogError();
            }
            word.MoveNext();

            integerVectorType.Signed = false;

            if (word.Eof)
            {
                word.AddError("illegal reg declaration");
                return null;
            }
            if (word.Text == "signed")
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
                integerVectorType.Signed = true;
            }
            if (word.Eof)
            {
                word.AddError("illegal reg declaration");
                return null;
            }


            while (word.GetCharAt(0) == '[')
            {
                Range range = Range.ParseCreate(word, nameSpace);
                if (word.Eof || range == null)
                {
                    word.AddError("illegal reg declaration");
                    return null;
                }
                integerVectorType.PackedDimensions.Add(range);
            }
            return integerVectorType;
        }

    }
}
