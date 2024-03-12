using pluginVerilog.Verilog.ModuleItems;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.DataTypes
{
    public class InterfaceInstance : DataType
    {
        public override DataTypeEnum Type
        {
            get
            {
                return DataTypeEnum.InterfaceInstance;
            }
        }

        private string interfaceName;

        public static InterfaceInstance Create(ModuleItems.IInstantiation inst)
        {
            InterfaceInstance interface_ = new InterfaceInstance();
            interface_.interfaceName = inst.Name;
            return interface_;
        }

    }
}
