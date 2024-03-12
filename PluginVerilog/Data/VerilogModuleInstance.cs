using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using codeEditor.CodeEditor;
using codeEditor.Data;

namespace pluginVerilog.Data
{
    public class VerilogModuleInstance : InstanceTextFile, IVerilogRelatedFile
    {
        protected VerilogModuleInstance(codeEditor.Data.TextFile sourceTextFile) : base(sourceTextFile)
        {

        }
        public static VerilogModuleInstance Create(
            Verilog.ModuleItems.ModuleInstantiation moduleInstantiation,
            codeEditor.Data.Project project
            )
        {
            ProjectProperty projectPropery = project.ProjectProperties[Plugin.StaticID] as ProjectProperty;
            Data.IVerilogRelatedFile file = projectPropery.GetFileOfBuildingblock(moduleInstantiation.SourceName);
            if (file == null) return null;

            VerilogModuleInstance fileItem = new VerilogModuleInstance(file as codeEditor.Data.TextFile);
            fileItem.ParameterOverrides = moduleInstantiation.ParameterOverrides;
            fileItem.Project = project;
            fileItem.RelativePath = file.RelativePath;
            fileItem.Name = moduleInstantiation.Name;
            fileItem.ModuleName = moduleInstantiation.SourceName;
            //            fileItem.ParseRequested = true;

            if (file is Data.VerilogFile)
            {
                Data.VerilogFile vfile = file as Data.VerilogFile;
                vfile.RegisterModuleInstance(fileItem);
            }

            return fileItem;
        }

        public override string ID
        {
            get
            {
                if (ParameterId == "")
                {
                    return RelativePath + ":" + ModuleName;
                }
                else
                {
                    return RelativePath + ":" + ModuleName + ":" + ParameterId;
                }
            }
        }

        public bool ReplaceBy(
            Verilog.ModuleItems.ModuleInstantiation moduleInstantiation,
            codeEditor.Data.Project project
            )
        {
            ProjectProperty projectPropery = project.ProjectProperties[Plugin.StaticID] as ProjectProperty;
            Data.IVerilogRelatedFile file = projectPropery.GetFileOfBuildingblock(moduleInstantiation.SourceName);
            if (file == null) return false;
            if (!IsSameAs(file as File)) return false;
            if (Project != project) return false;
            if (ModuleName != moduleInstantiation.SourceName) return false;

            if (ParameterId == moduleInstantiation.OverrideParameterID) return true;

            // re-register
            disposeItems();

            ParameterOverrides = moduleInstantiation.ParameterOverrides;

            if (file is Data.VerilogFile)
            {
                Data.VerilogFile vfile = file as Data.VerilogFile;
                vfile.RegisterModuleInstance(this);
            }

            return true;
        }

        public override codeEditor.CodeEditor.CodeDocument CodeDocument
        {
            get
            {
                return SourceVerilogFile.CodeDocument;
            }
        }
        public override void Dispose()
        {
            disposeItems();
        }

        private void disposeItems()
        {
            if (ParsedDocument != null && ParameterOverrides.Count != 0)
            {
                foreach (var incFile in VerilogParsedDocument.IncludeFiles.Values)
                {
                    incFile.Dispose();
                }
            }
            parsedDocument = null;
            SourceVerilogFile.RemoveModuleInstance(this);
        }

        public string ModuleName { set; get; }


        public Dictionary<string, Verilog.Expressions.Expression> ParameterOverrides;
        public string ParameterId {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (var kvp in ParameterOverrides)
                {
                    sb.Append(kvp.Key);
                    sb.Append("=");
                    sb.Append(kvp.Value.Value.ToString());
                    sb.Append(",");
                }
                return sb.ToString();
            }
        }

        private Data.VerilogFile SourceVerilogFile
        {
            get
            {
                return SourceTextFile as VerilogFile;
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
                if (parsedDocument == null)
                {
                    if (ParameterOverrides.Count == 0)
                    {
                        Data.VerilogFile file = SourceVerilogFile;
                        if (file == null) return null;
                        parsedDocument = file.ParsedDocument;
                    }
                    else
                    {
                        Data.VerilogFile source = SourceVerilogFile;
                        parsedDocument = source.GetInstancedParsedDocument(ParameterId);
                    }
                }

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

            {
                Data.VerilogFile source = SourceVerilogFile;
                if (source == null) return;
                source.RegisterInstanceParsedDocument(ModuleName+":"+ ParameterId, newParsedDocument, this);
            }
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
            NavigatePanel.VerilogModuleInstanceNode node = new NavigatePanel.VerilogModuleInstanceNode(this);
            nodeRef = new WeakReference<codeEditor.NavigatePanel.NavigatePanelNode>(node);
            return node;
        }

        public override codeEditor.CodeEditor.DocumentParser CreateDocumentParser(codeEditor.CodeEditor.DocumentParser.ParseModeEnum parseMode)
        {
            return new Parser.VerilogParser(this, ModuleName, ParameterOverrides, parseMode);
            //if (ParameterOverrides.Count == 0)
            //{
            //    return new Parser.VerilogParser(this, parseMode);
            //}
            //else
            //{
            //    return new Parser.VerilogParser(this, ModuleName, ParameterOverrides, parseMode);
            //}
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
            return VerilogCommon.AutoComplete.GetPopupItems(this, VerilogParsedDocument, version, index);
        }

        public override List<codeEditor.CodeEditor.ToolItem> GetToolItems(int index)
        {
            return VerilogCommon.AutoComplete.GetToolItems(this, index);
        }

        public override List<codeEditor.CodeEditor.AutocompleteItem> GetAutoCompleteItems(int index, out string cantidateWord)
        {
            return VerilogCommon.AutoComplete.GetAutoCompleteItems(this, VerilogParsedDocument, index, out cantidateWord);
        }


    }
}

