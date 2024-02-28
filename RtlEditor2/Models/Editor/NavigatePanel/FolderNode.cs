using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Avalonia.Media;
using RtlEditor2.Models.Common;

namespace RtlEditor2.Models.Editor.NavigatePanel
{
    public class FolderNode : NavigatePanelNode
    {
        protected FolderNode() { }

        public FolderNode(Data.Folder folder) : base(folder)
        {
            if (FolderNodeCreated != null) FolderNodeCreated(this);
        }
        public static Action<FolderNode> FolderNodeCreated;

//        private System.WeakReference<Data.Folder> folderRef;
        public virtual Data.Folder Folder
        {
            get
            {
                return Item as Data.Folder;
            }
        }

        public override IImage Image
        {
            get
            {
                return Icons.GetIconBitmap("folder");
            }
        }

        public override string Text
        {
            get { return Folder.Name; }
        }

        public override void OnSelected()
        {
            //codeEditor.Controller.NavigatePanel.GetContextMenuStrip().Items["openWithExploererTsmi"].Visible = true;
            //codeEditor.Controller.NavigatePanel.GetContextMenuStrip().Items["ignoreTsmi"].Visible = true;
        }

        public override void Update()
        {
            Folder.Update();

            List<Data.Item> addItems = new List<Data.Item>();
            foreach (Data.Item item in Folder.Items.Values)
            {
                addItems.Add(item);
            }

            List<NavigatePanelNode> removeNodes = new List<NavigatePanelNode>();
            foreach (NavigatePanelNode node in TreeNodes)
            {
                removeNodes.Add(node);
            }

            foreach (Data.Item item in Folder.Items.Values)
            {
                if (removeNodes.Contains(item.NavigatePanelNode))
                {
                    removeNodes.Remove(item.NavigatePanelNode);
                }
                if (TreeNodes.Contains(item.NavigatePanelNode))
                {
                    addItems.Remove(item);
                }
            }

            foreach (NavigatePanelNode nodes in removeNodes)
            {
                TreeNodes.Remove(nodes);
            }

            foreach (Data.Item item in addItems)
            {
                if (item == null) continue;
                TreeNodes.Add(item.NavigatePanelNode);
            }


        }

        //private static ajkControls.Primitive.IconImage openFolder = new ajkControls.Primitive.IconImage(Properties.Resources.openFolder);
        //private static ajkControls.Primitive.IconImage folder = new ajkControls.Primitive.IconImage(Properties.Resources.folder);
        //private static ajkControls.Primitive.IconImage ignoreIcon = new ajkControls.Primitive.IconImage(Properties.Resources.ignore);

        //public override void DrawNode(Graphics graphics, int x, int y, Font font, Color color, Color backgroundColor, Color selectedColor, int lineHeight, bool selected)
        //{
        //    if (IsExpanded)
        //    {
        //        graphics.DrawImage(openFolder.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Blue), new Point(x, y));
        //    }
        //    else
        //    {
        //        graphics.DrawImage(folder.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Blue), new Point(x, y));
        //    }

        //    if (Item.Ignore)
        //    {
        //        graphics.DrawImage(ignoreIcon.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Gray), new Point(x, y));
        //    }
        //    if (Link) graphics.DrawImage(codeEditor.Global.IconImages.Link.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Blue), new Point(x, y));

        //    Color bgColor = backgroundColor;
        //    if (selected) bgColor = selectedColor;
        //    System.Windows.Forms.TextRenderer.DrawText(
        //        graphics,
        //        Text,
        //        font,
        //        new Point(x + lineHeight + (lineHeight >> 2), y),
        //        color,
        //        bgColor,
        //        System.Windows.Forms.TextFormatFlags.NoPadding
        //        );
        //}
    }
}
