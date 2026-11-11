using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    /// <summary>
    /// Sync-domain checking of assignments.
    ///
    /// <para>
    /// A signal declares the clock domain it belongs to with a
    /// <c>// @sync:&lt;clock&gt;</c> annotation and the clocks / resets with
    /// <c>// @sync:clock</c> / <c>// @sync:reset</c>. Two checks must run on every
    /// assignment:
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     the enclosing event control's clock must match the left-hand side's
    ///     declared domain (<c>sync mismatch &lt;clock&gt; assigned</c>), and
    ///   </item>
    ///   <item>
    ///     the right-hand side's domain must match the left-hand side's domain
    ///     (<c>sync mismatch</c>) - i.e. no value crosses a clock domain without
    ///     a synchroniser.
    ///   </item>
    /// </list>
    /// <para>
    /// The non-blocking path only performed check 1 and never looked at the
    /// right-hand side, and the blocking path only performed check 2. Because
    /// verilog_sample/mySample/mod2.v drives <c>DATA1_O</c> (annotated
    /// <c>@sync:CLK2_I</c>, clocked by <c>CLK2_I</c>) from <c>DATA_I</c>
    /// (annotated <c>@sync:CLK_I</c>), neither check fired and the file looked
    /// clean.
    /// </para>
    /// </summary>
    public class SyncDomainTests : SvTestsParserHarness
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
                file = CreateFile(project, "sync_domain_test.sv");
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

        private static int CountWarnings(List<(string Type, string Text)> messages, string text)
        {
            int count = 0;
            foreach (var m in messages)
            {
                if (m.Type == "Warning" && m.Text == text) count++;
            }
            return count;
        }

        private static bool HasWarningContaining(List<(string Type, string Text)> messages, string fragment)
        {
            foreach (var m in messages)
            {
                if (m.Type == "Warning" && m.Text.Contains(fragment, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // ---- right-hand side cross-domain -----------------------------------

        [Fact]
        public async Task NonBlockingAssignment_FromAnotherClockDomain_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module m(\n" +
                "  input [7:0] D_I, // @sync:CLK1_I\n" +
                "  input CLK1_I, // @sync:clock\n" +
                "  input CLK2_I, // @sync:clock\n" +
                "  output reg [7:0] Q_O // @sync:CLK2_I\n" +
                ");\n" +
                "always @(posedge CLK2_I) Q_O <= D_I;\n" +
                "endmodule\n");
            Assert.Equal(1, CountWarnings(messages, "sync mismatch"));
        }

        [Fact]
        public async Task BlockingAssignment_FromAnotherClockDomain_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module m(\n" +
                "  input [7:0] D_I, // @sync:CLK1_I\n" +
                "  input CLK1_I, // @sync:clock\n" +
                "  input CLK2_I, // @sync:clock\n" +
                "  output reg [7:0] Q_O // @sync:CLK2_I\n" +
                ");\n" +
                "always @(posedge CLK2_I) Q_O = D_I;\n" +
                "endmodule\n");
            Assert.Equal(1, CountWarnings(messages, "sync mismatch"));
        }

        [Fact]
        public async Task ContinuousAssignment_FromAnotherClockDomain_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module m(\n" +
                "  input [7:0] D_I, // @sync:CLK1_I\n" +
                "  input CLK1_I, // @sync:clock\n" +
                "  input CLK2_I, // @sync:clock\n" +
                "  output [7:0] Q_O // @sync:CLK2_I\n" +
                ");\n" +
                "assign Q_O = D_I;\n" +
                "endmodule\n");
            Assert.Equal(1, CountWarnings(messages, "sync mismatch"));
        }

        /// <summary>
        /// The sample file that motivated this check: DATA_I is in the CLK_I
        /// domain while DATA1_O is annotated CLK2_I and clocked by CLK2_I, so the
        /// capture crosses clock domains and must be reported.
        /// </summary>
        [Fact]
        public async Task SampleMod2_CrossDomainCapture_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module mod2(\n" +
                "input [7:0] DATA_I, // @sync:CLK_I\n" +
                "input   CLK_I, // @sync:clock\n" +
                "input   RST_X_I, // @sync:reset\n" +
                "output reg [7:0] DATA1_O, // @sync:CLK2_I\n" +
                "input   CLK2_I, // @sync:clock\n" +
                "input   RST2_X_I // @sync:reset\n" +
                ");\n" +
                "localparam P_DELAY = 1;\n" +
                "always @(posedge CLK2_I or negedge RST2_X_I)\n" +
                "begin\n" +
                "  if(~RST2_X_I) begin DATA1_O <= #P_DELAY 8'h00; end\n" +
                "  else begin DATA1_O <= #P_DELAY DATA_I; end\n" +
                "end\n" +
                "endmodule\n");
            Assert.Equal(1, CountWarnings(messages, "sync mismatch"));
        }

        // ---- left-hand side declared domain vs the driving clock ------------

        [Fact]
        public async Task NonBlockingAssignment_DrivenByAnotherClock_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module m(\n" +
                "  input [7:0] D_I, // @sync:CLK2_I\n" +
                "  input CLK1_I, // @sync:clock\n" +
                "  input CLK2_I, // @sync:clock\n" +
                "  output reg [7:0] Q_O // @sync:CLK1_I\n" +
                ");\n" +
                "always @(posedge CLK2_I) Q_O <= D_I;\n" +
                "endmodule\n");
            Assert.True(HasWarningContaining(messages, "sync mismatch CLK2_I assigned"),
                "expected 'sync mismatch CLK2_I assigned' (got: " + string.Join(" | ", messages) + ")");
        }

        [Fact]
        public async Task BlockingAssignment_DrivenByAnotherClock_IsReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module m(\n" +
                "  input [7:0] D_I, // @sync:CLK2_I\n" +
                "  input CLK1_I, // @sync:clock\n" +
                "  input CLK2_I, // @sync:clock\n" +
                "  output reg [7:0] Q_O // @sync:CLK1_I\n" +
                ");\n" +
                "always @(posedge CLK2_I) Q_O = D_I;\n" +
                "endmodule\n");
            Assert.True(HasWarningContaining(messages, "sync mismatch CLK2_I assigned"),
                "expected 'sync mismatch CLK2_I assigned' (got: " + string.Join(" | ", messages) + ")");
        }

        // ---- negative cases -------------------------------------------------

        [Fact]
        public async Task SameClockDomain_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module m(\n" +
                "  input [7:0] D_I, // @sync:CLK2_I\n" +
                "  input CLK2_I, // @sync:clock\n" +
                "  output reg [7:0] Q_O // @sync:CLK2_I\n" +
                ");\n" +
                "always @(posedge CLK2_I) Q_O <= D_I;\n" +
                "endmodule\n");
            Assert.False(HasWarningContaining(messages, "sync mismatch"),
                "unexpected sync warning (got: " + string.Join(" | ", messages) + ")");
        }

        [Fact]
        public async Task ResetBranchWithLiteral_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module m(\n" +
                "  input CLK2_I, // @sync:clock\n" +
                "  input RST_X_I, // @sync:reset\n" +
                "  output reg [7:0] Q_O // @sync:CLK2_I\n" +
                ");\n" +
                "always @(posedge CLK2_I or negedge RST_X_I)\n" +
                "  if (~RST_X_I) Q_O <= 8'h00; else Q_O <= 8'h01;\n" +
                "endmodule\n");
            Assert.False(HasWarningContaining(messages, "sync mismatch"),
                "unexpected sync warning (got: " + string.Join(" | ", messages) + ")");
        }

        /// <summary>
        /// Without any <c>// @sync:</c> annotation there is no declared domain, so
        /// the left-hand side adopts the right-hand side's / enclosing clock's
        /// domain instead of reporting a mismatch.
        /// </summary>
        [Fact]
        public async Task NoAnnotation_IsNotReported()
        {
            EnsurePluginRegistered();
            var messages = await ParseAsync(
                "module m(\n" +
                "  input [7:0] D_I,\n" +
                "  input CLK2_I,\n" +
                "  output reg [7:0] Q_O\n" +
                ");\n" +
                "always @(posedge CLK2_I) Q_O <= D_I;\n" +
                "endmodule\n");
            Assert.False(HasWarningContaining(messages, "sync mismatch"),
                "unexpected sync warning (got: " + string.Join(" | ", messages) + ")");
        }
    }
}
