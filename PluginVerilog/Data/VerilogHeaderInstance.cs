using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using codeEditor.CodeEditor;
using codeEditor.Data;

namespace pluginVerilog.Data
{
    public class VerilogHeaderInstance : InstanceTextFile, IVerilogRelatedFile
    {
        protected VerilogHeaderInstance(codeEditor.Data.TextFile sourceTextFile) : base(sourceTextFile)
        {

        }

        public static VerilogHeaderInstance Create(
            string relativePath,
            IVerilogRelatedFile parentFile,
            codeEditor.Data.Project project,
            string id)
        {
            ProjectProperty projectPropery = project.ProjectProperties[Plugin.StaticID] as ProjectProperty;
            codeEditor.Data.Item fileItem = project.GetItem(relativePath);
            if ((fileItem as VerilogHeaderFile) == null) return null;

            VerilogHeaderInstance instance = new VerilogHeaderInstance(fileItem as VerilogHeaderFile);
            instance.Project = project;
            instance.RelativePath = relativePath;
            if (instance.RelativePath.Contains('\\'))
            {
                instance.Name = relativePath.Substring(relativePath.LastIndexOf('\\') + 1);
            }
            else
            {
                instance.Name = relativePath;
            }
            instance.id = id;
            instance.RootFile = parentFile;

            return instance;
        }
        public IVerilogRelatedFile RootFile { get; protected set; }

        private string id;
        public override string ID
        {
            get
            {
                return id;
            }
        }

        public void SetName(string name)
        {
            this.Name = name;
        }

        public bool ReplaceBy(
            VerilogHeaderInstance file
            //Verilog.ModuleItems.ModuleInstantiation moduleInstantiation,
            //codeEditor.Data.Project project
            )
        {
            //ProjectProperty projectPropery = project.ProjectProperties[Plugin.StaticID] as ProjectProperty;
            //Data.IVerilogRelatedFile file = projectPropery.GetFileOfModule(moduleInstantiation.ModuleName);
            if (file == null) return false;
            if (!IsSameAs(file as File)) return false;
            //if (Project != project) return false;
            //if (ModuleName != moduleInstantiation.ModuleName) return false;

            ParsedDocument = file.ParsedDocument;


            return true;
        }

        public override codeEditor.CodeEditor.CodeDocument CodeDocument
        {
            get
            {
                if (SourceVerilogFile == null) return null;
                return SourceVerilogFile.CodeDocument;
            }
        }
        public override void Dispose()
        {
            disposeItems();
        }

        private void disposeItems()
        {
            if (ParsedDocument != null)// && ParameterOverrides.Count != 0)
            {
                foreach (var incFile in VerilogParsedDocument.IncludeFiles.Values)
                {
                    incFile.Dispose();
                }
            }
            parsedDocument = null;
            //SourceVerilogFile.RemoveModuleInstance(this);
        }

        public string ModuleName { set; get; }


//        public Dictionary<string, Verilog.Expressions.Expression> ParameterOverrides;
        public string ParameterId
        {
            get
            {
                return "";
                //StringBuilder sb = new StringBuilder();
                //foreach (var kvp in ParameterOverrides)
                //{
                //    sb.Append(kvp.Key);
                //    sb.Append("=");
                //    sb.Append(kvp.Value.Value.ToString());
                //    sb.Append(",");
                //}
                //return sb.ToString();
            }
        }

        private Data.VerilogHeaderFile SourceVerilogFile
        {
            get
            {
                return SourceTextFile as VerilogHeaderFile;
            }
        }



        public override void Close()
        {
            if (VerilogParsedDocument != null) VerilogParsedDocument.ReloadIncludeFiles();
            SourceVerilogFile.Close();
        }

        private codeEditor.CodeEditor.ParsedDocument parsedDocument = null;

