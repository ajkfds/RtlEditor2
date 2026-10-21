
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    // temporary diagnostic test for module-outside macro duplicate errors
    public class DebugMacroDuplicateTests : SvTestsParserHarness
    {
        private async Task<string> ParseAndCollectAsync(string caseName, string text)
        {
            CodeEditor2.Data.Project project;
            pluginVerilog.Data.VerilogFile file;
            project = new TestProject();
            project.ProjectProperties.Add(
                pluginVerilog.Plugin.StaticID,
                new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
            file = CreateFile(project, "debug_macro.sv");

            pluginVerilog.Verilog.ParsedDocument? parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);
            Assert.NotNull(parsed);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== case: {caseName} ===");
            foreach (var m in parsed!.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>())
            {
                string type = m is pluginVerilog.Verilog.ParsedDocument.Message vm ? vm.Type.ToString() : "?";
                int line = 1 + (m.Index >= 0 && m.Index <= text.Length ? text.Substring(0, m.Index).Count(c => c == '\n') : -1);
                sb.AppendLine($"[{type}] index={m.Index} line={line}: {m.Text}");
            }
            return sb.ToString();
        }

        [Fact]
        public async Task Debug_MacroOutsideModule_Cases()
        {
            EnsurePluginRegistered();
            var report = new System.Text.StringBuilder();

            // 1. define before module
            report.Append(await ParseAndCollectAsync("define-before-module",
                "`define A 1\nmodule m;\nendmodule\n"));

            // 2. define after module
            report.Append(await ParseAndCollectAsync("define-after-module",
                "module m;\nendmodule\n`define A 1\n"));

            // 3. two defines at file level
            report.Append(await ParseAndCollectAsync("two-defines",
                "`define A 1\n`define B 2\nmodule m;\nendmodule\n"));

            // 4. define at file level + use inside module
            report.Append(await ParseAndCollectAsync("define-and-use",
                "`define A 1\nmodule m;\n  wire x = `A;\nendmodule\n"));

            // 5. define between two modules
            report.Append(await ParseAfterTwoPassesAsync("define-between-modules",
                "module a;\nendmodule\n`define A 1\nmodule b;\nendmodule\n"));

            // 6. genuine redefinition of the same macro at different lines -> should error
            report.Append(await ParseAndCollectAsync("genuine-redefinition",
                "`define A 1\n`define A 2\nmodule m;\nendmodule\n"));

            // 7. undef then re-define -> should NOT error
            report.Append(await ParseAndCollectAsync("undef-then-redefine",
                "`define A 1\n`undef A\n`define A 2\nmodule m;\nendmodule\n"));

            File.WriteAllText(
                Path.Combine(AppContext.BaseDirectory, "debug_macro_report.txt"),
                report.ToString());
        }

        private async Task<string> ParseAfterTwoPassesAsync(string caseName, string text)
        {
            CodeEditor2.Data.Project project;
            pluginVerilog.Data.VerilogFile file;
            project = new TestProject();
            project.ProjectProperties.Add(
                pluginVerilog.Plugin.StaticID,
                new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
            file = CreateFile(project, "debug_macro2.sv");

            pluginVerilog.Verilog.ParsedDocument? parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);
            Assert.NotNull(parsed);
            // run again to exercise repeated parse on same file
            parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== case: {caseName} ===");
            foreach (var m in parsed!.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>())
            {
                string type = m is pluginVerilog.Verilog.ParsedDocument.Message vm ? vm.Type.ToString() : "?";
                int line = 1 + (m.Index >= 0 && m.Index <= text.Length ? text.Substring(0, m.Index).Count(c => c == '\n') : -1);
                sb.AppendLine($"[{type}] index={m.Index} line={line}: {m.Text}");
            }
            return sb.ToString();
        }
    }
}
