using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog
{
    public class Item
    {
        public string Name { get; set; }
        public Attribute Attribute { get; set; }
        public WordReference DefinitionRefrecnce { get; set; }

        public codeEditor.Data.Project Project { get; protected set; }

        public Dictionary<string, string> Properties = new Dictionary<string, string>();
        public ProjectProperty ProjectProperty
        {
            get
            {
                return Project.ProjectProperties[Plugin.StaticID] as ProjectProperty;
            }
        }
    }
}
