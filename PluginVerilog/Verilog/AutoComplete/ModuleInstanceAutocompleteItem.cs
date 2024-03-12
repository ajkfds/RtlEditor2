using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using pluginVerilog.Verilog.BuildingBlocks;

namespace pluginVerilog.Verilog.AutoComplete
{
    public class ModuleInstanceAutocompleteItem : codeEditor.CodeEditor.AutocompleteItem
    {
        public ModuleInstanceAutocompleteItem(string text, byte colorIndex, Color color, codeEditor.Data.Project project) : base(text, colorIndex, color)
        {
            this.project = project;
        }
        codeEditor.Data.Project project;

        public override void Apply(codeEditor.CodeEditor.CodeDocument codeDocument, System.Windows.Forms.KeyEventArgs e)
        {
            int prevIndex = codeDocument.CaretIndex;
            if (codeDocument.GetLineStartIndex(codeDocument.GetLineAt(prevIndex)) != prevIndex && prevIndex != 0)
            {
                prevIndex--;
            }
            int headIndex, length;
            codeDocument.GetWord(prevIndex, out headIndex, out length);

            char currentChar = codeDocument.GetCharAt(codeDocument.CaretIndex);
            if (currentChar != '\r' && currentChar != '\n') return;

            ProjectProperty projectProperty = project.ProjectProperties[Plugin.StaticID] as ProjectProperty;

            codeEditor.Data.ITextFile itext = codeEditor.Controller.CodeEditor.GetTextFile();

            if (!(itext is Data.IVerilogRelatedFile)) return;
            var vfile = itext as Data.IVerilogRelatedFile;
            ParsedDocument parsedDocument = vfile.VerilogParsedDocument;
            if (parsedDocument == null) return;

            BuildingBlock module = parsedDocument.GetBuidingBlockAt(vfile.CodeDocument.GetLineStartIndex(vfile.CodeDocument.GetLineAt(vfile.CodeDocument.CaretIndex)));
            if (module == null) return;

            Data.VerilogFile instancedFile = projectProperty.GetFileOfBuildingblock(Text) as Data.VerilogFile;
            if (instancedFile == null) return;
            Verilog.ParsedDocument instancedParsedDocument = instancedFile.ParsedDocument as Verilog.ParsedDocument;
            if (instancedParsedDocument == null) return;
            Module instancedModule = instancedParsedDocument.Root.BuldingBlocks[Text] as Module;
            if (instancedModule == null) return;

            string instanceName;
            int i = 0;
            while (true)
            {
                instanceName = Text + "_" + i.ToString();
                if (!module.Instantiations.ContainsKey(instanceName)) break;
                i++;
            }

            // create code
            StringBuilder sb = new StringBuilder();

            // modulename
            sb.Append(" ");

            // parameters
            if (instancedModule.PortParameterNameList.Count > 0)
            {
                sb.Append("#(\r\n");
                bool first = true;
                foreach (string portName in instancedModule.PortParameterNameList)
                {
                    if (!first) sb.Append(",\r\n");
                    sb.Append("\t");
                    sb.Append(".");
                    sb.Append(portName);
                    sb.Append("\t(  )");
                    first = false;
                }
                sb.Append("\r\n) ");
            }

            int carletOffset = Text.Length + sb.Length;
            sb.Append(instanceName);
            sb.Append(" (\r\n");

            // ports
            i = 0;
            foreach (Verilog.DataObjects.Port port in instancedModule.Ports.Values)
            {
                sb.Append("\t.");
                sb.Append(port.Name);
                sb.Append("\t(  )");
                if (i != instancedModule.Ports.Count - 1) sb.Append(",");
                sb.Append("\r\n");
                i++;
            }
            sb.Append(");");

            codeDocument.Replace(headIndex, length, ColorIndex, Text + sb.ToString());
            codeDocument.CaretIndex = headIndex + carletOffset + instanceName.Length;
            codeDocument.SelectionStart = headIndex + carletOffset;
            codeDocument.SelectionLast = headIndex + carletOffset + instanceName.Length;

            e.Handled = true;

            codeEditor.Controller.CodeEditor.RequestReparse();
        }


    }
}
