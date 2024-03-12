using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginMarkdown.FileTypes
{
    public class MarkdownFile : RtlEditor2.FileTypes.FileType
    {
        public override string ID { get => "MarkdownFile"; }

        public override bool IsThisFileType(string relativeFilePath, RtlEditor2.Data.Project project)
        {
            if (
                relativeFilePath.ToLower().EndsWith(".md")
            )
            {
                return true;
            }
            return false;
        }

        public override RtlEditor2.Data.File CreateFile(string relativeFilePath, RtlEditor2.Data.Project project)
        {
            return Data.MarkdownFile.Create(relativeFilePath, project);
        }
    }

}
