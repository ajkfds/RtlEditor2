
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    // temporary diagnostic test : measure parse time of scr1_memory_tb_ahb.sv
    public class DebugPerfScr1Tests : SvTestsParserHarness
    {
        private static string FindScr1Root()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "CodeEditor2VerilogPlugin", "verilog_sample", "scr1");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            throw new InvalidOperationException("scr1 root not found.");
        }

        private static string ReadFile(string relativePath)
        {
            string path = Path.Combine(FindScr1Root(), relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            return File.ReadAllText(path);
        }

        [Fact]
        public async Task Debug_ParseScr1MemoryTbTimings()
        {
            EnsurePluginRegistered();
            string tbText = ReadFile("src/tb/scr1_memory_tb_ahb.sv");

            var project = new TestProject();
            project.ProjectProperties.Add(
                pluginVerilog.Plugin.StaticID,
                new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));

            var file = CreateFile(project, "src/tb/scr1_memory_tb_ahb.sv");

            // measure whole file parse (repeated, mirroring editor reparse behavior)
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== scr1_memory_tb_ahb.sv parse timing ===");
            sb.AppendLine("text length: " + tbText.Length);

            for (int round = 0; round < 5; round++)
            {
                var sw = Stopwatch.StartNew();
                var parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(tbText, file);
                sw.Stop();
                sb.AppendLine($"round {round}: parse {sw.ElapsedMilliseconds} ms, messages={parsed?.Messages?.Count ?? 0}");
                if (round == 0 && parsed != null)
                {
                    foreach (var m in parsed.Messages)
                    {
                        sb.AppendLine($"  {m.Text}");
                    }
                }
            }

            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "debug_perf_scr1_report.txt"),
                sb.ToString());
        }
    }
}
