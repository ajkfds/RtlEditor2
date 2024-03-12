using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Data
{
    public class InstanceTextFile : codeEditor.Data.TextFile
    {
        public InstanceTextFile(codeEditor.Data.TextFile sourceTextFile)
        {
            sourceFileRef = new WeakReference<codeEditor.Data.TextFile>(sourceTextFile);
        }

        private System.WeakReference<codeEditor.Data.TextFile> sourceFileRef;
        public codeEditor.Data.TextFile SourceTextFile
        {
            get
            {
                codeEditor.Data.TextFile ret;
                if (!sourceFileRef.TryGetTarget(out ret)) return null;
                return ret;
            }
        }
    }
}
