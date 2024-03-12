using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.BuildingBlocks
{
    public interface IBuildingBlock
    {
        // NameSpace
        Dictionary<string, DataObjects.DataObject> DataObjects { get; }
        NameSpace Parent { get; }
        Dictionary<string, DataObjects.Constants.Constants> Constants { get; }
        Dictionary<string, NameSpace> NameSpaces { get; }
        NameSpace GetHierNameSpace(int index);
        DataObjects.DataObject GetDataObject(string identifier);
        DataObjects.Constants.Constants GetConstants(string identifier);

        // Bulding Block
        Dictionary<string, Function> Functions { get; }
        Dictionary<string, Task> Tasks { get; }
        Dictionary<string, Class> Classes { get; }
        Dictionary<string, BuildingBlock> Elements { get; }
    }
}
