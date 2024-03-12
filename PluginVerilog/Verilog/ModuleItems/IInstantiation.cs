using pluginVerilog.Verilog.BuildingBlocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.ModuleItems
{
    public interface IInstantiation
    {
        string SourceName { get; }

        Dictionary<string, Expressions.Expression> ParameterOverrides { get; set; }
        Dictionary<string, Expressions.Expression> PortConnection { get; set; }
        string OverrideParameterID { get; }
        bool Prototype { get; set; }
        BuildingBlock GetInstancedBuildingBlock();

        // Item
        string Name { get; set; }
        Attribute Attribute { get; set; }
        WordReference DefinitionRefrecnce { get; set; }

        codeEditor.Data.Project Project { get; }


    }
}
