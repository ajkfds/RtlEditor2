using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.IcarusVerilog
{
    public static class Setup
    {
        public static string BinPath = @"C:\iverilog\bin\";
        public static string GtkWaveBinPath = @"C:\iverilog\gtkwave\bin";
        public static string SimulationPath = System.Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\Simulation\icarusVerilog";
    }
}
