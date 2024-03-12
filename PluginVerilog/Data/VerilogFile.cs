using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using codeEditor.CodeEditor;
using codeEditor.Data;
using pluginVerilog.Verilog.BuildingBlocks;

namespace pluginVerilog.Data
{
    public class VerilogFile : codeEditor.Data.TextFile, IVerilogRelatedFile
    {
        public new static VerilogFile Create(string relativePath, codeEditor.Data.Project project)
        {
            VerilogFile fileItem = new VerilogFile();
            fileItem.Project = project;
            fileItem.RelativePath = relativePath;
            if (relativePath.Contains('\\'))
            {
                fileItem.Name = relativePath.Substring(relativePath.LastIndexOf('\\') + 1);
            }
            else
            {
                fileItem.Name = relativePath;
            }

            return fileItem;
        }

        public static VerilogFile CreateSystemVerilog(string relativePath, codeEditor.Data.Project project)
        {
            VerilogFile fileItem = new VerilogFile();
            fileItem.Project = project;
            fileItem.RelativePath = relativePath;
            if (relativePath.Contains('\\'))
            {
                fileItem.Name = relativePath.Substring(relativePath.LastIndexOf('\\') + 1);
            }
            else
            {
                fileItem.Name = relativePath;
            }
            fileItem.SystemVerilog = true;
            return fileItem;
        }

        public bool SystemVerilog { get; set; } = false;

        public string FileID
        {
            get
            {
                return RelativePath;
            }
        }

        public override codeEditor.CodeEditor.CodeDocument CodeDocument
        {
            get
            {
                if (document == null)
                {
                    try
                    {
                        loadDoumentFromFile();
                    }
                    catch
                    {
                        document = null;
                    }
                }
                return document;
            }
            protected set
            {
                if (value != null &&  value as CodeEditor.CodeDocument == null) System.Diagnostics.Debugger.Break();
                document = value as CodeEditor.CodeDocument ;
            }
        }

        // accept new Parsed Document
        public override void AcceptParsedDocument(ParsedDocument newParsedDocument)
        {
            ParsedDocument oldParsedDocument = ParsedDocument;
            ParsedDocument = null;

            // copy include files



            if (oldParsedDocument != null) oldParsedDocument.Dispose();

            ParsedDocument = newParsedDocument;

            if(VerilogParsedDocument == null)
            {
                Update();
                return;
            }

            foreach (BuildingBlock buildingBlock in VerilogParsedDocument.Root.BuldingBlocks.Values)
            {
                if (!ProjectProperty.IsRegisterableModule(buildingBlock.Name, this))
                {
                    Module module = buildingBlock as Module;
                    Module registeredModule = ProjectProperty.GetBuildingBlock(module.Name)as Module;
                    if (registeredModule.File.RelativePath == module.File.RelativePath) continue;

                    if (module.NameReference != null) {
                        module.NameReference.AddError("duplicated module name"); 
                    }
                    continue;
                }

                bool suceed = ProjectProperty.RegisterModule(buildingBlock.Name, this);
                if (!suceed)
                {
                    System.Diagnostics.Debugger.Break();
                    // add module name error
                }
            }

            if (ParsedDocument is Verilog.ParsedDocument)
            {
                ReparseRequested = (ParsedDocument as Verilog.ParsedDocument).ReparseRequested;
            }
            Update();
        }
        public override void LoadFormFile()
        {
            loadDoumentFromFile();
            AcceptParsedDocument(null);
            Project.AddReparseTarget(this);
            if (NavigatePanelNode != null) NavigatePanelNode.Update();
        }
        private void loadDoumentFromFile()
        {
            try
            {
                if(document == null) document = new CodeEditor.CodeDocument(this);
                using (System.IO.StreamReader sr = new System.IO.StreamReader(Project.GetAbsolutePath(RelativePath)))
                {
                    loadedFileLastWriteTime = System.IO.File.GetLastWriteTime(AbsolutePath);

                    string text = sr.ReadToEnd();
                    document.Replace(0, document.Length, 0, text);
                    document.ClearHistory();
                    document.Clean();
                }
            }
            catch
            {
                document = null;
            }
        }

        private Dictionary<string, System.WeakReference<ParsedDocument>> instancedParsedDocumentRefs = new Dictionary<string, WeakReference<ParsedDocument>>();

        public ParsedDocument GetInstancedParsedDocument(string parameterId)
        {
            cleanWeakRef();

            ParsedDocument ret;

            if(parameterId == "")
            {
                return ParsedDocument;
            }
            else
            {
                lock (instancedParsedDocumentRefs)
                {
                    if (instancedParsedDocumentRefs.ContainsKey(parameterId))
                    {
                        if (instancedParsedDocumentRefs[parameterId].TryGetTarget(out ret))
                        {
                            return ret;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public void RegisterInstanceParsedDocument(string id, ParsedDocument parsedDocument,VerilogModuleInstance moduleInstance)
        {
            cleanWeakRef();
            if (id == "")
            {
                ParsedDocument = parsedDocument;
            }
            else
            {
                lock (instancedParsedDocumentRefs)
                {
                    if (instancedParsedDocumentRefs.ContainsKey(id))
                    {
                        instancedParsedDocumentRefs[id] = new WeakReference<ParsedDocument>(parsedDocument);
                    }
                    else
                    {
                        instancedParsedDocumentRefs.Add(id, new WeakReference<ParsedDocument>(parsedDocument));
                        Project.AddReparseTarget(moduleInstance);
                    }
                }
            }
        }

        private void cleanWeakRef()
        {
            List<string> removeKeys = new List<string>();
            ParsedDocument ret;
            lock (instancedParsedDocumentRefs)
            {
                foreach(var r in instancedParsedDocumentRefs)
                {
                    if (!r.Value.TryGetTarget(out ret)) removeKeys.Add(r.Key);
                }
                foreach(string key in removeKeys)
                {
                    instancedParsedDocumentRefs.Remove(key);
                }
            }
        }

        private List<System.WeakReference<Data.VerilogModuleInstance>> moduleInstanceRefs
            = new List<WeakReference<VerilogModuleInstance>>();

        public void RegisterModuleInstance(VerilogModuleInstance verilogModuleInstance)
        {
            moduleInstanceRefs.Add(new WeakReference<VerilogModuleInstance>(verilogModuleInstance));
        }

        public void RemoveModuleInstance(VerilogModuleInstance verilogModuleInstance)
        {
            for(int i = 0; i< moduleInstanceRefs.Count; i++)
            {
                VerilogModuleInstance ret;
                if (!moduleInstanceRefs[i].TryGetTarget(out ret)) continue;
                if (ret == verilogModuleInstance) moduleInstanceRefs.Remove(moduleInstanceRefs[i]);
            }
        }


        public override void Dispose()
        {
            if(ParsedDocument != null)
            {
                foreach(var incFile in VerilogParsedDocument.IncludeFiles.Values)
                {
                    incFile.Dispose();
                }
            }
            moduleInstanceRefs.Clear();
            base.Dispose();
        }


        public Verilog.ParsedDocument VerilogParsedDocument
        {
            get
            {
                return ParsedDocument as Verilog.ParsedDocument;
            }
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
            NavigatePanel.VerilogFileNode node = new NavigatePanel.VerilogFileNode(this);
            return node;
        }

        public override DocumentParser CreateDocumentParser(DocumentParser.ParseModeEnum parseMode)
        {
            return new Parser.VerilogParser(this, parseMode);
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
            return VerilogCommon.AutoComplete.GetAutoCompleteItems(this, VerilogParsedDocument, index, out cantidateWord);
        }

    }
}
