using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginMarkdown.Data
{
    public class MarkdownFile : RtlEditor2.Data.TextFile
    {
        public static new MarkdownFile Create(string relativePath,RtlEditor2.Data.Project project)
        {
            //string id = GetID(relativePath, project);

            MarkdownFile fileItem = new MarkdownFile();
            fileItem.Project = project;
            fileItem.RelativePath = relativePath;
            if (relativePath.Contains('\\'))
            {
                fileItem.Name = relativePath.Substring(relativePath.LastIndexOf('\\') + 1);
            }
            else
            {
                fileItem.Name = relativePath;
            }

            return fileItem;
        }



        protected override RtlEditor2.NavigatePanel.NavigatePanelNode createNode()
        {
            return new RtlEditor2.NavigatePanel.TextFileNode(this);
        }

        public override RtlEditor2.CodeEditor.DocumentParser CreateDocumentParser( RtlEditor2.CodeEditor.DocumentParser.ParseModeEnum parseMode)
        {
            return new Parser.Parser(this, parseMode);
        }


    }
}
