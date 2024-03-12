using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.BuildingBlocks
{
    public interface IModuleOrInterfaceOrProgram : IBuildingBlock
    {

        // Port
        Dictionary<string, DataObjects.Port> Ports { get; }
        List<DataObjects.Port> PortsList { get; }
        List<string> PortParameterNameList { get; }

        // Generate


    }
}
