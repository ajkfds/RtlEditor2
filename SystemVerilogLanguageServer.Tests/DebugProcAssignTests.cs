
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    public class DebugProcAssignTests : SvTestsParserHarness
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
                file = CreateFile(project, "debug_proc_assign.sv");
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
                foreach (var bb in parsed.Root.BuildingBlocks.Values)
                {
                    sb.Append($"  bb '{bb.Name}': ");
                    foreach (var ne in bb.NamedElements)
                    {
                        sb.Append($"[{ne.Name}:{ne.GetType().Name}]");
                    }
                    sb.AppendLine();
                }
            }
        }

        [Fact]
        public async Task Debug_ProcAssign()
        {
            EnsurePluginRegistered();
            var sb = new System.Text.StringBuilder();

            await RunCaseAsync("wire-simple", "module top;\nwire w;\ninitial w = 1'b1;\nendmodule\n", sb);
            await RunCaseAsync("wire-delay", "module top(input a, input b);\nwire w;\ninitial w = #10 a & b;\nendmodule\n", sb);
            await RunCaseAsync("reg-simple", "module top;\nreg w;\ninitial w = 1'b1;\nendmodule\n", sb);
            await RunCaseAsync("wire-nonblocking", "module top;\nwire w;\ninitial w <= 1'b1;\nendmodule\n", sb);
            await RunCaseAsync("full-file", File.ReadAllText(Path.Combine(FindSvTestsRoot(), "chapter-10", "10.3--proc-assignment--bad.sv")), sb);
            await RunCaseAsync("assign-deassign", File.ReadAllText(Path.Combine(FindSvTestsRoot(), "chapter-10", "10.6.1--assign-deassign.sv")), sb);
            await RunCaseAsync("force-release", File.ReadAllText(Path.Combine(FindSvTestsRoot(), "chapter-10", "10.6.2--force-release.sv")), sb);

            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "debug_proc_assign_report.txt"), sb.ToString());
        }
    }
}
