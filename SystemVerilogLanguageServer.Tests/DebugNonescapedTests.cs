
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    public class DebugNonescapedTests : SvTestsParserHarness
    {
        private static readonly object _debugLock = new object();

        private async Task RunTextAsync(string caseName, string text, System.Text.StringBuilder sb, bool dumpNames = false)
        {
            CodeEditor2.Data.Project project;
            pluginVerilog.Data.VerilogFile file;
            lock (_debugLock)
            {
                project = new TestProject();
                project.ProjectProperties.Add(
                    pluginVerilog.Plugin.StaticID,
                    new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
                file = CreateFile(project, "debug_nonescaped.sv");
            }
            pluginVerilog.Verilog.ParsedDocument? parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);

            sb.AppendLine($"=== case: {caseName} ===");
            if (dumpNames && parsed != null)
            {
                foreach (var bb in parsed.Root.BuildingBlocks.Values)
                {
                    sb.Append($"  buildingblock '{bb.Name}' namedElements: ");
                    foreach (var k in bb.NamedElements.Keys) sb.Append($"[{k}]");
                    sb.AppendLine();
                }
            }
            foreach (var m in parsed!.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>())
            {
                string type = m is pluginVerilog.Verilog.ParsedDocument.Message vm ? vm.Type.ToString() : "?";
                int line = 1 + (m.Index >= 0 && m.Index <= text.Length ? text.Substring(0, m.Index).Count(c => c == '\n') : -1);
                string snippet = (m.Index >= 0 && m.Index < text.Length) ? text.Substring(m.Index, Math.Min(12, text.Length - m.Index)).Replace("\n", "\\n") : "";
                sb.AppendLine($"[{type}] index={m.Index} line={line}: {m.Text}  @'{snippet}'");
            }
        }

        [Fact]
        public async Task Debug_NonescapedAccess()
        {
            EnsurePluginRegistered();
            try { File.Delete(Path.Combine(AppContext.BaseDirectory, "debug_nameref.txt")); } catch { }
            try { File.Delete(Path.Combine(AppContext.BaseDirectory, "debug_va.txt")); } catch { }
            var sb = new System.Text.StringBuilder();

            // full file
            {
                string path = Path.Combine(FindSvTestsRoot(), "chapter-5", "5.6.1--nonescaped-access.sv");
                (CodeEditor2.Data.Project proj, pluginVerilog.Data.VerilogFile file) = GetProjectAndFile(path);
                string text = File.ReadAllText(path);
                pluginVerilog.Verilog.ParsedDocument? parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);
                sb.AppendLine("=== full file ===");
                foreach (var m in parsed!.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>())
                {
                    string type = m is pluginVerilog.Verilog.ParsedDocument.Message vm ? vm.Type.ToString() : "?";
                    int line = 1 + (m.Index >= 0 && m.Index <= text.Length ? text.Substring(0, m.Index).Count(c => c == '\n') : -1);
                    string snippet = (m.Index >= 0 && m.Index < text.Length) ? text.Substring(m.Index, Math.Min(12, text.Length - m.Index)).Replace("\n", "\\n") : "";
                    sb.AppendLine($"[{type}] index={m.Index} line={line}: {m.Text}  @'{snippet}'");
                }
            }

            await RunTextAsync("g-normal-decl-ref", "module m();\n  reg cpu3;\n  wire r;\n  assign r = cpu3;\nendmodule\n", sb);
            await RunTextAsync("a-space-decl-ref-nonesc", "module m();\n  reg \\cpu3 ;\n  wire r;\n  assign r = cpu3;\nendmodule\n", sb);

            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "debug_nonescaped_report.txt"), sb.ToString());
        }
    }
}
