using Avalonia.Input;
using AvaloniaEdit.Document;
using RtlEditor2.NavigatePanel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.Data
{
    public class TextFile : File, ITextFile
    {
        public static TextFile Create(string relativePath, Project project)
        {
            TextFile fileItem = new TextFile();
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

        public override void Dispose()
        {
            if (document != null) document.Dispose();
            //if (ParsedDocument != null) ParsedDocument.Dispose();
            document = null;
            //ParsedDocument = null;
            base.Dispose();
        }

        public virtual bool ReparseRequested { get; protected set; } = false;

        public bool IsCodeDocumentCashed
        {
            get { if (document == null) return false; else return true; }
        }

        public virtual CodeEditor.ParsedDocument ParsedDocument { get; set; }

        public bool ParseValid
        {
            get
            {
                RtlEditor2.CodeEditor.CodeDocument doc = CodeDocument;
                RtlEditor2.CodeEditor.ParsedDocument parsedDocument = ParsedDocument;
                if (doc == null) return false;
                if (parsedDocument == null) return false;
                if (doc.Version == parsedDocument.Version) return true;
                return false;
            }
        }

        public virtual void AcceptParsedDocument(RtlEditor2.CodeEditor.ParsedDocument newParsedDocument)
        {
            RtlEditor2.CodeEditor.ParsedDocument oldParsedDocument = ParsedDocument;
            ParsedDocument = null;
            if (oldParsedDocument != null) oldParsedDocument.Dispose();

            ParsedDocument = newParsedDocument;
            Update();
        }
        public virtual void Close()
        {
            if (Dirty) return;
            if (CodeDocument == null) return;
            CodeDocument.Dispose();
            CodeDocument = null;
        }

        public virtual bool Dirty
        {
            get
            {
                if (CodeDocument == null) return false;
//                if (CodeDocument.IsDirty) return true;
                return false;
            }
        }

        public virtual void LoadFormFile()
        {
            loadDoumentFromFile();
        }


        protected CodeEditor.CodeDocument document = null;

        public virtual CodeEditor.CodeDocument CodeDocument
        {
            get
            {
                if (document == null)
                {
                    loadDoumentFromFile();
                }
                else
                {
                    loadedFileLastWriteTime = null;
                }
                return document;
            }
            protected set
            {
                document = value;
            }
        }

        public virtual void Save()
        {
            //if (CodeDocument == null) return;

            //using (System.IO.StreamWriter sw = new System.IO.StreamWriter(AbsolutePath))
            //{
            //    sw.Write(CodeDocument.CreateString());
            //}
            //CodeDocument.Clean();
            //loadedFileLastWriteTime = System.IO.File.GetLastWriteTime(AbsolutePath);
        }

        public virtual DateTime? LoadedFileLastWriteTime
        {
            get
            {
                return loadedFileLastWriteTime;
            }
        }

        protected DateTime? loadedFileLastWriteTime;
        private void loadDoumentFromFile()
        {
            try
            {
                if (document == null) document = new CodeEditor.CodeDocument(this);
                using (System.IO.StreamReader sr = new System.IO.StreamReader(AbsolutePath))
                {
                    loadedFileLastWriteTime = System.IO.File.GetLastWriteTime(AbsolutePath);

                    string text = sr.ReadToEnd();
                    document.TextDocument.Replace(0, document.TextDocument.TextLength, text);
                    //document.Replace(0, document.Length, 0, text);
                    //document.ClearHistory();
                    //document.Clean();
                }
            }
            catch
            {
                document = null;
            }
        }

        //public string GetMd5Hash()
        //{
        //    //if (document == null) return "";
        //    //byte[] data = System.Text.Encoding.UTF8.GetBytes(document.CreateString());

        //    //System.Security.Cryptography.MD5CryptoServiceProvider md5 =
        //    //    new System.Security.Cryptography.MD5CryptoServiceProvider();

        //    //byte[] bs = md5.ComputeHash(data);
        //    //md5.Clear();

        //    //System.Text.StringBuilder result = new System.Text.StringBuilder();
        //    //foreach (byte b in bs)
        //    //{
        //    //    result.Append(b.ToString("x2"));
        //    //}

        //    return result.ToString();
        //}


        //public virtual ajkControls.CodeTextbox.CodeDrawStyle DrawStyle
        //{
        //    get
        //    {
        //        return Global.DefaultDrawStyle;
        //    }
        //}



        protected override NavigatePanelNode createNode()
        {
            return new TextFileNode(this);
        }


        public override CodeEditor.DocumentParser CreateDocumentParser(CodeEditor.DocumentParser.ParseModeEnum parseMode)
        {
            return null;
        }

        //public virtual List<PopupItem> GetPopupItems(ulong Version, int index)
        //{
        //    return null;
        //}

        //public virtual List<AutocompleteItem> GetAutoCompleteItems(int index, out string cantidateWord)
        //{
        //    cantidateWord = null;
        //    return null;
        //}
        //public virtual List<codeEditor.CodeEditor.ToolItem> GetToolItems(int index)
        //{
        //    return null;
        //}


        //public virtual void BeforeKeyDown(KeyEventArgs e)
        //{

        //}

        //public virtual void AfterKeyDown(System.Windows.Forms.KeyEventArgs e)
        //{

        //}

        //public virtual void BeforeKeyPressed(KeyPressEventArgs e)
        //{

        //}

        //public virtual void AfterKeyPressed(System.Windows.Forms.KeyPressEventArgs e)
        //{
        //}

        public void ParseHierarchy(Action<ITextFile> action)
        {
//            codeEditor.Controller.AppendLog("parseHier : " + Name);

            List<string> parsedIds = new List<string>();
            parseHierarchy(this, parsedIds, action);
            Update();

            if (NavigatePanelNode != null)
            {
                NavigatePanelNode.HierarchicalVisibleUpdate();
            }
//            codeEditor.Controller.NavigatePanel.Update();
        }

        private void parseHierarchy(Data.Item item, List<string> parsedIds, Action<ITextFile> action)
        {
            if (item == null) return;
            Data.ITextFile textFile = item as Data.TextFile;
            if (textFile == null) return;
            if (parsedIds.Contains(textFile.ID)) return;

            action(textFile);
            parsedIds.Add(textFile.ID);

            if (textFile.ParseValid & !textFile.ReparseRequested)
            {
                textFile.Update();
            }
            else
            {
                CodeEditor.DocumentParser parser = item.CreateDocumentParser(CodeEditor.DocumentParser.ParseModeEnum.BackgroundParse);
                if (parser != null)
                {
                    System.Diagnostics.Debug.Print("## parse hier " + textFile.ID);
                    parser.Parse();
                    if (parser.ParsedDocument == null) return;
                    textFile.AcceptParsedDocument(parser.ParsedDocument);
                    textFile.Update();
                }
            }

            // parse all chiled nodes
            List<Data.Item> items = new List<Data.Item>();
            lock (textFile.Items)
            {
                foreach (Data.Item subItem in textFile.Items.Values)
                {
                    items.Add(subItem);
                }
            }

            foreach (Data.Item subitem in items)
            {
                parseHierarchy(subitem, parsedIds, action);
            }

            if (textFile.ReparseRequested)
            {
                CodeEditor.DocumentParser parser = item.CreateDocumentParser(CodeEditor.DocumentParser.ParseModeEnum.BackgroundParse);
                if (parser != null)
                {
                    System.Diagnostics.Debug.Print("## re-parse hier " + textFile.ID);
                    parser.Parse();
                    if (parser.ParsedDocument == null) return;
                    textFile.AcceptParsedDocument(parser.ParsedDocument);
                    textFile.Update();
                }
            }
        }
    }
}
