using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace pluginVerilog.NavigatePanel
{
    public class VerilogHeaderInstanceNode : codeEditor.NavigatePanel.FileNode, IVerilogNavigateNode
    {
        public VerilogHeaderInstanceNode(Data.VerilogHeaderInstance vhFile, codeEditor.Data.Project project) : base(vhFile)
        {

        }
        public Action NodeSelected;

        public Data.IVerilogRelatedFile VerilogRelatedFile
        {
            get { return Item as Data.IVerilogRelatedFile; }
        }
        public codeEditor.Data.TextFile TextFile
        {
            get { return Item as codeEditor.Data.TextFile; }
        }

        public Data.VerilogHeaderInstance VerilogHeaderInstance
        {
            get
            {
                return Item as Data.VerilogHeaderInstance;
            }
        }

        public override void Selected()
        {
            // activate navigate panel context menu
            var menu = codeEditor.Controller.NavigatePanel.GetContextMenuStrip();
            if (menu.Items.ContainsKey("openWithExploererTsmi")) menu.Items["openWithExploererTsmi"].Visible = true;

            codeEditor.Controller.CodeEditor.SetTextFile(TextFile);
            if (NodeSelected != null) NodeSelected();
        }

        public override string Text
        {
            get { return FileItem.Name; }
        }

        public override void Update()
        {
            if (VerilogHeaderInstance == null) return;
            //VerilogHeaderInstance.Update();

            List<codeEditor.Data.Item> targetDataItems = new List<codeEditor.Data.Item>();
            List<codeEditor.Data.Item> addDataItems = new List<codeEditor.Data.Item>();
            foreach (codeEditor.Data.Item item in VerilogHeaderInstance.Items.Values)
            {
                targetDataItems.Add(item);
                addDataItems.Add(item);
            }

            List<codeEditor.NavigatePanel.NavigatePanelNode> removeNodes = new List<codeEditor.NavigatePanel.NavigatePanelNode>();
            foreach (codeEditor.NavigatePanel.NavigatePanelNode node in TreeNodes)
            {
                if (node.Item != null && targetDataItems.Contains(node.Item))
                {
                    addDataItems.Remove(node.Item);
                }
                else
                {
                    removeNodes.Add(node);
                }
            }

            foreach (codeEditor.NavigatePanel.NavigatePanelNode node in removeNodes)
            {
                TreeNodes.Remove(node);
                node.Dispose();
            }

            int treeIndex = 0;
            foreach (codeEditor.Data.Item item in targetDataItems)
            {
                if (item == null) continue;
                if (addDataItems.Contains(item))
                {
                    TreeNodes.Insert(treeIndex, item.NavigatePanelNode);
                }
                treeIndex++;
            }


        }

        public override void DrawNode(Graphics graphics, int x, int y, Font font, Color color, Color backgroundColor, Color selectedColor, int lineHeight, bool selected)
        {
            graphics.DrawImage(Global.Icons.VerilogHeader.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Blue), new Point(x, y));
            Color bgColor = backgroundColor;
            if (selected) bgColor = selectedColor;
            System.Windows.Forms.TextRenderer.DrawText(
                graphics,
                Text,
                font,
                new Point(x + lineHeight + (lineHeight >> 2), y),
                color,
                bgColor,
                System.Windows.Forms.TextFormatFlags.NoPadding
                );
        }

    }
}
