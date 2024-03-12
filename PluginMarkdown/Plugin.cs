using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginMarkdown
{
    public class Plugin : CodeEditorPlugin.IPlugin
    {
        public bool Register()
        {
            // register filetype
            FileTypes.MarkdownFile fileType = new FileTypes.MarkdownFile();
            RtlEditor2.Global.FileTypes.Add(fileType.ID, fileType);
            return true;
        }

        public bool Initialize()
        {
            return true;
        }

        public string Id { get { return "Markdown"; } }
    }
}
