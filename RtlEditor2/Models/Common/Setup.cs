using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.Models.Common
{
    public class Setup
    {

        public void SaveSetup(string path)
        {
            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(path))
            {
                using (ajkControls.Json.JsonWriter writer = new ajkControls.Json.JsonWriter(sw))
                {
                    writeJson(writer);
                }
            }
        }

        public void LoadSetup(string path)
        {
            using (System.IO.StreamReader sr = new System.IO.StreamReader(path))
            {
                using (ajkControls.Json.JsonReader reader = new ajkControls.Json.JsonReader(sr))
                {
                    readJson(reader);
                }
            }
        }

        private void readJson(ajkControls.Json.JsonReader reader)
        {
            while (true)
            {
                string key = reader.GetNextKey();
                if (key == null) break;

                switch (key)
                {
                    case "codeEditor":
                        readCodeEditorSetup(reader);
                        break;
                    case "PluginSetups":
                        readPluginSetup(reader);
                        break;
                    case "Projects":
                        readProjects(reader);
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }
        }

        private void readCodeEditorSetup(ajkControls.Json.JsonReader jsonReader)
        {
            using (var reader = jsonReader.GetNextObjectReader())
            {
                while (true)
                {
                    string key = reader.GetNextKey();
                    if (key == null) break;

                    switch (key)
                    {
                        case "ApplicationName":
                            string applicationName = reader.GetNextStringValue();
                            if (applicationName != "codeEditor") throw new Exception("illegal format");
                            break;
                        case "LastUpdate":
                            string lastUpdate = reader.GetNextStringValue();
                            break;
                        default:
                            reader.SkipValue();
                            break;
                    }
                }
            }
        }

        private void readPluginSetup(ajkControls.Json.JsonReader jsonReader)
        {
            using (var reader = jsonReader.GetNextObjectReader())
            {
                while (true)
                {
                    string key = reader.GetNextKey();
                    if (key == null) break;

                    //if (Global.PluginSetups.ContainsKey(key))
                    //{
                    //    using (var block = reader.GetNextObjectReader())
                    //    {
                    //        Global.PluginSetups[key].ReadJson(block);
                    //    }
                    //}
                    //else
                    {
                        reader.SkipValue();
                    }
                }
            }
        }

        private void readProjects(ajkControls.Json.JsonReader jsonReader)
        {
            using (var reader = jsonReader.GetNextObjectReader())
            {
                while (true)
                {
                    string key = reader.GetNextKey();
                    if (key == null) break;

                    if (Global.Projects.ContainsKey(key))
                    {
                        Global.Projects[key].LoadSetup(reader);
                    }
                    else
                    {
                        Editor.Data.Project project = Editor.Data.Project.Create(reader);
                        Controller.AddProject(project);
                    }
                }
            }
        }


        private void writeJson(ajkControls.Json.JsonWriter writer)
        {
            using (var blockWriter = writer.GetObjectWriter("codeEditor"))
            {
                blockWriter.writeKeyValue("ApplicationName", "codeEditor");
                blockWriter.writeKeyValue("LastUpdate", DateTime.Now.ToString());
            }

            //using (var blockWriter = writer.GetObjectWriter("PluginSetups"))
            //{
            //    foreach (var pluginKvp in Global.PluginSetups)
            //    {
            //        using (var pluginWriter = blockWriter.GetObjectWriter(pluginKvp.Key))
            //        {
            //            pluginKvp.Value.SaveSetup(pluginWriter);
            //        }
            //    }
            //}

            using (var blockWriter = writer.GetObjectWriter("Projects"))
            {
                foreach (var projectKvp in Global.Projects)
                {
                    using (var projectWriter = blockWriter.GetObjectWriter(projectKvp.Key))
                    {
                        projectKvp.Value.SaveSetup(projectWriter);
                    }
                }
            }

        }
    }
}
