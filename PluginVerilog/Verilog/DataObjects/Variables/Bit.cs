using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ajkControls;
using ajkControls.ColorLabel;

namespace pluginVerilog.Verilog.DataObjects.Variables
{
    public class Bit : IntegerVectorValueVariable
    {
        protected Bit() { }

        public static new Bit Create(DataTypes.DataType dataType)
        {
            System.Diagnostics.Debug.Assert(dataType.Type == DataTypes.DataTypeEnum.Bit);
            DataTypes.IntegerVectorType dType = dataType as DataTypes.IntegerVectorType;

            Bit val = new Bit();
            val.PackedDimensions = dType.PackedDimensions;
            val.DataType = dType.Type;
            return val;
        }

        public override Variable Clone()
        {
            Bit val = new Bit();
            val.DataType = DataType;
            val.PackedDimensions = PackedDimensions;
            val.Signed = Signed;
            return val;
        }

    }
}
