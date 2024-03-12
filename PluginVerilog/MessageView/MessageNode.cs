using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace pluginVerilog.MessageView
{
    public class MessageNode : codeEditor.MessageView.MessageNode
    {
        public MessageNode(Data.IVerilogRelatedFile file,Verilog.ParsedDocument.Message message)
        {
            fileRef = new WeakReference<Data.IVerilogRelatedFile>(file);
            Text = "["+message.LineNo.ToString()+"]"+ message.Text;
            this.messageType = message.Type;
            this.index = message.Index;
            this.length = message.Length;
            this.project = message.Project;
            this.lineNo = message.LineNo;
        }


        private System.WeakReference<Data.IVerilogRelatedFile> fileRef;
        public Data.IVerilogRelatedFile File
        {
            get
            {
                Data.IVerilogRelatedFile file;
                if (!fileRef.TryGetTarget(out file)) return null;
                return file;
            }
        }

        
        Verilog.ParsedDocument.Message.MessageType messageType;
        int index;
        int length;
        int lineNo;
        codeEditor.Data.Project project;

        public override void Selected()
        {
            if(File != null && File.CodeDocument != null)
            {
                File.CodeDocument.SelectionStart = index;
                File.CodeDocument.SelectionLast = index + length;
                File.CodeDocument.CaretIndex = index;
            }
            codeEditor.Controller.CodeEditor.ScrollToCaret();
            codeEditor.Controller.CodeEditor.Refresh();
        }

        private static ajkControls.Primitive.IconImage icon = new ajkControls.Primitive.IconImage(Properties.Resources.exclamationBox);
        public override void DrawNode(Graphics graphics, int x, int y, Font font, Color color, Color backgroundColor, Color selectedColor, int lineHeight, bool selected)
        {
            switch (messageType)
            {
                case Verilog.ParsedDocument.Message.MessageType.Error:
                    graphics.DrawImage(icon.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Red), new Point(x, y));
                    break;
                case Verilog.ParsedDocument.Message.MessageType.Warning:
                    graphics.DrawImage(icon.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Orange), new Point(x, y));
                    break;
                case Verilog.ParsedDocument.Message.MessageType.Notice:
                    graphics.DrawImage(icon.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Green), new Point(x, y));
                    break;
                case Verilog.ParsedDocument.Message.MessageType.Hint:
                    graphics.DrawImage(icon.GetImage(lineHeight, ajkControls.Primitive.IconImage.ColorStyle.Blue), new Point(x, y));
                    break;
            }
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

            //            if (VerilogFile != null && VerilogFile.ParsedDocument != null && VerilogFile.ParsedDocument.Messages.Count != 0)
            //            {
            //                graphics.DrawImage(Style.ExclamationIcon.GetImage(lineHeight, ajkControls.Icon.ColorStyle.Red), new Point(x, y));
            //           }
        }

    }
}
