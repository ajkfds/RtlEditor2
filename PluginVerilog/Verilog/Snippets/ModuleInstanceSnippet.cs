using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using codeEditor.CodeEditor;
using System.Windows.Forms;
using System.Drawing;
using pluginVerilog.Verilog.BuildingBlocks;

namespace pluginVerilog.Verilog.Snippets
{
    public class ModuleInstanceSnippet : codeEditor.Snippets.InteractiveSnippet
    {
        public ModuleInstanceSnippet() : base("moduleInstance")
        {
        }

        private CodeDocument document;

        // initial value for {n}
        private List<string> initials = new List<string> {  };

        public override void Apply(CodeDocument codeDocument)
        {
            base.Apply(codeDocument);
            List<ToolItem> items = new List<ToolItem>();

            codeEditor.Data.Project project = codeDocument.TextFile.Project;
            ProjectProperty projectProperty = project.ProjectProperties[Plugin.StaticID] as ProjectProperty;

            List<string> moduleNmaes = projectProperty.GetModuleNameList();
            foreach(string moduleName in moduleNmaes)
            {
                items.Add(new ModuleInastnaceSelectionItem(moduleName,this));
            }

            codeEditor.Controller.CodeEditor.ForceOpenCustomSelection(ApplyTool, items);
        }

        public class ModuleInastnaceSelectionItem : codeEditor.CodeEditor.ToolItem
        {
            public ModuleInastnaceSelectionItem(string text, ModuleInstanceSnippet moduleInstanceSnippet) : base(text)
            {
                snippet = moduleInstanceSnippet;
            }

            private ModuleInstanceSnippet snippet;
            public override void Apply(CodeDocument codeDocument)
            {
                snippet.ApplyModuleInstance(codeDocument, Text);
            }
        }

        public void ApplyModuleInstance(CodeDocument codeDocument, string Text)
        {
            codeEditor.Data.Project project = codeDocument.TextFile.Project;

            ProjectProperty projectProperty = project.ProjectProperties[Plugin.StaticID] as ProjectProperty;

            Data.VerilogFile targetFile = projectProperty.GetFileOfBuildingblock(Text) as Data.VerilogFile;
            if (targetFile == null) return;

            string instanceName = Text + "_";
            {
                Data.IVerilogRelatedFile vfile = codeEditor.Controller.CodeEditor.GetTextFile() as Data.IVerilogRelatedFile;
                if (vfile == null) return;

                ParsedDocument parenetParsedDocument = vfile.VerilogParsedDocument;
                BuildingBlock module = parenetParsedDocument.GetBuidingBlockAt(vfile.CodeDocument.CaretIndex);

                int instanceCount = 0;
                while (module.Instantiations.ContainsKey(Text + "_" + instanceCount.ToString()))
                {
                    instanceCount++;
                }
                instanceName = Text + "_" + instanceCount.ToString();
            }

            Verilog.ParsedDocument targetParsedDocument = targetFile.ParsedDocument as Verilog.ParsedDocument;
            if (targetParsedDocument == null) return;

            Module targetModule = targetParsedDocument.Root.BuldingBlocks[Text] as Module;
            if (targetModule == null) return;

            string replaceText = getReplaceText(targetModule, instanceName);


            int index = codeDocument.CaretIndex;

            if (initials.Count == 0)
            {
                codeDocument.Replace(index, 0, 0, replaceText);
                codeEditor.Controller.CodeEditor.AbortInteractiveSnippet();
            }
            else
            {
                for (int i = 0; i < initials.Count; i++)
                {
                    string target = "{" + i.ToString() + "}";
                    if (!replaceText.Contains(target)) break;
                    startIndexs.Add(index + replaceText.IndexOf(target));
                    lastIndexs.Add(index + replaceText.IndexOf(target) + initials[i].Length - 1);
                    replaceText = replaceText.Replace(target, initials[i]);
                }

                codeDocument.Replace(index, 0, 0, replaceText);
                codeDocument.CaretIndex = startIndexs[0];
                codeDocument.SelectionStart = startIndexs[0];
                codeDocument.SelectionLast = lastIndexs[0] + 1;

                codeEditor.Controller.CodeEditor.ClearHighlight();
                for (int i = 0; i < startIndexs.Count; i++)
                {
                    codeEditor.Controller.CodeEditor.AppendHighlight(startIndexs[i], lastIndexs[i]);
                }
            }

            base.Apply(codeDocument);
        }


        public override void Aborted()
        {
            codeEditor.Controller.CodeEditor.ClearHighlight();
            document = null;
            base.Aborted();
        }

        private List<int> startIndexs = new List<int>();
        private List<int> lastIndexs = new List<int>();

        public override void BeforeKeyDown(object sender, KeyEventArgs e, codeEditor.CodeEditor.AutoCompleteForm autoCompleteForm)
        {
            // overrider return & escape
            if (autoCompleteForm == null || autoCompleteForm.Visible == false)
            {
                if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Escape)
                {
                    codeEditor.Controller.CodeEditor.AbortInteractiveSnippet();
                    e.Handled = true;
                }
            }
        }
        public override void AfterKeyDown(object sender, KeyEventArgs e, codeEditor.CodeEditor.AutoCompleteForm autoCompleteForm)
        {

        }
        public override void AfterAutoCompleteHandled(object sender, KeyEventArgs e, codeEditor.CodeEditor.AutoCompleteForm autoCompleteForm)
        {
            if (e.Handled) // closed
            {
               int i = codeEditor.Controller.CodeEditor.GetHighlightIndex(document.CaretIndex);
                switch (i)
                {
                    default:
                        codeEditor.Controller.CodeEditor.AbortInteractiveSnippet();
                        break;
                }
            }
        }

        private void moveToNextHighlight(out bool moved)
        {
            moved = false;
            int i = codeEditor.Controller.CodeEditor.GetHighlightIndex(document.CaretIndex);
            if (i == -1) return;
            i++;
            if (i >= initials.Count) return;

            codeEditor.Controller.CodeEditor.SelectHighlight(i);
            moved = true;
        }

        private void ApplyTool(object sender, EventArgs e)
        {

        }


        private string getReplaceText(Module module, string instanceName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(module.Name);
            sb.Append(" ");
            if (module.PortParameterNameList.Count > 0)
            {
                sb.Append("#(\r\n");
                bool first = true;
                foreach (string portName in module.PortParameterNameList)
                {
                    if (!first) sb.Append(",\r\n");
                    sb.Append("\t");
                    sb.Append(".");
                    sb.Append(portName);
                    sb.Append("\t( ");
                    sb.Append(" )");
                    first = false;
                }
                sb.Append("\r\n) ");
            }

            sb.Append("{0}");
            initials.Add(instanceName);

            sb.Append(" (\r\n");
            int j = 0;
            foreach (Verilog.DataObjects.Port port in module.Ports.Values)
            {
                sb.Append("\t.");
                sb.Append(port.Name);
                sb.Append("\t(  )");
                if (j != module.Ports.Count - 1) sb.Append(",");
                sb.Append("\r\n");
                j++;
            }
            sb.Append(");");

            return sb.ToString();
        }

    }
}
