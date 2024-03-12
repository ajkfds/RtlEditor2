using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace pluginVerilog.NavigatePanel
{
    public class VerilogHeaderNode : codeEditor.NavigatePanel.FileNode, IVerilogNavigateNode
    {
        public VerilogHeaderNode(Data.VerilogHeaderFile headerFile) : base(headerFile)
        {

        }

        public Data.IVerilogRelatedFile VerilogRelatedFile
        {
            get { return Item as Data.IVerilogRelatedFile; }
        }
        public codeEditor.Data.TextFile TextFile
        {
            get { return Item as codeEditor.Data.TextFile; }
        }

        public override string Text
        {
            get { return FileItem.Name; }
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

        public override void Selected()
        {
            codeEditor.Controller.CodeEditor.SetTextFile(TextFile);
        }
    }
}
