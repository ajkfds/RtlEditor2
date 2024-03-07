using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeEditorPlugin
{
    public class PulginManager
    {
        public List<IPlugin> LoadPlugIns(string path)
        {
            List<IPlugin> plugins = new List<IPlugin>();
            if (!System.IO.Directory.Exists(path)) return plugins;

            string[] files = System.IO.Directory.GetFiles(path);

            foreach (string filePath in files)
            {
                if (!filePath.EndsWith(".dll")) continue;
                var asms = System.Reflection.Assembly.LoadFrom(filePath);
                try
                {
                    foreach (var t in asms.GetTypes())
                    {
                        if (t.IsInterface) continue;
                        if (t.Name != "Plugin") continue;
                        IPlugin plugin = Activator.CreateInstance(t) as IPlugin;
                        if (plugin != null)
                        {
                            plugins.Add(plugin);
                        }
                    }
                }
                catch
                {

                }
            }
            return plugins;
        }

    }
}
