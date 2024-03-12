using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using pluginVerilog.Verilog.BuildingBlocks;

namespace pluginVerilog.NavigatePanel
{
    public class VerilogFileNode : codeEditor.NavigatePanel.FileNode, IVerilogNavigateNode
    {
        public VerilogFileNode(Data.VerilogFile verilogFile) : base(verilogFile)
        {
            if (NodeCreated != null) NodeCreated(this);
        }
        public static Action<VerilogFileNode> NodeCreated;

        public Action NodeSelected;

        public Data.IVerilogRelatedFile VerilogRelatedFile
        {
            get { return Item as Data.IVerilogRelatedFile; }
        }

        public codeEditor.Data.TextFile TextFile
        {
            get { return Item as codeEditor.Data.TextFile; }
        }

        public virtual Data.VerilogFile VerilogFile
        {
            get { return Item as Data.VerilogFile; }
        }

        public override string Text
        {
            get {
                if (FileItem == null) return "-";
                return FileItem.Name;
            }
        }

        public override void DrawNode(Graphics graphics, int x, int y, Font font, Color color, Color backgroundColor, Color selectedColor, int lineHeight, bool selected)
        {
            graphics.DrawImage(Global.Icons.Verilog.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Blue), new Point(x, y));
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

            if (Link) graphics.DrawImage(codeEditor.Global.IconImages.Link.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Blue), new Point(x, y));

            if (VerilogFile != null && VerilogFile.ParsedDocument != null && VerilogFile.VerilogParsedDocument.ErrorCount != 0)
            {
                graphics.DrawImage(Global.Icons.Exclamation.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Red), new Point(x, y));
            }

            if (VerilogFile != null && VerilogFile.Dirty)
            {
                graphics.DrawImage(Global.Icons.NewBadge.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Orange), new Point(x, y));
            }
        }

        public override void Selected()
        {
            // activate navigate panel context menu
            var menu = codeEditor.Controller.NavigatePanel.GetContextMenuStrip();
            if (menu.Items.ContainsKey("openWithExploererTsmi")) menu.Items["openWithExploererTsmi"].Visible = true;
            if (menu.Items.ContainsKey("icarusVerilogTsmi")) menu.Items["icarusVerilogTsmi"].Visible = true;
            if (menu.Items.ContainsKey("VerilogDebugTsmi")) menu.Items["VerilogDebugTsmi"].Visible = true;

            codeEditor.Controller.CodeEditor.SetTextFile(TextFile);

            if (!TextFile.ParseValid | TextFile.ReparseRequested)
            {
                if (!codeEditor.Global.StopParse)
                {
                    codeEditor.Tools.ParseHierarchyForm pform = new codeEditor.Tools.ParseHierarchyForm(this);
                    codeEditor.Controller.ShowDialogForm(pform);
                }
            }

            foreach (BuildingBlock module in VerilogFile.VerilogParsedDocument.Root.BuldingBlocks.Values)
            {
                VerilogFile.CodeDocument.ExpandBlock(VerilogFile.CodeDocument.GetLineAt(module.BeginIndexReference.Indexs.Last() ));
            }

            if (NodeSelected != null) NodeSelected();
        }


        public override void Update()
        {
            if(VerilogFile == null)
            {
                return;
            }
            VerilogFile.Update();

            List<codeEditor.Data.Item> targetDataItems = new List<codeEditor.Data.Item>();
            List<codeEditor.Data.Item> addDataItems = new List<codeEditor.Data.Item>();
            foreach (codeEditor.Data.Item item in VerilogFile.Items.Values)
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

    }

}
