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

    private void DidOpen(LspHandler handler, string uri, string text)
    {
        string json = JsonSerializer.Serialize(new
        {
            textDocument = new { uri, languageId = "systemverilog", version = 1, text },
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
    /// clock / reset (via the <c>// @sync: clock</c> annotation), and the body
    /// is emitted as tab stops.
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
        Assert.Contains("always @(posedge clk or negedge rst_n)", snippetText);
        // the body is a tab stop, not a fixed string
        Assert.Contains("${1", snippetText);
        Assert.Equal("insert", snippets[0].GetProperty("kind").GetString());
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
