using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Data
{
    public interface IVerilogRelatedFile : codeEditor.Data.ITextFile
    {

        Verilog.ParsedDocument VerilogParsedDocument { get; }

        ProjectProperty ProjectProperty { get; }

    }
}
