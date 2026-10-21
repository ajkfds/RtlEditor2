
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    // temporary diagnostic test for "assign A = BBB.CCC" acceptance issue
    public class DebugAssignDotTests : SvTestsParserHarness
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
                file = CreateFile(project, "debug_assign_dot.sv");
            }
            pluginVerilog.Verilog.ParsedDocument? parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);
            parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);

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
        public async Task Debug_AssignDot()
        {
            EnsurePluginRegistered();
            try { File.Delete(Path.Combine(AppContext.BaseDirectory, "debug_assign_dot_report.txt")); } catch { }
            var sb = new System.Text.StringBuilder();

            await RunCaseAsync("case1-semicolon", "module MODULE4;\nwire x;\nassign x = BBB.CCC;\nendmodule\n", sb);
            await RunCaseAsync("case2-no-semicolon", "module MODULE4;\nwire x;\nassign x = BBB.CCC\nendmodule\n", sb);
            await RunCaseAsync("case3-plain-unfound", "module MODULE4;\nwire x;\nassign x = BBB;\nendmodule\n", sb);
            await RunCaseAsync("case4-just-bb", "module MODULE4;\nwire x;\nassign x = BBB.CCC\nendmodule\n", sb);

            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "debug_assign_dot_report.txt"), sb.ToString());
        }
    }
}
