using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace pluginMarkdown.NavigatePanel
{
    public class MarkdownFileNode : RtlEditor2.NavigatePanel.FileNode
    {
        public MarkdownFileNode(Data.MarkdownFile file) : base(file)
        {

        }

        public RtlEditor2.Data.TextFile TextFile
        {
            get
            {
                return Item as RtlEditor2.Data.TextFile;
            }
        }

        public override string Text
        {
            get => FileItem.Name;
        }

//        private static ajkControls.Primitive.IconImage icon = new ajkControls.Primitive.IconImage(Properties.Resources.text);
        //public override void DrawNode(Graphics graphics, int x, int y, Font font, Color color, Color backgroundColor, Color selectedColor, int lineHeight, bool selected)
        //{
        //    graphics.DrawImage(icon.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.White), new Point(x, y));
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

        //public override void OnSelected()
        //{
        //    codeEditor.Controller.CodeEditor.SetTextFile(TextFile);
        //}
    }
}
