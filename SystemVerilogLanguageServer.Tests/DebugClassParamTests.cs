
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    // temporary diagnostic test for "class test_cls #(int t = 12) ... a = def - t;"
    public class DebugClassParamTests : SvTestsParserHarness
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
                file = CreateFile(project, "debug_class_param.sv");
            }
            pluginVerilog.Verilog.ParsedDocument? parsed = await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);

            sb.AppendLine($"=== case: {caseName} ===");
            if (parsed != null)
            {
                foreach (var m in parsed.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>())
                {
                    string type = m is pluginVerilog.Verilog.ParsedDocument.Message vm ? vm.Type.ToString() : "?";
                    int line = 1 + (m.Index >= 0 && m.Index <= text.Length ? text.Substring(0, m.Index).Count(c => c == '\n') : -1);
                    string snippet = (m.Index >= 0 && m.Index < text.Length) ? text.Substring(m.Index, Math.Min(20, text.Length - m.Index)).Replace("\n", "\\n") : "";
                    sb.AppendLine($"[{type}] index={m.Index} line={line}: {m.Text}  @'{snippet}'");
                }
                foreach (var bb in parsed.Root.BuildingBlocks.Values)
                {
                    sb.AppendLine($"  bb '{bb.Name}' : {bb.GetType().Name}");
                    foreach (var nb in bb.BuildingBlocks.Values.OfType<pluginVerilog.Verilog.BuildingBlocks.Class>())
                    {
                        sb.AppendLine($"    BUILDINGBLOCK class '{nb.Name}' hash={nb.GetHashCode()}: ");
                        foreach (var neItem in nb.NamedElements)
                        {
                            string extra = "";
                            if (neItem is pluginVerilog.Verilog.DataObjects.Constants.Constants cst)
                            {
                                extra = $"(Defined={cst.Defined},Type={cst.DataType?.GetType().Name})";
                            }
                            sb.Append($"[{neItem.Name}{extra}]");
                        }
                        sb.AppendLine();
                    }
                    foreach (var ne in bb.NamedElements.Values.OfType<pluginVerilog.Verilog.BuildingBlocks.Class>())
                    {
                        sb.AppendLine($"    NAMEDELEM class '{ne.Name}' hash={ne.GetHashCode()}: ");
                        foreach (var neItem in ne.NamedElements)
                        {
                            string extra = "";
                            if (neItem is pluginVerilog.Verilog.DataObjects.Constants.Constants cst)
                            {
                                extra = $"(Defined={cst.Defined},Type={cst.DataType?.GetType().Name})";
                            }
                            sb.Append($"[{neItem.Name}:{neItem.GetType().Name}{extra}]");
                        }
                        sb.AppendLine();
                    }
                }
            }
        }

        [Fact]
        public async Task Debug_ClassParam()
        {
            EnsurePluginRegistered();
            try { File.Delete(Path.Combine(AppContext.BaseDirectory, "debug_class_param_report.txt")); } catch { }
            var sb = new System.Text.StringBuilder();

            await RunCaseAsync("full", File.ReadAllText(Path.Combine(FindSvTestsRoot(), "chapter-8", "8.8--typed_constructor_param.sv")), sb);
            await RunCaseAsync("minimal-param-used",
                "module tb();\n" +
                "  class c #(int t = 12);\n" +
                "    int a;\n" +
                "    function new(int def = 42);\n" +
                "      a = def - t;\n" +
                "    endfunction\n" +
                "  endclass\n" +
                "endmodule\n", sb);
            await RunCaseAsync("class-body-use",
                "module tb();\n" +
                "  class c #(int t = 12);\n" +
                "    int a = t;\n" +
                "  endclass\n" +
                "endmodule\n", sb);
            await RunCaseAsync("func-return-param",
                "module tb();\n" +
                "  class c #(int t = 12);\n" +
                "    function int f();\n" +
                "      return t;\n" +
                "    endfunction\n" +
                "  endclass\n" +
                "endmodule\n", sb);
            await RunCaseAsync("func-arg-named-param",
                "module tb();\n" +
                "  class c #(int t = 12);\n" +
                "    function int f(int def);\n" +
                "      return def - t;\n" +
                "    endfunction\n" +
                "  endclass\n" +
                "endmodule\n", sb);
            await RunCaseAsync("class-body-param-keyword",
                "module tb();\n" +
                "  class c #(parameter int t = 12);\n" +
                "    int a = t;\n" +
                "  endclass\n" +
                "endmodule\n", sb);
            await RunCaseAsync("class-body-no-type",
                "module tb();\n" +
                "  class c #(t = 12);\n" +
                "    int a = t;\n" +
                "  endclass\n" +
                "endmodule\n", sb);
            await RunCaseAsync("class-body-param-no-type",
                "module tb();\n" +
                "  class c #(parameter t = 12);\n" +
                "    int a = t;\n" +
                "  endclass\n" +
                "endmodule\n", sb);
            await RunCaseAsync("rootclass-int-param",
                "class c #(int t = 12);\n" +
                "  int a = t;\n" +
                "endclass\n" +
                "module tb();\n" +
                "endmodule\n", sb);
            await RunCaseAsync("module-class-int-param-2",
                "module tb();\n" +
                "  class c #(int t = 12);\n" +
                "    int a;\n" +
                "    function void f();\n" +
                "      a = t;\n" +
                "    endfunction\n" +
                "  endclass\n" +
                "endmodule\n", sb);
            await RunCaseAsync("minimal-param-keyword",
                "module tb();\n" +
                "  class c #(parameter int t = 12);\n" +
                "    int a;\n" +
                "    function new(int def = 42);\n" +
                "      a = def - t;\n" +
                "    endfunction\n" +
                "  endclass\n" +
                "endmodule\n", sb);

            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "debug_class_param_report.txt"), sb.ToString());
        }
    }
}
