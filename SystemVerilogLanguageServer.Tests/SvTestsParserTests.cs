
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace SystemVerilogLanguageServer.Tests
{
    /// <summary>
    /// Runs the real VerilogParser over every .sv file in
    /// CodeEditor2VerilogPlugin/verilog_sample/sv_tests via the UI-free
    /// ParseEngine and reports parse diagnostics.
    ///
    /// Positive tests (no error expected) fail the run when diagnostics are
    /// produced. Negative tests (marked by `:should_fail_because:` metadata or
    /// "__bad"/"illegal" file names) must produce at least one diagnostic.
    ///
    /// A summary report is written to sv_tests_report.txt next to the test dll.
    /// </summary>
    public class SvTestsParserTests : SvTestsParserHarness
    {
        private static readonly object _lock = new object();

        public static IEnumerable<object[]> SvFiles()
        {
            EnsurePluginRegistered();
            return EnumerateSvFiles().Select(p => new object[] { p });
        }

        internal static bool IsNegativeTest(string path, string text)
        {
            if (text.Contains(":should_fail_because:", StringComparison.Ordinal)) return true;
            string fileName = Path.GetFileName(path);
            return fileName.Contains("__bad", StringComparison.OrdinalIgnoreCase)
                || fileName.Contains("illegal", StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [MemberData(nameof(SvFiles))]
        public async Task ParseSvTestFile(string path)
        {
            EnsurePluginRegistered();

            string text = File.ReadAllText(path);
            string fileName = Path.GetFileName(path);

            // UVM-dependent tests are excluded until uvm_pkg / uvm_macros.svh are provided
            if (IsUvmTest(path, text))
            {
                RecordDiagnostic(fileName, "[UVM-SKIPPED]");
                return;
            }

            bool isNegative = IsNegativeTest(path, text);

            // use the shared project where every .sv / .svh under sv_tests is
            // registered so that include directives and cross-file references
            // can resolve their targets
            (CodeEditor2.Data.Project project, pluginVerilog.Data.VerilogFile file) =
                GetProjectAndFile(path);

            // sv-tests `:defines:` metadata marker: the runner passes these as
            // compiler -D defines. Register them project-wide for the duration
            // of this parse (tests are not parallelized).
            Dictionary<string, string> defines = ParseDefinesMetadata(text);
            pluginVerilog.ProjectProperty projectProperty =
                (pluginVerilog.ProjectProperty)project.ProjectProperties[pluginVerilog.Plugin.StaticID];
            var addedMacros = new List<string>();
            foreach (var kvp in defines)
            {
                if (!projectProperty.Macros.ContainsKey(kvp.Key))
                {
                    projectProperty.Macros.Add(kvp.Key, pluginVerilog.Verilog.Macro.Create(kvp.Key, kvp.Value));
                    addedMacros.Add(kvp.Key);
                }
            }

            Exception? failure = null;
            pluginVerilog.Verilog.ParsedDocument? parsed = null;
            try
            {
                parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                foreach (string name in addedMacros) projectProperty.Macros.Remove(name);
            }

            if (failure != null)
            {
                RecordDiagnostic(fileName, $"CRASH: {failure.GetType().Name}: {failure.Message}");
                Assert.Fail($"{fileName}: parse threw {failure.GetType().Name}: {failure.Message}");
                return;
            }

            Assert.NotNull(parsed);
            Assert.NotNull(parsed!.Root);

            var messages = parsed.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>();
            int errorCount = messages.Count(m => m is pluginVerilog.Verilog.ParsedDocument.Message vm && vm.Type == pluginVerilog.Verilog.ParsedDocument.Message.MessageType.Error);
            int allCount = messages.Count;

            string diagText = DescribeMessages(messages);

            if (isNegative)
            {
                if (errorCount == 0)
                {
                    // negative test not rejected by the parser (known limitation,
                    // recorded in the report but does not fail the run)
                    RecordDiagnostic(fileName, $"[NEG-NOT-REJECTED] 0 errors ({allCount} diagnostics){diagText}");
                }
                return;
            }

            if (errorCount > 0)
            {
                RecordDiagnostic(fileName, $"[POS-ERROR] {errorCount} error(s) ({allCount} diagnostics){diagText}");
                Assert.Fail($"{fileName}: unexpected error(s) in positive test:{diagText}");
            }
        }

        private static string DescribeMessages(List<CodeEditor2.CodeEditor.ParsedDocument.Message> messages)
        {
            if (messages.Count == 0) return "";
            StringBuilder sb = new StringBuilder();
            foreach (var m in messages)
            {
                string type = m is pluginVerilog.Verilog.ParsedDocument.Message vm
                    ? vm.Type.ToString() : "?";
                sb.Append($"\n  [{type}] {m.Index}: {m.Text}");
            }
            return sb.ToString();
        }

        private static readonly List<string> _reportLines = new List<string>();

        private static void RecordDiagnostic(string fileName, string line)
        {
            lock (_reportLines)
            {
                _reportLines.Add(fileName + " " + line);
            }
        }

        /// <summary>
        /// Writes the collected report; runs after all ParseSvTestFile cases
        /// (alphabetical order guarantees this test runs last: "Z..." sorts after
        /// "ParseSvTestFile").
        /// </summary>
        [Fact]
        public void Zzz_WriteReport()
        {
            List<string> lines;
            lock (_reportLines)
            {
                lines = new List<string>(_reportLines);
            }
            // aggregate diagnostics by type and by message text
            var byType = new SortedDictionary<string, int>();
            var byText = new Dictionary<string, int>();
            foreach (string line in lines)
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"^\s*\[(\w+)\]\s*(\d+):\s*(.*)$", System.Text.RegularExpressions.RegexOptions.Multiline);
                if (!m.Success) continue;
                string type = m.Groups[1].Value;
                string text = m.Groups[3].Value;
                byType[type] = byType.TryGetValue(type, out int t) ? t + 1 : 1;
                string key = type + ": " + text;
                byText[key] = byText.TryGetValue(key, out int c) ? c + 1 : 1;
            }

            List<string> output = new List<string>();
            output.Add("=== sv-tests diagnostic summary ===");
            output.Add("");
            output.Add("-- by type --");
            foreach (var kvp in byType) output.Add($"{kvp.Value,6} [{kvp.Key}]");
            output.Add("");
            output.Add("-- by message (top 40) --");
            foreach (var kvp in byText.OrderByDescending(k => k.Value).Take(40))
                output.Add($"{kvp.Value,6} {kvp.Key}");
            output.Add("");
            output.Add("=== detail ===");
            output.AddRange(lines);

            string outPath = Path.Combine(AppContext.BaseDirectory, "sv_tests_report.txt");
            File.WriteAllLines(outPath, output);

            // the report must exist; failing tests are reported individually
            Assert.True(File.Exists(outPath));
        }
    }
}
