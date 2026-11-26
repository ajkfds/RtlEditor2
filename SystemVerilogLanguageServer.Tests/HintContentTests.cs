using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemVerilogCore;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// The mouse-over hint used to be built by the parser as an editor
/// <c>PopupItem</c> (coloured spans plus a rendered SVG icon), which is why the
/// parser referenced the editor assembly and Avalonia. The parser now returns
/// plain data (<see cref="SystemVerilogCore.Documents.VerilogHintContent"/>) and
/// the editor draws it.
///
/// <para>
/// These tests pin the data half: the messages covering the caret and the
/// element's declaration label. The drawing half cannot be tested here (it needs
/// Avalonia), so a change that empties the data would show up as "the hint has
/// no detail" in the editor - exactly what these tests prevent.
/// </para>
/// </summary>
public class HintContentTests
{
    /// <summary>
    /// A position on a declared variable must yield its declaration label, so
    /// the hint can show type / width instead of just the name.
    /// </summary>
    [Fact]
    public void HintContent_CarriesTheElementLabel()
    {
        SystemVerilogCore.Verilog.ParsedDocument? parsed = Parse(
            "module top;\n  logic [7:0] data_bus;\n  assign data_bus = 8'h00;\nendmodule\n");
        Assert.NotNull(parsed);

        // One character into the word: the hint refuses a position whose
        // preceding character is a space (the editor's caret is inside the
        // word, which is what this models).
        int index = parsed!.VerilogDocument.CreateString().IndexOf("data_bus", StringComparison.Ordinal) + 1;
        SystemVerilogCore.Documents.VerilogHintContent? content =
            parsed.GetHintContent(index, "data_bus");

        Assert.NotNull(content);
        Assert.NotNull(content!.Label);
        string label = content.Label!.CreateString();
        Assert.Contains("data_bus", label);
        // the declaration's type and range come from the element label
        Assert.Contains("logic", label);
        Assert.Contains("[7:0]", label);
    }

    /// <summary>
    /// Messages covering the position must be reported as plain data with a
    /// severity, so the editor can still colour them and the language server can
    /// render them as text.
    /// </summary>
    [Fact]
    public void HintContent_CarriesTheMessagesAtThePosition()
    {
        // an undeclared identifier produces an "unfound object" error at its
        // own position
        SystemVerilogCore.Verilog.ParsedDocument? parsed = Parse(
            "module top;\n  assign undefined_sig = 1'b0;\nendmodule\n");
        Assert.NotNull(parsed);

        int index = parsed!.VerilogDocument.CreateString().IndexOf("undefined_sig", StringComparison.Ordinal) + 1;
        SystemVerilogCore.Documents.VerilogHintContent? content =
            parsed.GetHintContent(index, "undefined_sig");

        Assert.NotNull(content);
        // measured: an undeclared identifier yields "Implicit net declaration"
        // (Warning) and "unused" (Notice) at its own position
        Assert.NotEmpty(content!.Messages);
        Assert.Contains(content.Messages, m => m.Severity == SystemVerilogCore.Documents.VerilogHintSeverity.Warning);
        Assert.Contains(content.Messages, m => m.Text == "Implicit net declaration");
        Assert.All(content.Messages, m => Assert.False(string.IsNullOrEmpty(m.Text)));
    }

    /// <summary>
    /// A position the hint refuses (the character before the caret is a space)
    /// must report null, so the host shows no hint at all.
    /// </summary>
    /// <remarks>
    /// This is the same guard the editor's hint always had: without it a hint
    /// would appear while the caret sits at the start of a word.
    /// </remarks>
    [Fact]
    public void HintContent_IsNullWhereTheHintIsRefused()
    {
        SystemVerilogCore.Verilog.ParsedDocument? parsed = Parse(
            "module top;\n  logic sig;\nendmodule\n");
        Assert.NotNull(parsed);

        // first character of "sig": the preceding character is a space
        int index = parsed!.VerilogDocument.CreateString().IndexOf("sig", StringComparison.Ordinal);
        Assert.Null(parsed.GetHintContent(index, "sig"));
    }

    private static readonly object _lock = new();

    /// <summary>Parses <paramref name="text"/> with the real parser.</summary>
    private static SystemVerilogCore.Verilog.ParsedDocument? Parse(string text)
    {
        CodeEditor2.Data.Project project;
        pluginVerilog.Data.VerilogFile file;
        lock (_lock)
        {
            project = new SvTestsParserHarness.TestProject();
            project.ProjectProperties.Add(
                pluginVerilog.Plugin.StaticID,
                new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
            file = SvTestsParserHarness.CreateFile(project, "hint_content_test.sv");
        }
        return pluginVerilog.CoreBridge.ParseEngine
            .ParseSystemVerilogAsync(text, file)
            .GetAwaiter().GetResult();
    }
}
