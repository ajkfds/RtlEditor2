using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.FileTypes
{
    public class SystemVerilogFile : codeEditor.FileTypes.FileType
    {
        public override string ID { get { return "SystemVerilogFile"; } }

        public override bool IsThisFileType(string relativeFilePath, codeEditor.Data.Project project)
        {
            if (
                relativeFilePath.ToLower().EndsWith(".sv")
            )
            {
                return true;
            }
            return false;
        }

        public override codeEditor.Data.File CreateFile(string relativeFilePath, codeEditor.Data.Project project)
        {
            return Data.VerilogFile.CreateSystemVerilog(relativeFilePath, project);
        }
    }
}
