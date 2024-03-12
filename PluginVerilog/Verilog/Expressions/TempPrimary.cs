using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Expressions
{
    public class TempPrimary :Primary
    {
        public TempPrimary(string text)
        {
            this.text = text;
        }

        private string text;
        public override ajkControls.ColorLabel.ColorLabel GetLabel()
        {
            ajkControls.ColorLabel.ColorLabel label = new ajkControls.ColorLabel.ColorLabel();
            AppendLabel(label);
            return label;
        }

        public override string CreateString()
        {
            return GetLabel().CreateString();
        }

        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            label.AppendText(text);
        }

    }
}
