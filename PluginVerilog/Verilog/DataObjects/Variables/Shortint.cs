using pluginVerilog.Verilog.DataObjects.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.Variables
{
    public class Shortint : IntegerAtomVariable
    {
        protected Shortint() { }

        public static new Shortint Create(DataType dataType)
        {
            System.Diagnostics.Debug.Assert(dataType.Type == DataTypeEnum.Shortint);
            DataTypes.IntegerAtomType dType = dataType as DataTypes.IntegerAtomType;

            Shortint val = new Shortint();
            val.DataType = dType.Type;
            return val;
        }

        public override Variable Clone()
        {
            Shortint val = new Shortint();
            val.DataType = DataType;
            val.Signed = Signed;
            return val;
        }


    }
}
