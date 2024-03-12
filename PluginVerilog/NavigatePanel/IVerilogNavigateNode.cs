using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace pluginVerilog.NavigatePanel
{
    public interface IVerilogNavigateNode
    {
        Data.IVerilogRelatedFile VerilogRelatedFile { get; }

        string Text { get; }

        void DrawNode(Graphics graphics, int x, int y, Font font, Color color, Color backgroundColor, Color selectedColor, int lineHeight, bool selected);

        void Selected();

        void Update();
    }
}
