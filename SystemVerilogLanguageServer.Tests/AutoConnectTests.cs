using System;
using System.IO;
using System.Text.Json;
using SystemVerilogLanguageServer.Server;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// <c>rtl/autoConnect</c> must return the rows the desktop editor's
/// <c>AutoConnectWindow</c> shows (one per port, with the signal the editor
/// would connect), and <c>rtl/autoConnectApply</c> must rebuild the
/// instantiation with the chosen connections. The matching rule itself lives in
/// the plugin's UI-free <c>CoreBridge.AutoConnectAdapter</c>; these tests pin
/// the wire shape and the parts of the behaviour that are easy to regress.
/// </summary>
public class AutoConnectTests : IDisposable
{
    private readonly string _root;

    public AutoConnectTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "svlsp_autoconnect_" + Guid.NewGuid().ToString("N"));
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
    /// The form lists every port of the instantiated module, with the signal
    /// whose name matches the port (the editor's rule) as the candidate.
    /// </summary>
    [Fact]
    public void AutoConnect_ListsEveryPortWithMatchingCandidates()
    {
        const string text =
            "module child(input clk, input rst_n, output [7:0] data_o);\n" +
            "endmodule\n" +
            "module top;\n" +
            "  wire clk;\n" +
            "  wire rst_n;\n" +
            "  wire [7:0] data_o;\n" +
            "  child u_child();\n" +
            "endmodule\n";
        string uri = WriteFile("ac.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        // caret on the instantiation (line 6, character 8)
        JsonElement result = Request(handler, "rtl/autoConnect", new
        {
            textDocument = new { uri },
            position = new { line = 6, character = 8 },
        });

        Assert.Equal("child", result.GetProperty("moduleName").GetString());
        Assert.Equal("u_child", result.GetProperty("instanceName").GetString());

        JsonElement ports = result.GetProperty("ports");
        Assert.Equal(3, ports.GetArrayLength());

        // every port is listed, in declaration order
        Assert.Equal("clk", ports[0].GetProperty("name").GetString());
        Assert.Equal("input", ports[0].GetProperty("direction").GetString());
        Assert.Equal("rst_n", ports[1].GetProperty("name").GetString());
        Assert.Equal("data_o", ports[2].GetProperty("name").GetString());
        Assert.Equal("output", ports[2].GetProperty("direction").GetString());

        // the candidate is the signal whose name equals the port name
        JsonElement clkCandidates = ports[0].GetProperty("candidates");
        Assert.Equal(1, clkCandidates.GetArrayLength());
        Assert.Equal("clk", clkCandidates[0].GetString());

        // nothing is connected yet
        Assert.Equal(JsonValueKind.Null, ports[0].GetProperty("currentConnection").ValueKind);
    }

    /// <summary>
    /// The editor also matches a port whose name carries an <c>_I</c> /
    /// <c>_O</c> / <c>_IO</c> suffix against the bare signal name.
    /// </summary>
    [Fact]
    public void AutoConnect_StripsPortSuffixWhenMatching()
    {
        const string text =
            "module child(input data_i, output data_o);\n" +
            "endmodule\n" +
            "module top;\n" +
            "  wire data;\n" +
            "  child u_child();\n" +
            "endmodule\n";
        string uri = WriteFile("ac_suffix.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/autoConnect", new
        {
            textDocument = new { uri },
            position = new { line = 4, character = 8 },
        });

        JsonElement ports = result.GetProperty("ports");
        Assert.Equal(2, ports.GetArrayLength());
        Assert.Equal("data", ports[0].GetProperty("candidates")[0].GetString());
        Assert.Equal("data", ports[1].GetProperty("candidates")[0].GetString());
    }

    /// <summary>
    /// Applying the choices rebuilds the instantiation with the selected
    /// connections as a single replacement edit.
    /// </summary>
    [Fact]
    public void AutoConnectApply_RebuildsTheInstantiation()
    {
        const string text =
            "module child(input clk, input rst_n, output [7:0] data_o);\n" +
            "endmodule\n" +
            "module top;\n" +
            "  wire clk;\n" +
            "  wire rst_n;\n" +
            "  wire [7:0] data_o;\n" +
            "  child u_child();\n" +
            "endmodule\n";
        string uri = WriteFile("ac_apply.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/autoConnectApply", new
        {
            textDocument = new { uri },
            position = new { line = 6, character = 8 },
            selections = new
            {
                clk = "clk",
                rst_n = "rst_n",
                data_o = "data_o",
            },
        });

        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(1, result.GetArrayLength());

        JsonElement edit = result[0];
        string newText = edit.GetProperty("newText").GetString() ?? "";
        // the parser's CreateString emits ".port\t( signal )" with tabs
        Assert.Contains(".clk", newText);
        Assert.Contains("clk", newText);
        Assert.Contains(".rst_n", newText);
        Assert.Contains("rst_n", newText);
        Assert.Contains(".data_o", newText);
        Assert.Contains("data_o", newText);

        // the edit covers the instantiation (line 6)
        JsonElement range = edit.GetProperty("range");
        Assert.Equal(6, range.GetProperty("start").GetProperty("line").GetInt32());
    }

    /// <summary>
    /// A port mapped to null is left unconnected (the editor's Reject).
    /// </summary>
    [Fact]
    public void AutoConnectApply_LeavesRejectedPortsUnconnected()
    {
        const string text =
            "module child(input clk, input rst_n);\n" +
            "endmodule\n" +
            "module top;\n" +
            "  wire clk;\n" +
            "  wire rst_n;\n" +
            "  child u_child();\n" +
            "endmodule\n";
        string uri = WriteFile("ac_reject.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/autoConnectApply", new
        {
            textDocument = new { uri },
            position = new { line = 5, character = 8 },
            selections = new
            {
                clk = "clk",
                rst_n = (string?)null,
            },
        });

        Assert.Equal(1, result.GetArrayLength());
        string newText = result[0].GetProperty("newText").GetString() ?? "";
        Assert.Contains(".clk", newText);
        // The rejected port stays in the list but with an empty connection
        // (".rst_n\t(  )"), which is how the editor renders an unconnected
        // port. What must not happen is the signal being wired in.
        Assert.Contains(".rst_n", newText);
        Assert.DoesNotContain("rst_n\t( rst_n )", newText);
    }

    /// <summary>
    /// The caret not being on an instantiation yields an explanatory message
    /// rather than an empty form.
    /// </summary>
    [Fact]
    public void AutoConnect_ReportsWhenTheCaretIsNotOnAnInstantiation()
    {
        const string text =
            "module top;\n" +
            "  wire clk;\n" +
            "endmodule\n";
        string uri = WriteFile("ac_none.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/autoConnect", new
        {
            textDocument = new { uri },
            position = new { line = 1, character = 4 },
        });

        Assert.Equal(0, result.GetProperty("ports").GetArrayLength());
        Assert.False(string.IsNullOrEmpty(result.GetProperty("message").GetString()));
    }
}
