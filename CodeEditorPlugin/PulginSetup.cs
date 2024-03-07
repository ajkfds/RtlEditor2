using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeEditorPlugin
{
    public class PluginSetup
    {
        public void SaveSetup(AjkAvaloniaLibs.Libs.Json.JsonWriter writer)
        {

        }

        public void ReadJson(AjkAvaloniaLibs.Libs.Json.JsonReader reader)
        {
            while (true)
            {
                string key = reader.GetNextKey();
                if (key == null) break;
                reader.SkipValue();
            }

        }
    }
}
