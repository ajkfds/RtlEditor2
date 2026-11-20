using System;
using System.IO;
using System.Text.Json;
using SystemVerilogLanguageServer.Server;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// <c>rtl/snippet</c> must return the text the editor's snippet would insert,
/// so the VS Code extension can apply it with <c>TextEditor.insertSnippet</c>
/// instead of re-implementing the logic. The snippet logic itself lives in the
/// plugin's UI-free <c>CoreBridge.SnippetAdapter</c>; these tests pin the wire
/// shape and the parts of the behaviour that are easy to regress.
/// </summary>
public class SnippetTests : IDisposable
{
    private readonly string _root;

    public SnippetTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "svlsp_snippet_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* best effort */ }
    }

    private LspHandler CreateInitializedHandler()
    {
        LspHandler handler = new(new InMemorySystemVerilogCore());
        string initialize = JsonSerializer.Serialize(new
        {
            processId = 1,
            rootUri = new Uri(_root).AbsoluteUri,
            capabilities = new { },
        });
        using JsonDocument doc = JsonDocument.Parse(initialize);
        handler.TryHandleRequest("initialize", doc.RootElement, default, out _);
        return handler;
    }

    private string WriteFile(string name, string text)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, text);
        return new Uri(path).AbsoluteUri;
    }

    private void DidOpen(LspHandler handler, string uri, string text, string languageId = "systemverilog")
    {
        string json = JsonSerializer.Serialize(new
        {
            textDocument = new { uri, languageId, version = 1, text },
        });
        using JsonDocument doc = JsonDocument.Parse(json);
        handler.TryHandleNotification("textDocument/didOpen", doc.RootElement, default);
    }

    private JsonElement Request(LspHandler handler, string method, object parameters)
    {
        string json = JsonSerializer.Serialize(parameters);
        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.True(handler.TryHandleRequest(method, doc.RootElement, default, out object? result));
        string serialized = JsonSerializer.Serialize(result);
        return JsonDocument.Parse(serialized).RootElement;
    }

    /// <summary>
    /// alwaysFF: the clock / reset come from the ports the parser marked as
    /// clock / reset (via the <c>// @sync: clock</c> annotation).
    ///
    /// <para>
    /// The clock and reset are real tab stops (prefilled with the resolved
    /// port) so the user can change them, and the <c>if(~reset)</c> is a
    /// <b>mirror</b> of the reset tab stop. That is the same interaction the
    /// desktop editor offers (a popup when several candidates exist) and it is
    /// expressed declaratively, so both hosts get it from one template.
    /// </para>
    /// </summary>
    [Fact]
    public void Snippet_AlwaysFF_UsesClockAndResetPorts()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module top(\n" +
            "  input logic clk,   // @sync: clock\n" +
            "  input logic rst_n, // @sync: reset\n" +
            "  output logic q\n" +
            ");\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 4, character = 2 },
            kind = "alwaysFF",
        });

        JsonElement snippets = result.GetProperty("snippets");
        Assert.Equal(1, snippets.GetArrayLength());
        string snippetText = snippets[0].GetProperty("snippetText").GetString() ?? "";

        // clock / reset are prefilled tab stops holding the resolved ports
        Assert.Contains("${1:clk}", snippetText);
        Assert.Contains("${2:rst_n}", snippetText);
        // ...and the if(~reset) repeats tab stop 2 (a mirror, not a copy)
        Assert.Contains("if(~${2}", snippetText);

        // the plain rendering has the same text with the defaults inlined, so
        // the desktop editor inserts exactly what this test reads here.
        // A .sv file gets always_ff (SystemVerilog); a .v file gets always.
        string plainText = snippets[0].GetProperty("plainText").GetString() ?? "";
        Assert.Contains("always_ff @(posedge clk or negedge rst_n)", plainText);
        Assert.Contains("if(~rst_n)", plainText);

        // the fields tell a host which values were suggested. Five entries:
        // clock, reset, the if(~reset) mirror of reset, and the two branches.
        JsonElement fields = snippets[0].GetProperty("fields");
        Assert.Equal(5, fields.GetArrayLength());
        Assert.Equal("clock", fields[0].GetProperty("name").GetString());
        Assert.Equal("clk", fields[0].GetProperty("default").GetString());
        Assert.Equal("reset", fields[1].GetProperty("name").GetString());
        Assert.Equal("rst_n", fields[1].GetProperty("default").GetString());
        Assert.Equal(2, fields[2].GetProperty("index").GetInt32());
        Assert.Equal("insert", snippets[0].GetProperty("kind").GetString());
    }

    /// <summary>
    /// alwaysFF in a Verilog (.v) file: the block keyword is <c>always</c>
    /// rather than SystemVerilog's <c>always_ff</c>. The snippet picks it from
    /// the parse result's language, so both extensions get the right keyword
    /// without the caller telling it.
    /// </summary>
    [Fact]
    public void Snippet_AlwaysFF_UsesAlwaysInVerilog()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module top(\n" +
            "  input clk,   // @sync: clock\n" +
            "  input rst_n, // @sync: reset\n" +
            "  output q\n" +
            ");\n" +
            "endmodule\n";
        string uri = WriteFile("top.v", text);
        DidOpen(handler, uri, text, languageId: "verilog");

        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 4, character = 2 },
            kind = "alwaysFF",
        });

        string plainText = result.GetProperty("snippets")[0].GetProperty("plainText").GetString() ?? "";
        Assert.Contains("always @(posedge clk or negedge rst_n)", plainText);
        Assert.DoesNotContain("always_ff", plainText);
    }

    /// <summary>
    /// moduleInstance without a module name returns the candidate list so the
    /// client can show a picker.
    /// </summary>
    [Fact]
    public void Snippet_ModuleInstance_WithoutArgument_ReturnsCandidates()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module leaf(input logic a, output logic b);\n" +
            "  assign b = a;\n" +
            "endmodule\n" +
            "module top();\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 4, character = 0 },
            kind = "moduleInstance",
        });

        JsonElement candidates = result.GetProperty("candidates");
        Assert.True(candidates.GetArrayLength() >= 2);
        bool hasLeaf = false;
        foreach (JsonElement candidate in candidates.EnumerateArray())
        {
            if (candidate.GetString() == "leaf") hasLeaf = true;
        }
        Assert.True(hasLeaf, "the candidate list must contain the 'leaf' module");
    }

    /// <summary>
    /// moduleInstance with a module name returns a full instantiation: one
    /// named port connection per port, each a tab stop.
    /// </summary>
    [Fact]
    public void Snippet_ModuleInstance_WithArgument_EmitsPortConnections()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module leaf(input logic a, output logic b);\n" +
            "  assign b = a;\n" +
            "endmodule\n" +
            "module top();\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 4, character = 0 },
            kind = "moduleInstance",
            argument = "leaf",
        });

        JsonElement snippets = result.GetProperty("snippets");
        Assert.Equal(1, snippets.GetArrayLength());
        string snippetText = snippets[0].GetProperty("snippetText").GetString() ?? "";
        Assert.Contains("leaf ", snippetText);
        Assert.Contains(".a", snippetText);
        Assert.Contains(".b", snippetText);
        // the instance name is a tab stop so the user can rename it
        Assert.Contains("${1:", snippetText);

        // The port connections must be BLANK tab stops: the editor's
        // ModuleInstanceSnippet emits `.port(  )` and never guesses a signal,
        // so a placeholder default (`${2:a}`) must not come back. The instance
        // name is tab stop 1, so the ports start at 2.
        Assert.Contains("( ${2} )", snippetText);
        Assert.Contains("( ${3} )", snippetText);
        Assert.DoesNotContain("${2:a}", snippetText);
        Assert.DoesNotContain("${3:b}", snippetText);

        // The generated text must be a complete instantiation: the port list
        // opened after the instance name has to be closed. A missing ")"
        // (which happened when the builder moved out of SnippetAdapter) still
        // contains every port name, so only a parenthesis check catches it -
        // and the inserted text would not parse, making the instance vanish
        // from the hierarchy.
        string plainText = snippets[0].GetProperty("plainText").GetString() ?? "";
        Assert.Equal(CountChar(plainText, '('), CountChar(plainText, ')'));
        Assert.EndsWith(");", plainText.TrimEnd());
    }

    private static int CountChar(string text, char target)
    {
        int count = 0;
        foreach (char c in text) if (c == target) count++;
        return count;
    }

    /// <summary>
    /// portConnectionCreate fills the unconnected ports of an existing
    /// instantiation: the signal name is the port name without its
    /// <c>_I</c> / <c>_O</c> direction suffix and lower-cased, and the signal is
    /// declared (with the port's type) when the enclosing module does not have
    /// it yet.
    /// </summary>
    /// <remarks>
    /// This is the behaviour the editor's original snippet had. It was lost when
    /// the text generation moved into the shared
    /// <c>SnippetBuilder.BuildInstantiationTemplate</c>, which left every
    /// connection blank - i.e. the tool "did nothing".
    /// </remarks>
    [Fact]
    public void Snippet_PortConnectionCreate_DerivesAndDeclaresTheSignals()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module leaf (\n" +
            "  input logic CLK_I,\n" +
            "  input logic [7:0] DATA_I,\n" +
            "  output logic [7:0] RESULT_O\n" +
            ");\n" +
            "endmodule\n" +
            "module top;\n" +
            "  leaf u_leaf ();\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 7, character = 9 },
            kind = "portConnectionCreate",
        });

        string plainText = result.GetProperty("snippets")[0].GetProperty("plainText").GetString() ?? "";

        // the suffix is stripped and the rest lower-cased
        Assert.Contains(".CLK_I\t( clk )", plainText);
        Assert.Contains(".DATA_I\t( data )", plainText);
        Assert.Contains(".RESULT_O\t( result )", plainText);

        // ...and the signals are declared with the port's own width, before the
        // statement that uses them
        Assert.Contains("wire clk;", plainText);
        Assert.Contains("[7:0] data;", plainText);
        Assert.Contains("[7:0] result;", plainText);
        Assert.True(
            plainText.IndexOf("wire clk;", StringComparison.Ordinal)
                < plainText.IndexOf("leaf u_leaf", StringComparison.Ordinal),
            "the declarations must come before the instantiation");
    }

    /// <summary>
    /// An already connected port keeps its expression, and a signal the
    /// enclosing module already declares is not declared a second time.
    /// </summary>
    [Fact]
    public void Snippet_PortConnectionCreate_KeepsConnectionsAndExistingSignals()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module leaf (\n" +
            "  input logic CLK_I,\n" +
            "  output logic [7:0] RESULT_O\n" +
            ");\n" +
            "endmodule\n" +
            "module top;\n" +
            "  wire my_clk;\n" +
            "  wire [7:0] result;\n" +
            "  leaf u_leaf (.CLK_I(my_clk));\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 8, character = 9 },
            kind = "portConnectionCreate",
        });

        string plainText = result.GetProperty("snippets")[0].GetProperty("plainText").GetString() ?? "";

        // the existing connection is kept, not replaced by the derived name
        Assert.Contains(".CLK_I\t( my_clk )", plainText);
        Assert.DoesNotContain(".CLK_I\t( clk )", plainText);

        // both signals are already declared, so nothing is declared here
        Assert.DoesNotContain("wire clk;", plainText);
        Assert.DoesNotContain("wire  [7:0] result;", plainText);
        Assert.StartsWith("leaf u_leaf", plainText);
    }

    /// <summary>
    /// autoFormat reformats only the instantiation at the caret (the editor's
    /// tool scope), not the whole document - that is what distinguishes it
    /// from the LSP <c>textDocument/formatting</c> feature.
    /// </summary>
    [Fact]
    public void Snippet_AutoFormat_ReformatsTheInstantiationAtTheCaret()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module leaf(input logic a, output logic b);\n" +
            "  assign b = a;\n" +
            "endmodule\n" +
            "module top();\n" +
            "  leaf u_leaf(.a(sig), .b(out_sig));\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        // caret inside the instantiation on line 4
        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 4, character = 10 },
            kind = "autoFormat",
        });

        JsonElement snippets = result.GetProperty("snippets");
        Assert.Equal(1, snippets.GetArrayLength());
        Assert.Equal("replace", snippets[0].GetProperty("kind").GetString());
        string snippetText = snippets[0].GetProperty("snippetText").GetString() ?? "";
        // the replaced range is the instantiation only, so the module
        // declaration on line 3 stays untouched
        Assert.Contains("u_leaf", snippetText);
        Assert.Contains(".a", snippetText);
        Assert.DoesNotContain("endmodule", snippetText);
        Assert.True(
            snippets[0].GetProperty("start").GetInt32() > 0,
            "the replacement must start at the instantiation, not at the document start");
    }

    /// <summary>
    /// autoFormat outside an instantiation reports a message (the menu row is
    /// disabled for the same reason).
    /// </summary>
    [Fact]
    public void Snippet_AutoFormat_OutsideInstantiation_ReportsMessage()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text = "module top();\nendmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 0, character = 8 },
            kind = "autoFormat",
        });

        Assert.Equal(0, result.GetProperty("snippets").GetArrayLength());
        Assert.Contains("module instantiation", result.GetProperty("message").GetString());
    }

    /// <summary>
    /// portInvert flips input/output and the _I / _O suffix of the selected
    /// port declarations, and reports the range to replace.
    /// </summary>
    [Fact]
    public void Snippet_PortInvert_FlipsDirectionAndSuffix()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module top(\n" +
            "  input  wire [7:0] data_I,\n" +
            "  output wire [7:0] data_O\n" +
            ");\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 1, character = 2 },
            kind = "portInvert",
            selection = new
            {
                start = new { line = 1, character = 0 },
                // line 2 is "  output wire [7:0] data_O" (26 characters)
                end = new { line = 2, character = 26 },
            },
        });

        JsonElement snippets = result.GetProperty("snippets");
        Assert.Equal(1, snippets.GetArrayLength());
        Assert.Equal("replace", snippets[0].GetProperty("kind").GetString());
        string snippetText = snippets[0].GetProperty("snippetText").GetString() ?? "";
        Assert.Contains("output", snippetText);
        Assert.Contains("data_O", snippetText);
        Assert.Contains("input", snippetText);
        Assert.Contains("data_I", snippetText);
    }

    /// <summary>
    /// The plain rendering and the tab-stop rendering must agree, because the
    /// desktop editor inserts <c>plainText</c> while the VS Code extension
    /// inserts <c>snippetText</c>. This pins the single-template contract: with
    /// every field left at its default, stripping the tab stop markers from
    /// <c>snippetText</c> has to give <c>plainText</c>.
    /// </summary>
    [Fact]
    public void Snippet_PlainText_MatchesTheTabStopRendering()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module leaf(input logic a, output logic b);\n" +
            "  assign b = a;\n" +
            "endmodule\n" +
            "module top();\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 4, character = 0 },
            kind = "moduleInstance",
            argument = "leaf",
        });

        JsonElement snippet = result.GetProperty("snippets")[0];
        string snippetText = snippet.GetProperty("snippetText").GetString() ?? "";
        string plainText = snippet.GetProperty("plainText").GetString() ?? "";

        // Rebuild the literal text from the tab stops: ${n:default} keeps the
        // default, ${n} and ${n|...|} keep nothing (a blank field).
        string stripped = System.Text.RegularExpressions.Regex.Replace(
            snippetText,
            @"\$\{(\d+)(?::([^}]*))?\}",
            m => m.Groups[2].Success ? m.Groups[2].Value : "");
        Assert.Equal(plainText, stripped);
        Assert.Contains("leaf_0", plainText);
    }

    /// <summary>
    /// A snippet without tab stops is literal, so the extension inserts
    /// <c>plainText</c> and a stray <c>$</c> / <c>}</c> in the code is not read
    /// as snippet syntax.
    /// </summary>
    [Fact]
    public void Snippet_WithoutFields_IsLiteral()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module leaf(input logic a, output logic b);\n" +
            "  assign b = a;\n" +
            "endmodule\n" +
            "module top();\n" +
            "  leaf u_leaf(.a({sig_a, sig_b}), .b(out_sig));\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 4, character = 6 },
            kind = "autoFormat",
        });

        JsonElement snippet = result.GetProperty("snippets")[0];
        Assert.Equal(0, snippet.GetProperty("fields").GetArrayLength());
        string plainText = snippet.GetProperty("plainText").GetString() ?? "";
        // the concatenation survives (it used to be dropped by CreateString)
        Assert.Contains("{sig_a, sig_b}", plainText);
        Assert.Equal(plainText, snippet.GetProperty("snippetText").GetString());
    }

    /// <summary>
    /// modport outside an interface reports a message instead of throwing.
    /// </summary>
    [Fact]
    public void Snippet_ModPort_OutsideInterface_ReportsMessage()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module top();\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 0, character = 8 },
            kind = "modport",
        });

        Assert.Equal(0, result.GetProperty("snippets").GetArrayLength());
        Assert.False(string.IsNullOrEmpty(result.GetProperty("message").GetString()));
    }

    /// <summary>
    /// An unknown snippet kind is reported, not thrown.
    /// </summary>
    [Fact]
    public void Snippet_UnknownKind_ReportsMessage()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text = "module top();\nendmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/snippet", new
        {
            textDocument = new { uri },
            position = new { line = 0, character = 0 },
            kind = "noSuchSnippet",
        });

        Assert.Equal(0, result.GetProperty("snippets").GetArrayLength());
        Assert.Contains("unknown snippet", result.GetProperty("message").GetString());
    }
}
