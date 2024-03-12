using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using codeEditor.CodeEditor;
using System.Windows.Forms;
using pluginVerilog.Verilog.BuildingBlocks;

namespace pluginVerilog.Verilog.Snippets
{
    public class PortNetCreateSnippet : codeEditor.CodeEditor.ToolItem
    {
        public PortNetCreateSnippet() : base("portNetCreate")
        {
        }

        public override void Apply(CodeDocument codeDocument)
        {
            codeEditor.Data.ITextFile itext = codeEditor.Controller.CodeEditor.GetTextFile();

            if (!(itext is Data.IVerilogRelatedFile)) return;
            var vfile = itext as Data.IVerilogRelatedFile;
            ParsedDocument parsedDocument = vfile.VerilogParsedDocument;
            if (parsedDocument == null) return;

            int index = codeDocument.CaretIndex;
            BuildingBlock module = parsedDocument.GetBuidingBlockAt(index);

        }

    }
}

