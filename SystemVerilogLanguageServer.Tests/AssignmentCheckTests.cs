using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    /// <summary>
    /// Regression tests for three assignment checks that were missing:
    /// <list type="number">
    ///   <item>
    ///     assigning a plain (scalar / packed) value to a whole unpacked array
    ///     (IEEE 1800-2017 7.4 / 10.9) - previously silent, because only the
    ///     flattened bit width was compared;
    ///   </item>
    ///   <item>
    ///     assigning a function name from outside of the function body
    ///     (IEEE 1800-2017 13.4.1) - the function name denotes its return
    ///     variable, which may only be assigned inside the body;
    ///   </item>
    ///   <item>
    ///     assigning the same variable from two different procedural blocks
    ///     (two <c>always</c> / <c>initial</c> / <c>final</c> blocks) - legal
    ///     Verilog, but almost always a design error, so it is a Warning.
    ///   </item>
    /// </list>
    /// </summary>
    public class AssignmentCheckTests : SvTestsParserHarness
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
                file = CreateFile(project, "assignment_check_test.sv");
            }
            pluginVerilog.Verilog.ParsedDocument? parsed =
                await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);

            var result = new List<(string, string)>();
            if (parsed == null) return result;
            // Messages is the parser's own type now (no base-class cast needed).
            foreach (pluginVerilog.Verilog.ParsedDocument.Message m in parsed.Messages)
            {
                result.Add((m.Type.ToString(), m.Text));
            }
            return result;
        }

        private static bool HasContaining(List<(string Type, string Text)> messages, string type, string fragment)
        {
            foreach (var m in messages)
            {
                if (m.Type == type && m.Text.Contains(fragment, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string Describe(List<(string Type, string Text)> messages)
        {
            var parts = new List<string>();
            foreach (var m in messages) parts.Add($"[{m.Type}] {m.Text}");
            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Counts the "undriven" notices. The message text is just
        /// <c>undriven</c> (the variable name is carried by the reference the
        /// notice is attached to), so a test must count them rather than look
        /// for a name.
        /// </summary>
        private static int CountUndriven(List<(string Type, string Text)> messages)
        {
            int count = 0;
            foreach (var m in messages)
            {
                if (m.Text.Contains("undriven", System.StringComparison.Ordinal)) count++;
            }
            return count;
        }

        // ---- 1. unpacked array assigned from a scalar -----------------------

        [Fact]
        public async Task UnpackedArray_AssignedFromScalar_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n  logic [7:0] arr [3:0];\n  initial arr = 8'hFF;\nendmodule\n");
            Assert.True(HasContaining(messages, "Error", "'arr' is an unpacked array"),
                "expected the unpacked array assignment error (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task UnpackedArray_NonBlockingFromScalar_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n  logic clk;\n  logic [7:0] arr [3:0];\n  always @(posedge clk) arr <= 8'hFF;\nendmodule\n");
            Assert.True(HasContaining(messages, "Error", "'arr' is an unpacked array"),
                "expected the unpacked array assignment error (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task UnpackedArray_ContinuousFromScalar_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n  logic [7:0] arr [3:0];\n  assign arr = 8'hFF;\nendmodule\n");
            Assert.True(HasContaining(messages, "Error", "'arr' is an unpacked array"),
                "expected the unpacked array assignment error (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task UnpackedArray_AssignmentPattern_IsNotReported()
        {
            EnsurePluginRegistered();
            // an assignment pattern is the legal way to assign a whole array
            var messages = await ParseAsync(
                "module top;\n  logic [7:0] arr [3:0];\n  initial arr = '{8'h01, 8'h02, 8'h03, 8'h04};\nendmodule\n");
            Assert.False(HasContaining(messages, "Error", "unpacked array"),
                "an assignment pattern must not be reported (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task UnpackedArray_ElementAssignment_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n  logic [7:0] arr [3:0];\n  initial arr[0] = 8'hFF;\nendmodule\n");
            Assert.False(HasContaining(messages, "Error", "unpacked array"),
                "an element assignment must not be reported (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task UnpackedArray_CopyFromSameShapeArray_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n  logic [7:0] a [3:0];\n  logic [7:0] b [3:0];\n  initial a = b;\nendmodule\n");
            Assert.False(HasContaining(messages, "Error", "unpacked array"),
                "a whole-array copy must not be reported (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task PackedVector_AssignedFromScalar_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n  logic [7:0] a;\n  initial a = 8'hFF;\nendmodule\n");
            Assert.False(HasContaining(messages, "Error", "unpacked array"),
                "a packed vector must not be reported (got: " + Describe(messages) + ")");
        }

        // ---- 2. function name assigned from outside -------------------------

        [Fact]
        public async Task FunctionName_AssignedOutsideTheFunction_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n" +
                "  function int get_value;\n    get_value = 1;\n  endfunction\n" +
                "  initial begin\n    get_value = 10;\n  end\n" +
                "endmodule\n");
            Assert.True(HasContaining(messages, "Error", "a function name cannot be assigned"),
                "expected the function-name assignment error (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task FunctionName_AssignedByContinuousAssign_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n" +
                "  function int get_value;\n    get_value = 1;\n  endfunction\n" +
                "  assign get_value = 10;\n" +
                "endmodule\n");
            Assert.True(HasContaining(messages, "Error", "a function name cannot be assigned"),
                "expected the function-name assignment error (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task FunctionName_AssignedByNonBlockingAssignment_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n" +
                "  function int get_value;\n    get_value = 1;\n  endfunction\n" +
                "  initial get_value <= 10;\n" +
                "endmodule\n");
            Assert.True(HasContaining(messages, "Error", "a function name cannot be assigned"),
                "expected the function-name assignment error (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task FunctionReturnVariable_AssignedInsideTheFunction_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n" +
                "  function int get_value;\n    get_value = 10;\n  endfunction\n" +
                "endmodule\n");
            Assert.False(HasContaining(messages, "Error", "function name"),
                "assigning the return variable inside the body must not be reported (got: "
                + Describe(messages) + ")");
        }

        [Fact]
        public async Task FunctionCallOnRightHandSide_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n" +
                "  logic [31:0] x;\n" +
                "  initial x = get_value();\n" +
                "  function int get_value;\n    return 1;\n  endfunction\n" +
                "endmodule\n");
            Assert.False(HasContaining(messages, "Error", "function name"),
                "a function call on the right-hand side must not be reported (got: "
                + Describe(messages) + ")");
        }

        // ---- 3. multiple procedural blocks driving the same variable --------

        [Fact]
        public async Task TwoAlwaysBlocks_AssigningTheSameVariable_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n" +
                "  logic clk;\n  logic a;\n" +
                "  always @(posedge clk) a <= 1'b1;\n" +
                "  always @(posedge clk) a <= 1'b0;\n" +
                "endmodule\n");
            Assert.True(HasContaining(messages, "Warning", "'a' is assigned from multiple procedural blocks"),
                "expected the multiple-procedural-blocks warning (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task AlwaysAndInitial_AssigningTheSameVariable_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n" +
                "  logic clk;\n  logic a;\n" +
                "  always @(posedge clk) a <= 1'b1;\n" +
                "  initial a = 1'b0;\n" +
                "endmodule\n");
            Assert.True(HasContaining(messages, "Warning", "'a' is assigned from multiple procedural blocks"),
                "expected the multiple-procedural-blocks warning (got: " + Describe(messages) + ")");
        }

        /// <summary>
        /// A variable driven only by <c>always_comb</c> is driven: it must not be
        /// reported as "undriven".
        ///
        /// <para>
        /// <c>AssertAssigned()</c> (which fills the assigned map that the
        /// "undriven" notice is derived from) used to sit behind a
        /// <c>clockDomains != null</c> guard. Only an event control created that
        /// list, so <c>always_comb</c> / <c>always_latch</c> - which have no
        /// sensitivity list - never filled the map and every variable they drove
        /// was reported as undriven.
        /// </para>
        /// </summary>
        [Fact]
        public async Task AlwaysComb_DrivenVariable_IsNotReportedAsUndriven()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n" +
                "  logic a, b, c;\n" +
                "  always_comb begin\n" +
                "    a = b;\n" +
                "    c = b;\n" +
                "  end\n" +
                "  assign b = 1'b1;\n" +
                "endmodule\n");
            Assert.Equal(0, CountUndriven(messages));
        }

        [Fact]
        public async Task AlwaysComb_NonBlockingDrivenVariable_IsNotReportedAsUndriven()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n" +
                "  logic a, b;\n" +
                "  always_comb a <= b;\n" +
                "  assign b = 1'b1;\n" +
                "endmodule\n");
            Assert.Equal(0, CountUndriven(messages));
        }

        [Fact]
        public async Task AlwaysLatch_DrivenVariable_IsNotReportedAsUndriven()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n" +
                "  logic en, a, b;\n" +
                "  always_latch if (en) a = b;\n" +
                "  assign en = 1'b1;\n" +
                "  assign b = 1'b0;\n" +
                "endmodule\n");
            Assert.Equal(0, CountUndriven(messages));
        }

        [Fact]
        public async Task AlwaysCombAndInitial_DrivenVariables_AreNotReportedAsUndriven()
        {
            EnsurePluginRegistered();
            // an initial block has no sensitivity list either, so it went through
            // the same code path
            var messages = await ParseAsync(
                "module top;\n" +
                "  logic a, b, c;\n" +
                "  always_comb a = b;\n" +
                "  initial c = b;\n" +
                "  assign b = 1'b0;\n" +
                "endmodule\n");
            Assert.Equal(0, CountUndriven(messages));
        }

        [Fact]
        public async Task AlwaysFfAndAlwaysComb_AssigningTheSameVariable_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n" +
                "  logic clk;\n  logic a, b;\n" +
                "  always_ff @(posedge clk) a <= b;\n" +
                "  always_comb a = b;\n" +
                "endmodule\n");
            Assert.True(HasContaining(messages, "Warning", "'a' is assigned from multiple procedural blocks"),
                "expected the multiple-procedural-blocks warning (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task OneAlwaysBlock_AssigningTheSameVariableTwice_IsNotReported()
        {
            EnsurePluginRegistered();
            // several assignments inside one block (e.g. the two branches of an
            // if / else) are the normal way to describe a register
            var messages = await ParseAsync(
                "module top;\n" +
                "  logic clk, rst;\n  logic a;\n" +
                "  always @(posedge clk) begin\n" +
                "    if (rst) a <= 1'b0;\n" +
                "    else a <= 1'b1;\n" +
                "  end\n" +
                "endmodule\n");
            Assert.False(HasContaining(messages, "Warning", "multiple procedural blocks"),
                "assignments within one block must not be reported (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task TwoAlwaysBlocks_AssigningDifferentVariables_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module top;\n" +
                "  logic clk;\n  logic a, b;\n" +
                "  always @(posedge clk) a <= 1'b1;\n" +
                "  always @(posedge clk) b <= 1'b0;\n" +
                "endmodule\n");
            Assert.False(HasContaining(messages, "Warning", "multiple procedural blocks"),
                "different variables must not be reported (got: " + Describe(messages) + ")");
        }

        [Fact]
        public async Task FunctionBodyAssignment_DoesNotCountAsAProceduralBlock()
        {
            EnsurePluginRegistered();
            // the function body is parsed at its declaration site (module
            // level), so its assignments must not inherit the identifier of the
            // preceding always block
            var messages = await ParseAsync(
                "module top;\n" +
                "  logic clk;\n  logic a;\n" +
                "  always @(posedge clk) a <= get_a();\n" +
                "  function logic get_a;\n    get_a = 1'b1;\n  endfunction\n" +
                "endmodule\n");
            Assert.False(HasContaining(messages, "Warning", "multiple procedural blocks"),
                "a function body must not be treated as a separate block (got: "
                + Describe(messages) + ")");
        }
    }
}
