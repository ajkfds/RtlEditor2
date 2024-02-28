using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace RtlEditor2.Models.Editor.NavigatePanel
{
    public class FileNode : NavigatePanelNode
    {
        protected FileNode() { }
        public FileNode(Data.File file) : base(file)
        {
            if (FileNodeCreated != null) FileNodeCreated(this);
        }
        public static Action<FileNode> FileNodeCreated;

        public virtual Data.File FileItem
        {
            get { return Item as Data.File; }
        }

        public override string Text
        {
            get { return FileItem.Name; }
        }

        public override void OnSelected()
        {
            //Data.ITextFile textFile = FileItem as Data.ITextFile;
            //codeEditor.Controller.NavigatePanel.GetContextMenuStrip().Items["openWithExploererTsmi"].Visible = true;

        }
    }
}
