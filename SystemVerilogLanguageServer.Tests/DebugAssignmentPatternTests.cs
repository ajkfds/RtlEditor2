using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    public class DebugAssignmentPatternTests : SvTestsParserHarness
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
                file = CreateFile(project, "debug_assignment_pattern.sv");
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
            }
        }

        [Fact]
        public async Task Debug_AssignmentPattern()
        {
            EnsurePluginRegistered();
            var sb = new System.Text.StringBuilder();

            // positive : struct positional / struct keyed / struct default key
            await RunCaseAsync("struct-ok",
                "module top();\n  typedef struct { int a; int b; } ms_t;\n  ms_t ms;\n  initial begin\n    ms = '{ 0, 1};\n    ms = '{ a:0, b:1};\n    ms = '{ default:1, a:1};\n  end\nendmodule\n", sb);

            // negative : wrong element count / unknown member
            await RunCaseAsync("struct-ng",
                "module top();\n  typedef struct { int a; int b; } ms_t;\n  ms_t ms;\n  initial begin\n    ms = '{ 0, 1, 2};\n    ms = '{ a:0, c:1};\n  end\nendmodule\n", sb);

            // positive : unpacked array of bit, sized
            await RunCaseAsync("array-ok",
                "module top();\n  bit arr_a [7:0];\n  initial begin\n    arr_a = '{1, 1, 1, 0, 0, 1, 1, 1};\n  end\nendmodule\n", sb);

            // negative : unpacked array element count mismatch
            await RunCaseAsync("array-ng",
                "module top();\n  bit arr_a [7:0];\n  initial begin\n    arr_a = '{1, 0, 1};\n  end\nendmodule\n", sb);

            // positive : repetition pattern (element count not fixed)
            await RunCaseAsync("array-rep-ok",
                "module top();\n  struct {int X,Y,Z;} XYZ = '{3{1}};\nendmodule\n", sb);

            // negative : scalar too many elements
            await RunCaseAsync("scalar-ng",
                "module top();\n  int a;\n  initial begin\n    a = '{1, 2};\n  end\nendmodule\n", sb);

            // positive : associative array default key
            await RunCaseAsync("assoc-ok",
                "module top ();\n  string words [int] = '{default: \"hello\"};\nendmodule\n", sb);

            // negative : associative array positional elements
            await RunCaseAsync("assoc-ng",
                "module top ();\n  string words [int] = '{ \"a\", \"b\" };\nendmodule\n", sb);

            // negative : C-like flattened struct array pattern (5.10)
            await RunCaseAsync("struct-array-ng",
                "module top();\n  typedef struct {\n    int a;\n    int b;\n  } ms_t;\n\n  ms_t ms[1:0] = '{0, 0, 1, 1};\n\nendmodule\n", sb);

            // positive : nested pattern per element
            await RunCaseAsync("struct-array-ok",
                "module top();\n  typedef struct {\n    int a;\n    int b;\n  } ms_t;\n\n  ms_t ms[1:0] = '{'{0, 0}, '{1, 1}};\n\nendmodule\n", sb);

            // negative : blocking assignment (RHS) uses the resolved lhs type
            await RunCaseAsync("blocking-ng",
                "module top();\n  typedef struct { int a; int b; } ms_t;\n  ms_t ms;\n  bit arr_a [7:0];\n  initial begin\n    ms = '{ 0, 1, 2};\n    arr_a = '{1, 0, 1};\n  end\nendmodule\n", sb);

            // negative : non blocking assignment (RHS) uses the resolved lhs type
            await RunCaseAsync("nonblocking-ng",
                "module top();\n  typedef struct { int a; int b; } ms_t;\n  ms_t ms;\n  bit arr_a [7:0];\n  initial begin\n    ms <= '{ 0, 1, 2};\n    arr_a <= '{1, 0, 1};\n  end\nendmodule\n", sb);

            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "debug_assignment_pattern_report.txt"), sb.ToString());
        }
    }
}
