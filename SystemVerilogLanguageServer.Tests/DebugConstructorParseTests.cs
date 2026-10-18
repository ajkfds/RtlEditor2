
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    // temporary diagnostic test for classification-B (class constructor / new)
    public class DebugConstructorParseTests : SvTestsParserHarness
    {
        private static readonly object _debugLock = new object();

        private async Task RunCaseAsync(string caseName, string text)
        {
            CodeEditor2.Data.Project project;
            pluginVerilog.Data.VerilogFile file;
            lock (_debugLock)
            {
                project = new TestProject();
                project.ProjectProperties.Add(
                    pluginVerilog.Plugin.StaticID,
                    new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
                file = CreateFile(project, "debug_ctor.sv");
            }

            pluginVerilog.Verilog.ParsedDocument? parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);
            Assert.NotNull(parsed);
            parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);
            Assert.NotNull(parsed);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== case: {caseName} ===");
            foreach (var m in parsed!.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>())
            {
                string type = m is pluginVerilog.Verilog.ParsedDocument.Message vm ? vm.Type.ToString() : "?";
                int line = 1 + (m.Index >= 0 && m.Index <= text.Length ? text.Substring(0, m.Index).Count(c => c == '\n') : -1);
                sb.AppendLine($"[{type}] index={m.Index} line={line}: {m.Text}");
            }
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "debug_ctor_report.txt"),
                sb.ToString());
        }

        [Fact]
        public async Task Debug_ParseClassMember14Shared()
        {
            EnsurePluginRegistered();
            string path = Path.Combine(FindSvTestsRoot(), "generic", "member", "class_member_test_14.sv");
            (CodeEditor2.Data.Project proj, pluginVerilog.Data.VerilogFile file) = GetProjectAndFile(path);
            string text = File.ReadAllText(path);
            pluginVerilog.Verilog.ParsedDocument? parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== shared project parse: class_member_test_14 ===");
            foreach (var m in parsed!.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>())
            {
                string type = m is pluginVerilog.Verilog.ParsedDocument.Message vm ? vm.Type.ToString() : "?";
                int line = 1 + (m.Index >= 0 && m.Index <= text.Length ? text.Substring(0, m.Index).Count(c => c == '\n') : -1);
                sb.AppendLine($"[{type}] index={m.Index} line={line}: {m.Text}");
            }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "debug_member14_report.txt"), sb.ToString());
        }
    }
}
