using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace pluginVerilog.Verilog.Popup
{
    public class PortPopup : codeEditor.CodeEditor.PopupItem
    {
        public PortPopup(DataObjects.Port port)
        {
            label.AppendLabel(port.GetLabel());
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
