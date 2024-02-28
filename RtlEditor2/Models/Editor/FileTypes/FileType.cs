using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.Models.Editor.FileTypes
{
    public class FileType
    {
        public virtual string ID { get; protected set; }

        public virtual bool IsThisFileType ( string relativeFilePath, Data.Project project )
        {
            return false;
        }

        public virtual Data.File CreateFile(string relativeFilePath, Data.Project project)
        {
            System.Diagnostics.Debugger.Break();
            return null;
        }
    }
}
