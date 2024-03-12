using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.CodeEditor
{
    public class ParsedDocument : IDisposable
    {
        public ParsedDocument(Data.TextFile textFile, ulong version, DocumentParser.ParseModeEnum parseMode)
        {
            this.Version = version;
            this.ParseMode = parseMode;
            textFileRef = new WeakReference<Data.TextFile>(textFile);
        }

        public readonly DocumentParser.ParseModeEnum ParseMode;

        private System.WeakReference<Data.TextFile> textFileRef;
        public Data.Item Item
        {
            get
            {
                Data.TextFile ret;
                if (!textFileRef.TryGetTarget(out ret)) return null;
                return ret;
            }
        }

        public Data.TextFile TextFile
        {
            get
            {
                Data.TextFile ret;
                if (!textFileRef.TryGetTarget(out ret)) return null;
                return ret;
            }
        }

        public Data.Project Project
        {
            get
            {
                if (Item == null) return null;
                return Item.Project;
            }
        }
        public ulong Version { get; set; }

        public virtual void Dispose()
        {
        }


        public List<Message> Messages = new List<Message>();

        public class Message
        {
            public int Index { get; protected set; }
            public int Length { get; protected set; }
            public string Text { get; protected set; }
            public Data.Project Project { get; protected set; }
            //public virtual MessageView.MessageNode CreateMessageNode()
            //{
            //    return null;
            //}
        }

        //public virtual List<ajkControls.SelectionForm.SelectionItem> GetInputCandidates()
        //{
        //    return null;
        //}
    }
}
