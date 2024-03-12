using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog
{
    public class ModuleReferenceX
    {
        public string Name;
        public Dictionary<string, string> Parameters = new Dictionary<string, string>();

        public string ID
        {
            get
            {
                if (Parameters.Count == 0) return Name;
                StringBuilder sb = new StringBuilder();
                sb.Append(Name);
                sb.Append(":");
                bool first = true;
                foreach(var pair in Parameters)
                {
                    if(first)
                    {
                        sb.Append(",");
                        first = false;
                    }
                    sb.Append(pair.Key);
                    sb.Append("=");
                    sb.Append(pair.Value);
                }
                return sb.ToString();
            }
        }
    }
}
