using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ajkControls.ColorLabel;
using pluginVerilog.Verilog.DataObjects;
using pluginVerilog.Verilog.DataObjects.DataTypes;
using pluginVerilog.Verilog.DataObjects.Variables;

namespace pluginVerilog.Verilog.DataObjects.Variables
{
    public class IntegerVectorValueVariable : ValueVariable
    {
        //integer_vector_type::= bit | logic | reg
        public Range Range
        {
            get
            {
                if (PackedDimensions.Count < 1) return null;
                return PackedDimensions[0];
            }
        }
        public bool Signed { get; set; }

        public List<Range> PackedDimensions = new List<Range>();

        public new static IntegerVectorValueVariable Create(DataType dataType)
        {
            switch (dataType.Type)
            {
                case DataTypeEnum.Bit:
                    return Bit.Create(dataType);
                case DataTypeEnum.Logic:
                    return Logic.Create(dataType);
                case DataTypeEnum.Reg:
                    return Reg.Create(dataType);
                default:
                    System.Diagnostics.Debugger.Break();
                    break;
            }
            return null;
        }


        public override void AppendTypeLabel(ColorLabel label)
        {
            switch (DataType)
            {
                case DataTypeEnum.Bit:
                    label.AppendText("bit ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case DataTypeEnum.Logic:
                    label.AppendText("logic ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case DataTypeEnum.Reg:
                    label.AppendText("reg ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                default:
                    System.Diagnostics.Debugger.Break();
                    break;
            }
            label.AppendText(" ");
            if (Signed)
            {
                label.AppendText("signed ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
            }

            foreach(Range dimension in PackedDimensions)
            {
                label.AppendLabel(dimension.GetLabel());
                label.AppendText(" ");
            }
        }

    }
}
