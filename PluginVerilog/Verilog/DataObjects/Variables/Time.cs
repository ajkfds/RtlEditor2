using pluginVerilog.Verilog.DataObjects.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.Variables
{
    public class Time : IntegerAtomVariable
    {
        protected Time() { }

        public static new Time Create(DataType dataType)
        {
            System.Diagnostics.Debug.Assert(dataType.Type == DataTypeEnum.Int);
            DataTypes.IntegerAtomType dType = dataType as DataTypes.IntegerAtomType;

            Time val = new Time();
            val.DataType = dType.Type;
            return val;
        }

        public override Variable Clone()
        {
            Time val = new Time();
            val.DataType = DataType;
            val.Signed = Signed;
            return val;
        }

    }
}
