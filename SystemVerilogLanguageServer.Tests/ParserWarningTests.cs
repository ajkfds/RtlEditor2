using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    /// <summary>
    /// Regression tests for the parser diagnostics added for:
    /// <list type="number">
    ///   <item>duplicate parameter / localparam declaration (Error)</item>
    ///   <item>function / task referencing an outer signal without a port (Warning)</item>
    ///   <item>if condition whose bit width is not 1 (Warning)</item>
    ///   <item>case item whose bit width differs from the case expression (Warning)</item>
    /// </list>
    /// </summary>
    public class ParserWarningTests : SvTestsParserHarness
    {
        private static readonly object _lock = new object();

        private async Task<List<(string Type, string Text)>> ParseAsync(string text)
        {
            CodeEditor2.Data.Project project;
            pluginVerilog.Data.VerilogFile file;
            lock (_lock)
            {
                project = new TestProject();
                project.ProjectProperties.Add(
                    pluginVerilog.Plugin.StaticID,
                    new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
                file = CreateFile(project, "parser_warning_test.sv");
            }
            pluginVerilog.Verilog.ParsedDocument? parsed =
                await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);

            var result = new List<(string, string)>();
            if (parsed == null) return result;
            foreach (var m in parsed.Messages ?? new List<CodeEditor2.CodeEditor.ParsedDocument.Message>())
            {
                string type = m is pluginVerilog.Verilog.ParsedDocument.Message vm ? vm.Type.ToString() : "?";
                result.Add((type, m.Text));
            }
            return result;
        }

        private static bool Has(List<(string Type, string Text)> messages, string type, string text)
        {
            foreach (var m in messages)
            {
                if (m.Type == type && m.Text == text) return true;
            }
            return false;
        }

        private static bool HasContaining(List<(string Type, string Text)> messages, string type, string fragment)
        {
            foreach (var m in messages)
            {
                if (m.Type == type && m.Text.Contains(fragment, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // ---- 1. duplicate parameter / localparam ----------------------------

        [Fact]
        public async Task DuplicateParameter_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nparameter A = 1;\nparameter A = 2;\nendmodule\n");
            Assert.True(Has(messages, "Error", "parameter name duplicated"),
                "expected 'parameter name duplicated' (got: " + string.Join(" | ", messages) + ")");
        }

        [Fact]
        public async Task DuplicateLocalparam_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nlocalparam B = 1;\nlocalparam B = 2;\nendmodule\n");
            Assert.True(Has(messages, "Error", "parameter name duplicated"),
                "expected 'parameter name duplicated' (got: " + string.Join(" | ", messages) + ")");
        }

        [Fact]
        public async Task DuplicateParameterInSameStatement_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nparameter A = 1, A = 2;\nendmodule\n");
            Assert.True(Has(messages, "Error", "parameter name duplicated"),
                "expected 'parameter name duplicated' (got: " + string.Join(" | ", messages) + ")");
        }

        [Fact]
        public async Task DuplicateParameterPort_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top #(parameter W = 1, parameter W = 2) ();\nendmodule\n");
            Assert.True(Has(messages, "Error", "parameter name duplicated"),
                "expected 'parameter name duplicated' (got: " + string.Join(" | ", messages) + ")");
        }

        /// <summary>
        /// Distinct parameters must not be reported: the module body is parsed
        /// twice (prototype pass, then real pass) into the same name space, so
        /// the check must compare the declaration position, not just the name.
        /// </summary>
        [Fact]
        public async Task DistinctParameters_AreNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nparameter A = 1;\nparameter B = 2;\nendmodule\n");
            Assert.False(Has(messages, "Error", "parameter name duplicated"),
                "unexpected duplicate report (got: " + string.Join(" | ", messages) + ")");
        }

        // ---- 2. function / task referencing an outer signal -----------------

        [Fact]
        public async Task FunctionReferencingOuterSignal_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nlogic sig;\nfunction automatic int f(input int a);\n  f = a + sig;\nendfunction\nendmodule\n");
            Assert.True(HasContaining(messages, "Warning", "'sig' is referenced from a function without a port"),
                "expected the outer-signal warning (got: " + string.Join(" | ", messages) + ")");
        }

        [Fact]
        public async Task TaskReferencingOuterSignal_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nlogic sig;\ntask automatic t(input int a);\n  sig = a;\nendtask\nendmodule\n");
            Assert.True(HasContaining(messages, "Warning", "'sig' is referenced from a task without a port"),
                "expected the outer-signal warning (got: " + string.Join(" | ", messages) + ")");
        }

        [Fact]
        public async Task FunctionUsingOnlyPorts_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nlogic sig;\nfunction automatic int f(input int a, input int b);\n  f = a + b;\nendfunction\nendmodule\n");
            Assert.False(HasContaining(messages, "Warning", "without a port"),
                "unexpected outer-signal warning (got: " + string.Join(" | ", messages) + ")");
        }

        [Fact]
        public async Task FunctionUsingLocalAndParameter_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nparameter P = 3;\nfunction automatic int f(input int a);\n  int t;\n  t = a;\n  f = t + P;\nendfunction\nendmodule\n");
            Assert.False(HasContaining(messages, "Warning", "without a port"),
                "unexpected outer-signal warning (got: " + string.Join(" | ", messages) + ")");
        }

        /// <summary>
        /// A class method may access the members of its class, so it must not be
        /// reported as an outer-signal reference.
        /// </summary>
        [Fact]
        public async Task ClassMethodUsingMember_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nclass C;\n  int m;\n  function int f();\n    f = m;\n  endfunction\nendclass\nendmodule\n");
            Assert.False(HasContaining(messages, "Warning", "without a port"),
                "unexpected outer-signal warning (got: " + string.Join(" | ", messages) + ")");
        }

        // ---- 3. if condition bit width --------------------------------------

        [Fact]
        public async Task IfConditionWithMultiBitWidth_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nlogic [3:0] a;\ninitial begin\n  if (a) a = 0;\nend\nendmodule\n");
            Assert.True(HasContaining(messages, "Warning", "if condition bit width is 4"),
                "expected the if-condition width warning (got: " + string.Join(" | ", messages) + ")");
        }

        [Fact]
        public async Task IfConditionWithSingleBit_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nlogic a;\ninitial begin\n  if (a) a = 0;\nend\nendmodule\n");
            Assert.False(HasContaining(messages, "Warning", "if condition bit width"),
                "unexpected if-condition width warning (got: " + string.Join(" | ", messages) + ")");
        }

        // ---- 4. case item bit width -----------------------------------------

        [Fact]
        public async Task CaseItemWithDifferentBitWidth_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nlogic [1:0] sel;\ninitial begin\n  case (sel)\n    4'h1: sel = 0;\n    default: sel = 0;\n  endcase\nend\nendmodule\n");
            Assert.True(HasContaining(messages, "Warning", "case item bit width is 4"),
                "expected the case-item width warning (got: " + string.Join(" | ", messages) + ")");
        }

        [Fact]
        public async Task CaseItemWithSameBitWidth_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nlogic [1:0] sel;\ninitial begin\n  case (sel)\n    2'h1: sel = 0;\n    default: sel = 0;\n  endcase\nend\nendmodule\n");
            Assert.False(HasContaining(messages, "Warning", "case item bit width"),
                "unexpected case-item width warning (got: " + string.Join(" | ", messages) + ")");
        }

        /// <summary>
        /// case inside uses an open range list, not plain case items, so the
        /// width comparison must not run there.
        /// </summary>
        [Fact]
        public async Task CaseInside_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\nlogic [1:0] sel;\ninitial begin\n  case (sel) inside\n    [0:1]: sel = 0;\n    default: sel = 0;\n  endcase\nend\nendmodule\n");
            Assert.False(HasContaining(messages, "Warning", "case item bit width"),
                "unexpected case-item width warning (got: " + string.Join(" | ", messages) + ")");
        }
    }
}
