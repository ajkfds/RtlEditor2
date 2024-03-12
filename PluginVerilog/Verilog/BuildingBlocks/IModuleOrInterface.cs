using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.BuildingBlocks
{
    public interface IModuleOrInterface : IModuleOrInterfaceOrProgram
    {
        Dictionary<string, ModuleItems.IInstantiation> Instantiations { get; }
    }
}
