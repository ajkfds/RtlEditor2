
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    // focused test: how many times is `define processed within a single parse?
    public class DebugMacroTraceTests : SvTestsParserHarness
    {
        [Fact]
        public async Task Debug_Macro_SingleParseCount()
        {
            EnsurePluginRegistered();

            string text = "`define A 1\n`undef A\n`define A 2\nmodule m;\nendmodule\n";

            CodeEditor2.Data.Project project = new TestProject();
            project.ProjectProperties.Add(
                pluginVerilog.Plugin.StaticID,
                new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
            var file = CreateFile(project, "debug_macro_trace.sv");

            // mimic ParseEngine: create the code document first
            MethodInfo? create = file.GetType().GetMethod(
                "CreateCodeDocument",
                BindingFlags.Instance | BindingFlags.NonPublic);
            create?.Invoke(file, null);

            pluginVerilog.Parser.VerilogParser parser = new pluginVerilog.Parser.VerilogParser(
                file,
                CodeEditor2.CodeEditor.Parser.DocumentParser.ParseModeEnum.BackgroundParse,
                null);
            var srcDoc = new CodeEditor2.CodeEditor.CodeDocument(file, text);
            parser.Document.CopyTextOnlyFrom(srcDoc);
            await parser.ParseAsync(null);

            var parsed = parser.VerilogParsedDocument;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== single parse ===");
            sb.AppendLine($"ErrorCount={parsed.ErrorCount}");
            foreach (var m in parsed.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>())
            {
                sb.AppendLine($"[{m.Text}] index={m.Index}");
            }

            System.IO.File.WriteAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "debug_macro_trace.txt"), sb.ToString());
        }
    }
}
