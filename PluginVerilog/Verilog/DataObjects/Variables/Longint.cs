using pluginVerilog.Verilog.DataObjects.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.Variables
{
    public class Longint : IntegerAtomVariable
    {
        protected Longint() { }

        public static new Longint Create(DataType dataType)
        {
            if (dataType.Type == DataTypeEnum.Int) System.Diagnostics.Debugger.Break();
            DataTypes.IntegerAtomType dType = dataType as DataTypes.IntegerAtomType;

            Longint val = new Longint();
            val.DataType = dType.Type;
            return val;
        }

        public override Variable Clone()
        {
            Longint val = new Longint();
            val.DataType = DataType;
            val.Signed = Signed;
            return val;
        }


    }
}
