using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    public class DebugTimescaleCommentTests : SvTestsParserHarness
    {
        private static readonly object _lock = new object();

        private async Task<List<string>> ParseAsync(string text)
        {
            CodeEditor2.Data.Project project;
            pluginVerilog.Data.VerilogFile file;
            lock (_lock)
            {
                project = new TestProject();
                project.ProjectProperties.Add(
                    pluginVerilog.Plugin.StaticID,
                    new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
                file = CreateFile(project, "timescale_comment.sv");
            }
            pluginVerilog.Verilog.ParsedDocument? parsed =
                await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);
            var messages = new List<string>();
            if (parsed != null)
            {
                foreach (var m in parsed.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>())
                {
                    messages.Add(m.Text);
                }
            }
            return messages;
        }

        [Fact]
        public async Task Timescale_WithComments()
        {
            EnsurePluginRegistered();
            var sb = new System.Text.StringBuilder();

            var cases = new (string name, string text)[]
            {
                ("line-comment", "`timescale 1 ns / 1 ps // comment\nmodule m(); endmodule\n"),
                ("block-comment", "`timescale 1 ns / 1 ps /* comment */\nmodule m(); endmodule\n"),
                ("no-comment", "`timescale 1 ns / 1 ps\nmodule m(); endmodule\n"),
                ("comment-before-slash", "`timescale 1 ns // unit\n / 1 ps\nmodule m(); endmodule\n"),
                ("unconnected_drive-comment", "`unconnected_drive pull0 // c\n`nounconnected_drive\nmodule m(); endmodule\n"),
                ("line-directive-string", "`line 1 \"//foo\" 2\nmodule m(); endmodule\n"),
            };

            foreach (var c in cases)
            {
                var messages = await ParseAsync(c.text);
                sb.AppendLine($"=== {c.name} ===");
                foreach (var m in messages) sb.AppendLine("  " + m);
            }
            File.WriteAllText(Path.Combine(System.AppContext.BaseDirectory, "debug_timescale_comment.txt"), sb.ToString());

            var lineComment = await ParseAsync(cases[0].text);
            Assert.DoesNotContain(lineComment, m => m.Contains("timescale"));
            var blockComment = await ParseAsync(cases[1].text);
            Assert.DoesNotContain(blockComment, m => m.Contains("timescale"));
            var noComment = await ParseAsync(cases[2].text);
            Assert.DoesNotContain(noComment, m => m.Contains("timescale"));
        }
    }
}
