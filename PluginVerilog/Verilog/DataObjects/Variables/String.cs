using ajkControls.ColorLabel;
using pluginVerilog.Verilog.DataObjects.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.Variables
{
    public class String : ValueVariable
    {
        protected String() { }

        public static new String Create(DataType dataType)
        {
            System.Diagnostics.Debug.Assert(dataType.Type == DataTypeEnum.String);

            String val = new String();
            val.DataType = dataType.Type;
            return val;
        }

        public override Variable Clone()
        {
            String val = new String();
            val.DataType = DataType;
            return val;
        }

        public override void AppendTypeLabel(ColorLabel label)
        {
            label.AppendText("string ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
            label.AppendText(" ");
        }

    }
}
