using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.FileTypes
{
    public class TextFile : FileType
    {
        public override string ID { get { return "TextFile"; } }

        public override bool IsThisFileType(string relativeFilePath, Data.Project project)
        {
            if (
                relativeFilePath.ToLower().EndsWith(".txt") ||
                relativeFilePath.EndsWith(".text")
            )
            {
                return true;
            }
            return false;
        }

        public override Data.File CreateFile(string relativeFilePath, Data.Project project)
        {
            return Data.TextFile.Create(relativeFilePath, project);
        }
    }
}
