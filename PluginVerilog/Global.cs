using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ajkControls.Primitive;

namespace pluginVerilog
{
    public static class Global
    {
        public static SetupForm SetupForm = new SetupForm();
        public static CodeDrawStyle CodeDrawStyle = new CodeDrawStyle();

        public static class Icons
        {
            public static IconImage Exclamation = new IconImage(Properties.Resources.exclamation);
            public static IconImage ExclamationBox = new IconImage(Properties.Resources.exclamationBox);
            public static IconImage Play = new IconImage(Properties.Resources.play);
            public static IconImage Pause = new IconImage(Properties.Resources.pause);
            public static IconImage Verilog = new IconImage(Properties.Resources.verilog);
            public static IconImage VerilogHeader = new IconImage(Properties.Resources.verilogHeader);
            public static IconImage SystemVerilog = new IconImage(Properties.Resources.systemVerilog);
            public static IconImage SystemVerilogHeader = new IconImage(Properties.Resources.systemVerilogHeader);
            public static IconImage IcarusVerilog = new IconImage(Properties.Resources.icarusVerilog);
            public static IconImage NewBadge = new IconImage(Properties.Resources.newBadge);
            public static IconImage MedalBadge = new IconImage(Properties.Resources.medalBadge);
        }
    }
}
