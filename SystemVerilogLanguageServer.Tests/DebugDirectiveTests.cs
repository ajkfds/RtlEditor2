
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    public class DebugDirectiveTests : SvTestsParserHarness
    {
        private static readonly object _debugLock = new object();

        private async Task RunCaseAsync(string caseName, string text, System.Text.StringBuilder sb)
        {
            CodeEditor2.Data.Project project;
            pluginVerilog.Data.VerilogFile file;
            lock (_debugLock)
            {
                project = new TestProject();
                project.ProjectProperties.Add(
                    pluginVerilog.Plugin.StaticID,
                    new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
                file = CreateFile(project, "debug_directive.sv");
            }
            pluginVerilog.Verilog.ParsedDocument? parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);

            sb.AppendLine($"=== case: {caseName} ===");
            if (parsed != null)
            {
                foreach (var m in parsed.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>())
                {
                    string type = m is pluginVerilog.Verilog.ParsedDocument.Message vm ? vm.Type.ToString() : "?";
                    sb.AppendLine($"[{type}] index={m.Index}: {m.Text}");
                }
            }
        }

        [Fact]
        public async Task Debug_Directive()
        {
            EnsurePluginRegistered();
            var sb = new System.Text.StringBuilder();

            await RunCaseAsync("timescale-ok", "`timescale 1 ns / 1 ps\nmodule m(); endmodule\n", sb);
            await RunCaseAsync("timescale-bad-mag", "`timescale 9 ns / 1 ps\nmodule m(); endmodule\n", sb);
            await RunCaseAsync("line-ok", "`line 1 \"somefile\" 2\nmodule m(); endmodule\n", sb);
            await RunCaseAsync("line-missing-level", "`line 1 \"somefile\"\nmodule m(); endmodule\n", sb);
            await RunCaseAsync("uncond-ok", "`unconnected_drive pull0\n`nounconnected_drive\nmodule m(); endmodule\n", sb);
            await RunCaseAsync("uncond-bad", "`unconnected_drive pull2\n`nounconnected_drive\nmodule m(); endmodule\n", sb);
            await RunCaseAsync("nouncond-bad", "`unconnected_drive pull0\n`nounconnected_drive pull0\nmodule m(); endmodule\n", sb);

            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "debug_directive_report.txt"), sb.ToString());
        }
    }
}
