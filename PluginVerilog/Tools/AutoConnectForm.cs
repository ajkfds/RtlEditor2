using pluginVerilog.Verilog.BuildingBlocks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pluginVerilog.Tools
{
    public partial class AutoConnectForm : Form
    {
        public AutoConnectForm()
        {
            InitializeComponent();

            codeEditor.Data.ITextFile textFile = codeEditor.Controller.CodeEditor.GetTextFile();
            if (textFile == null) return;

            verilogRelatedFile = textFile as Data.IVerilogRelatedFile;
            if (verilogRelatedFile == null) return;

            parsedDocument = verilogRelatedFile.VerilogParsedDocument;
            if (parsedDocument == null) return;

            codeDocument = textFile.CodeDocument;
            if (codeDocument == null) return;

            project = textFile.Project;
            if (project == null) return;

            int index = codeDocument.CaretIndex;
            module = parsedDocument.GetBuidingBlockAt(index);


            //foreach (var inst in
            //    module.ModuleInstantiations.Values)
            //{
            //    if (inst.BeginIndex < index && index < inst.LastIndex)
            //    {
            //        moduleInstantiation = inst;
            //        setupModule();
            //        return;
            //    }
            //}
        }

        Data.IVerilogRelatedFile verilogRelatedFile;
        Verilog.ParsedDocument parsedDocument;
        codeEditor.CodeEditor.CodeDocument codeDocument;
        codeEditor.Data.Project project;
        BuildingBlock module;
        Verilog.ModuleItems.ModuleInstantiation moduleInstantiation;

        private void setupModule()
        {
            Data.VerilogFile source = parsedDocument.ProjectProperty.GetFileOfBuildingblock(moduleInstantiation.SourceName) as Data.VerilogFile;
            if (source == null) return;

            Verilog.ParsedDocument sourceParsedDocument;
            if (moduleInstantiation.ParameterOverrides.Count == 0)
            {
                sourceParsedDocument = source.VerilogParsedDocument;
            }
            else
            {
                sourceParsedDocument = source.GetInstancedParsedDocument(moduleInstantiation.OverrideParameterID) as Verilog.ParsedDocument;
            }
            if (sourceParsedDocument == null) return;
            Module sourceModule = sourceParsedDocument.Root.BuldingBlocks[moduleInstantiation.SourceName] as Module;
            if (sourceModule == null) return;

            BuildingBlock instancedModule = parsedDocument.ProjectProperty.GetBuildingBlock(moduleInstantiation.SourceName);

            HeaderLabel header = new HeaderLabel();
            header.AppendText(moduleInstantiation.SourceName, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
            header.AppendText(" ");
            header.AppendText(moduleInstantiation.Name);
            header.AppendText("(");
            colorLabelList.Add(header);

            string sectionName = null;
            foreach (var port in sourceModule.PortsList)
            {
                if (port.SectionName != sectionName)
                {
                    sectionName = port.SectionName;
                    SectionLabel section = new SectionLabel();
                    section.AppendText("// ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Comment));
                    section.AppendText(sectionName, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Comment));
                    colorLabelList.Add(section);
                }

                PortLabel portLabel;
                if (moduleInstantiation.PortConnection.ContainsKey(port.Name))
                {
                    portLabel = new PortLabel(port.Name, moduleInstantiation.PortConnection[port.Name].GetLabel());
                }
                else
                {
                    portLabel = new PortLabel(port.Name,null);
                }
                colorLabelList.Add(portLabel);
            }

            FooterLabel footer = new FooterLabel();
            footer.AppendText(");");
            colorLabelList.Add(footer);

            checkConnectCantidate(colorLabelList, module, instancedModule);
        }

        private static void checkConnectCantidate(ajkControls.ColorLabel.ColorLabelList labelList, BuildingBlock module, BuildingBlock instancedModule)
        {
            foreach(ajkControls.ColorLabel.ColorLabel label in labelList)
            {
                if( label is PortLabel )
                {
                    PortLabel portLabel = label as PortLabel;
                    string portName = portLabel.PortName.ToLower();

                    int matchLength = 0;
                    foreach(var variable in module.DataObjects.Values)
                    {
                        string valName = variable.Name.ToLower();
                        int i, l;
                        searchMatch(portName, valName, out i, out l);
                        if(l > matchLength)
                        {
                            matchLength = l;

                            ajkControls.ColorLabel.ColorLabel cantidate = new ajkControls.ColorLabel.ColorLabel();
                            if(i != 0)
                            {
                                cantidate.AppendText(variable.Name.Substring(0,i), Color.LightGray);
                            }
                            cantidate.AppendText(variable.Name.Substring(i, l), Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Variable));
                            if(i+l < variable.Name.Length)
                            {
                                cantidate.AppendText(variable.Name.Substring(i+l), Color.LightGray);
                            }
                            portLabel.Cantidate = cantidate;
                        }
                    }

                    if (portLabel.Cantidate != null) portLabel.Update();
                }
            }

        }

        private static void searchMatch(string target,string search,out int matchIndex,out int matchLength)
        {
            matchIndex = 0;
            matchLength = 0;

            for(int start = 0; start < search.Length; start++)
            {
                for(int l = search.Length-start; l > 0; l--)
                {
                    if (l < matchLength) break;

                    string partialSearch = search.Substring(start, l);
                    if (!target.Contains(partialSearch)) continue;
                    if (l > matchLength)
                    {
                        matchLength = l;
                        matchIndex = start;
                    }
                }
            }
        }


        private void AutoConnectForm_Load(object sender, EventArgs e)
        {

        }

        private class HeaderLabel : ajkControls.ColorLabel.ColorLabel
        {

        }

        private class SectionLabel : ajkControls.ColorLabel.ColorLabel
        {

        }

        private class PortLabel : ajkControls.ColorLabel.ColorLabel
        {
            public PortLabel(string portName, ajkControls.ColorLabel.ColorLabel defaultConnection)
            {
                PortName = portName;
                DefaultConnection = defaultConnection;
            }
            public void Update()
            {
                Clear();
                AppendText(".");
                AppendText(PortName, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Identifier));
                AppendText(" ( ");
                if (ApplyCantidate)
                {
                    if (Cantidate != null) AppendLabel(Cantidate);
                }
                else
                {
                    if (DefaultConnection != null) AppendLabel(DefaultConnection);
                }
                AppendText(" )");

                if(!ApplyCantidate && Cantidate != null)
                {
                    AppendText(" -> ");
                    AppendLabel(Cantidate);
                }
            }

            public string PortName;
            public ajkControls.ColorLabel.ColorLabel Cantidate;
            public ajkControls.ColorLabel.ColorLabel DefaultConnection;
            public bool ApplyCantidate = false;
        }

        private class FooterLabel : ajkControls.ColorLabel.ColorLabel
        {

        }

        private void colorLabelList_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Enter)
            {
                writeModuleInstance();
                Hide();
            }
            else if(e.KeyCode == Keys.Escape)
            {
                Hide();
            }
        }

        private void colorLabelList_ColorLabelClicked(ajkControls.ColorLabel.ColorLabel colorLabel)
        {
            if(colorLabel is PortLabel)
            {
                PortLabel portLabel = colorLabel as PortLabel;
                if (portLabel.ApplyCantidate)
                {
                    portLabel.ApplyCantidate = false;
                }else
                {
                    if (portLabel.Cantidate != null) portLabel.ApplyCantidate = true;
                }
                portLabel.Update();
                colorLabelList.Refresh();
            }
        }

        private void writeModuleInstance()
        {
            foreach(var label in colorLabelList)
            {
                if (!(label is PortLabel)) continue;
                PortLabel portLabel = label as PortLabel;
                if (!portLabel.ApplyCantidate) continue;

                string connection = portLabel.Cantidate.CreateString();
                moduleInstantiation.PortConnection[portLabel.PortName] = Verilog.Expressions.Expression.CreateTempExpression(connection);
            }


            int index = codeDocument.CaretIndex;
            string indent = (codeDocument as CodeEditor.CodeDocument).GetIndentString(index);

            //codeDocument.CaretIndex = moduleInstantiation.BeginIndex;
            //codeDocument.Replace(
            //    moduleInstantiation.BeginIndex,
            //    moduleInstantiation.LastIndex - moduleInstantiation.BeginIndex + 1,
            //    0,
            //    moduleInstantiation.CreateSrting("\t")
            //    );
            //codeDocument.SelectionStart = codeDocument.CaretIndex;
            //codeDocument.SelectionLast = codeDocument.CaretIndex;
        }

    }
}
