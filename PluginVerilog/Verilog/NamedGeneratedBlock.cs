using pluginVerilog.Verilog.BuildingBlocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog
{
    public class NamedGeneratedBlock : NameSpace
    {
        private Dictionary<string, Function> functions = new Dictionary<string, Function>();
        private Dictionary<string, Task> tasks = new Dictionary<string, Task>();
        private Dictionary<string, ModuleItems.ModuleInstantiation> moduleInstantiations = new Dictionary<string, ModuleItems.ModuleInstantiation>();
        public Dictionary<string, Function> Functions { get { return functions; } }
        public Dictionary<string, Task> Tasks { get { return tasks; } }
        public Dictionary<string, ModuleItems.ModuleInstantiation> ModuleInstantiations { get { return moduleInstantiations; } }

        protected NamedGeneratedBlock(NameSpace parent) : base(parent.BuildingBlock, parent)
        {
        }

        public static NamedGeneratedBlock Create(NameSpace parent, string name)
        {
            NamedGeneratedBlock block = new NamedGeneratedBlock(parent);
            block.Name = name;
            return block;
        }

    }
}
