using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RtlEditor2.Data;

namespace RtlEditor2.FileTypes
{
    public class FileType
    {
        public virtual string ID { get; protected set; }

        public virtual bool IsThisFileType(string relativeFilePath, Project project)
        {
            return false;
        }

        public virtual File CreateFile(string relativeFilePath, Project project)
        {
            System.Diagnostics.Debugger.Break();
            return null;
        }
    }
}