        public override codeEditor.CodeEditor.ParsedDocument ParsedDocument
        {
            get
            {
                return parsedDocument;
            }
            set
            {
                parsedDocument = value;
            }
        }
        public override void Save()
        {
            if (CodeDocument == null) return;

            SourceTextFile.Save();
        }

        public override DateTime? LoadedFileLastWriteTime
        {
            get
            {
                return SourceTextFile.LoadedFileLastWriteTime;
            }
        }

        public Verilog.ParsedDocument VerilogParsedDocument
        {
            get
            {
                return ParsedDocument as Verilog.ParsedDocument;
            }
        }

        public override void AcceptParsedDocument(ParsedDocument newParsedDocument)
        {
            Verilog.ParsedDocument vParsedDocument = newParsedDocument as Verilog.ParsedDocument;
            parsedDocument = vParsedDocument;

            //{
            //    Data.VerilogFile source = SourceVerilogFile;
            //    if (source == null) return;
            //    source.RegisterInstanceParsedDocument(ParameterId, newParsedDocument, this);
            //}
            ReparseRequested = vParsedDocument.ReparseRequested;
            Update();
        }



        public ProjectProperty ProjectProperty
        {
            get
            {
                return Project.ProjectProperties[Plugin.StaticID] as ProjectProperty;
            }
        }




        public override ajkControls.CodeTextbox.CodeDrawStyle DrawStyle
        {
            get
            {
                return Global.CodeDrawStyle;
            }
        }

        protected override codeEditor.NavigatePanel.NavigatePanelNode createNode()
        {
            NavigatePanel.VerilogHeaderInstanceNode node = new NavigatePanel.VerilogHeaderInstanceNode(this,Project);
            nodeRef = new WeakReference<codeEditor.NavigatePanel.NavigatePanelNode>(node);
            return node;
        }

        public override codeEditor.CodeEditor.DocumentParser CreateDocumentParser(codeEditor.CodeEditor.DocumentParser.ParseModeEnum parseMode)
        {
            Data.IVerilogRelatedFile parentFile = Parent as Data.IVerilogRelatedFile;
            if (parentFile == null) return null;
            // do not parse again for background parse. header file is parsed with parent file.
            if (parseMode != DocumentParser.ParseModeEnum.EditParse) return null;

            // Use Parent File Parser for Edit Parse
            return parentFile.CreateDocumentParser(parseMode);
        }

        // update sub-items from ParsedDocument
        public override void Update()
        {
            VerilogCommon.Updater.Update(this);
        }

        // Auto Complete Handler

        public override void AfterKeyDown(System.Windows.Forms.KeyEventArgs e)
        {
            VerilogCommon.AutoComplete.AfterKeyDown(this, e);
        }

        public override void AfterKeyPressed(System.Windows.Forms.KeyPressEventArgs e)
        {
            VerilogCommon.AutoComplete.AfterKeyPressed(this, e);
        }

        public override void BeforeKeyPressed(System.Windows.Forms.KeyPressEventArgs e)
        {
            VerilogCommon.AutoComplete.BeforeKeyPressed(this, e);
        }

        public override void BeforeKeyDown(System.Windows.Forms.KeyEventArgs e)
        {
            VerilogCommon.AutoComplete.BeforeKeyDown(this, e);
        }

        public override List<codeEditor.CodeEditor.PopupItem> GetPopupItems(ulong version, int index)
        {
            return VerilogCommon.AutoComplete.GetPopupItems(this,VerilogParsedDocument, version, index);
        }

        public override List<codeEditor.CodeEditor.ToolItem> GetToolItems(int index)
        {
            return VerilogCommon.AutoComplete.GetToolItems(this, index);
        }

        public override List<codeEditor.CodeEditor.AutocompleteItem> GetAutoCompleteItems(int index, out string cantidateWord)
        {
            return VerilogCommon.AutoComplete.GetAutoCompleteItems(this,VerilogParsedDocument, index, out cantidateWord);
        }



    }
}
