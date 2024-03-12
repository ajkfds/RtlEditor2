using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace pluginVerilog.Verilog.Popup
{
    public class ParameterPopup : codeEditor.CodeEditor.PopupItem
    {
        public ParameterPopup(DataObjects.Constants.Constants parameter)
        {
            label.AppendText("parameter ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
            label.AppendText(parameter.Name, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Paramater));
            if(parameter.Expression != null)
            {
                label.AppendText(" = ");
                label.AppendLabel(parameter.Expression.GetLabel());
            }
        }

        ajkControls.ColorLabel.ColorLabel label = new ajkControls.ColorLabel.ColorLabel();

        public override Size GetSize(Graphics graphics, Font font)
        {
            return label.GetSize(graphics, font);
        }

        public override void Draw(Graphics graphics, int x, int y, Font font, Color backgroundColor)
        {
            label.Draw(graphics, x, y, font, Color.FromArgb(210, 210, 210), backgroundColor);
        }
    }
}
