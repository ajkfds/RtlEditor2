using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace pluginVerilog.Verilog.Popup
{
    public class FunctionPopup : codeEditor.CodeEditor.PopupItem
    {
        public FunctionPopup(Function function)
        {
            label.AppendText("function ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
            if (function.DataObjects.ContainsKey(function.Name))
            {
                DataObjects.DataObject retVal = function.DataObjects[function.Name];
                retVal.AppendTypeLabel(label);
            }
            label.AppendText(function.Name, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Identifier));

            label.AppendText("\r\n");
            bool first = true;
            foreach(DataObjects.Port port in function.Ports.Values)
            {
                if(!first) label.AppendText("\r\n");
                label.AppendLabel(port.GetLabel());
                first = false;
            }
        }

        ajkControls.ColorLabel.ColorLabel label = new ajkControls.ColorLabel.ColorLabel();

        public override Size GetSize(Graphics graphics, Font font)
        {
            return label.GetSize(graphics, font);
        }

        public override void Draw(Graphics graphics, int x, int y, Font font, Color backgroundColor)
        {
            label.Draw(graphics, x, y, font, Color.FromArgb(210,210,210), backgroundColor);
        }


    }

}
