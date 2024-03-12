using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace pluginVerilog.NavigatePanel
{
    public class VerilogModuleNode : codeEditor.NavigatePanel.FileNode
    {
        public VerilogModuleNode(string ID, codeEditor.Data.Project project) : base(ID, project)
        {

        }

        public codeEditor.Data.ITextFile ITextFile
        {
            get => Project.GetRegisterdItem(ID) as codeEditor.Data.ITextFile;
        }

        public virtual Data.VerilogFile VerilogFile
        {
            get => Project.GetRegisterdItem(ID) as Data.VerilogFile;
        }

        public override string Text
        {
            get => FileItem.Name;
        }

        private static ajkControls.Icon icon = new ajkControls.Icon(Properties.Resources.verilog);
        public override void DrawNode(Graphics graphics, int x, int y, Font font, Color color, Color backgroundColor, Color selectedColor, int lineHeight, bool selected)
        {
            graphics.DrawImage(icon.GetImage(lineHeight, ajkControls.Icon.ColorStyle.Gray), new Point(x, y));
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

        public override void Selected()
        {
            codeEditor.Global.ViewController.CodeEditor.SetTextFile(ITextFile);
        }

        public override void Update()
        {
            VerilogFile.Update();

            List<string> currentDataIds = new List<string>();
            foreach (string key in VerilogFile.Items.Keys)
            {
                currentDataIds.Add(key);
            }

            List<codeEditor.NavigatePanel.NavigatePanelNode> removeNodes = new List<codeEditor.NavigatePanel.NavigatePanelNode>();
            foreach (codeEditor.NavigatePanel.NavigatePanelNode node in TreeNodes)
            {
                if (currentDataIds.Contains(node.ID))
                {
                    currentDataIds.Remove(node.ID);
                }
                else
                {
                    removeNodes.Add(node);
                }
            }

            foreach (codeEditor.NavigatePanel.NavigatePanelNode nodes in removeNodes)
            {
                TreeNodes.Remove(nodes);
            }

            foreach (string id in currentDataIds)
            {
                codeEditor.Data.Item item = Project.GetRegisterdItem(id);
                if (item == null) continue;
                TreeNodes.Add(item.CreateNode());
            }
        }

    }

}
