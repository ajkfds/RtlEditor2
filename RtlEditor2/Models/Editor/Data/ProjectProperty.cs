using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.Models.Editor.Data
{
    public class ProjectProperty
    {
        public virtual void SaveSetup(ajkControls.Json.JsonWriter writer)
        {

        }

        public virtual void LoadSetup(ajkControls.Json.JsonReader jsonReader)
        {
            using (var reader = jsonReader.GetNextObjectReader())
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
}
