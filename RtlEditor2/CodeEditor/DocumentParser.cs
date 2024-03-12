using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.CodeEditor
{
    public class DocumentParser : IDisposable
    {
        protected DocumentParser() { }
        public DocumentParser(Data.TextFile textFile, ParseModeEnum parseMode)
        {
            this.document = new CodeDocument(textFile);
            this.document.CopyTextOnlyFrom(textFile.CodeDocument);
            this.ParseMode = parseMode;
            this.TextFile = textFile;
        }

        public void Dispose()
        {
            document = null;
        }

        public Data.TextFile TextFile { get; protected set; }
        public ParseModeEnum ParseMode { get; protected set; }
        protected CodeDocument document;
        public CodeDocument Document
        {
            get
            {
                return document;
            }
        }

        public enum ParseModeEnum
        {
            LoadParse,
            BackgroundParse,
            ActivatedParse,
            EditParse,
            PostEditParse
        }

        public virtual void Parse()
        {
        }

        public virtual ParsedDocument ParsedDocument { get; protected set; }
    }
}
