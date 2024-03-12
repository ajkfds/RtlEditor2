using pluginVerilog.Verilog.BuildingBlocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog
{
    public interface IPortNameSpace
    {
        Dictionary<string, DataObjects.DataObject> DataObjects { get; }
        NameSpace Parent { get; }
        Dictionary<string, DataObjects.Constants.Constants> Constants { get; }

        BuildingBlocks.BuildingBlock BuildingBlock { get; }

        Dictionary<string, NameSpace> NameSpaces { get; }

        Dictionary<string, DataObjects.Port> Ports { get; }
        List<DataObjects.Port> PortsList { get; }
    }
}
