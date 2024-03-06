using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.FileTypes
{
    public class FileAttribute
    {
        public static bool HasThisAttribule()
        {
            return false;
        }

        public string Filter;
        public void ReadJson(ajkControls.Json.JsonReader reader)
        {
            while (true)
            {
                string key = reader.GetNextKey();
                if (key == null) break;

                switch (key)
                {
                    case "Filter":
                        Filter = reader.GetNextStringValue();
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }
        }
        public void WriteJson(ajkControls.Json.JsonWriter writer)
        {
            using (var blockWriter = writer.GetObjectWriter("FileAttribute"))
            {
                blockWriter.writeKeyValue("Filter", Filter);
            }
        }
    }
}
