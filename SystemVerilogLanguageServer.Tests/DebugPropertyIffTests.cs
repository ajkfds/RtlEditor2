
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    // temporary diagnostic test for "assert property (@(posedge clk) a iff b)"
    public class DebugPropertyIffTests : SvTestsParserHarness
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
                file = CreateFile(project, "debug_property_iff.sv");
            }
            pluginVerilog.Verilog.ParsedDocument? parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);

            sb.AppendLine($"=== case: {caseName} ===");
            if (parsed != null)
            {
                foreach (var m in parsed.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>())
                {
                    string type = m is pluginVerilog.Verilog.ParsedDocument.Message vm ? vm.Type.ToString() : "?";
                    int line = 1 + (m.Index >= 0 && m.Index <= text.Length ? text.Substring(0, m.Index).Count(c => c == '\n') : -1);
                    string snippet = (m.Index >= 0 && m.Index < text.Length) ? text.Substring(m.Index, Math.Min(14, text.Length - m.Index)).Replace("\n", "\\n") : "";
                    sb.AppendLine($"[{type}] index={m.Index} line={line}: {m.Text}  @'{snippet}'");
                }
            }
        }

        [Fact]
        public async Task Debug_PropertyIff()
        {
            EnsurePluginRegistered();
            try { File.Delete(Path.Combine(AppContext.BaseDirectory, "debug_property_iff_report.txt")); } catch { }
            var sb = new System.Text.StringBuilder();

            await RunCaseAsync("full", File.ReadAllText(Path.Combine(FindSvTestsRoot(), "chapter-16", "16.12--property-iff.sv")), sb);
            await RunCaseAsync("and-op", "module top();\nlogic clk; logic a; logic b;\nassert property ( @(posedge clk) a and b );\nendmodule\n", sb);
            await RunCaseAsync("plain", "module top();\nlogic clk; logic a;\nassert property ( @(posedge clk) a );\nendmodule\n", sb);
            await RunCaseAsync("iff-no-clock", "module top();\nlogic a; logic b;\nassert property ( a iff b );\nendmodule\n", sb);
            await RunCaseAsync("logical-iff-assign", "module top();\nlogic a; logic b; logic c;\nassign c = a iff b;\nendmodule\n", sb);

            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "debug_property_iff_report.txt"), sb.ToString());
        }
    }
}
