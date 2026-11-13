using System;
using System.IO;
using System.Text.Json;
using SystemVerilogLanguageServer.Server;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// <c>rtl/toolMenu</c> must return the same tool list the desktop editor shows
/// on Shift+Space, with each row's availability decided by the server.
///
/// <para>
/// The catalog itself lives in the plugin's <c>CoreBridge.ToolMenuCatalog</c>,
/// so these tests pin the wire shape (ids, enabled flag, reason) and the two
/// things that are easy to regress: the whole catalog is offered, and a tool
/// that cannot run at the caret is reported as disabled instead of silently
/// doing nothing.
/// </para>
/// </summary>
public class ToolMenuTests : IDisposable
{
    private readonly string _root;

    public ToolMenuTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "svlsp_toolmenu_" + Guid.NewGuid().ToString("N"));
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

    private JsonElement RequestMenu(LspHandler handler, string uri, int line, int character)
    {
        return Request(handler, "rtl/toolMenu", new
        {
            textDocument = new { uri },
            position = new { line, character },
        });
    }

    /// <summary>Finds a row by id, failing the test when it is missing.</summary>
    private static JsonElement FindEntry(JsonElement result, string id)
    {
        foreach (JsonElement entry in result.GetProperty("entries").EnumerateArray())
        {
            if (entry.GetProperty("id").GetString() == id) return entry;
        }
        Assert.Fail("the tool menu has no entry '" + id + "'");
        return default;
    }

    /// <summary>
    /// Every tool of the editor's Shift+Space list is offered, in the same
    /// order, so the two menus cannot drift apart.
    /// </summary>
    [Fact]
    public void ToolMenu_OffersEveryCatalogEntry()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module top(input logic clk, output logic q);\n" +
            "  assign q = clk;\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = RequestMenu(handler, uri, 1, 2);

        string[] expected =
        {
            "alwaysFF", "autoConnect", "autoFormat", "moduleInstance",
            "portConnectionCreate", "portInvert", "modport", "connectionCheck",
            "toUpper", "toLower",
        };
        JsonElement entries = result.GetProperty("entries");
        Assert.Equal(expected.Length, entries.GetArrayLength());
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], entries[i].GetProperty("id").GetString());
            Assert.NotEqual("", entries[i].GetProperty("label").GetString());
        }
    }

    /// <summary>
    /// The selection-based tools are disabled (with a reason) until something
    /// is selected, which is the part the editor cannot show.
    /// </summary>
    [Fact]
    public void ToolMenu_SelectionTools_AreDisabledWithoutASelection()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module top(input logic clk);\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = RequestMenu(handler, uri, 1, 0);

        foreach (string id in new[] { "portInvert", "toUpper", "toLower" })
        {
            JsonElement entry = FindEntry(result, id);
            Assert.False(entry.GetProperty("enabled").GetBoolean());
            Assert.False(string.IsNullOrEmpty(entry.GetProperty("reason").GetString()));
        }
    }

    /// <summary>
    /// With a selection the case transforms become available (the reason must
    /// disappear, not just the flag flip).
    /// </summary>
    [Fact]
    public void ToolMenu_SelectionTools_AreEnabledWithASelection()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module top(input logic clk);\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "rtl/toolMenu", new
        {
            textDocument = new { uri },
            position = new { line = 1, character = 0 },
            selection = new
            {
                start = new { line = 0, character = 0 },
                end = new { line = 0, character = 6 },
            },
        });

        JsonElement entry = FindEntry(result, "toUpper");
        Assert.True(entry.GetProperty("enabled").GetBoolean());
        Assert.True(string.IsNullOrEmpty(entry.GetProperty("reason").GetString()));
    }

    /// <summary>
    /// A tool that only rewrites an instantiation is disabled when the caret is
    /// elsewhere, and enabled on the instantiation itself.
    /// </summary>
    [Fact]
    public void ToolMenu_InstantiationTools_FollowTheCaret()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module leaf(input logic a, output logic b);\n" +
            "  assign b = a;\n" +
            "endmodule\n" +
            "module top();\n" +
            "  leaf u_leaf();\n" +
            "endmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        // line 3 (`module top();`) is not on the instantiation
        JsonElement offMenu = RequestMenu(handler, uri, 3, 0);
        JsonElement offEntry = FindEntry(offMenu, "autoFormat");
        Assert.False(offEntry.GetProperty("enabled").GetBoolean());
        Assert.Contains("module instantiation", offEntry.GetProperty("reason").GetString());

        // line 4 is the instantiation itself
        JsonElement onMenu = RequestMenu(handler, uri, 4, 4);
        Assert.True(FindEntry(onMenu, "autoFormat").GetProperty("enabled").GetBoolean());
    }

    /// <summary>
    /// A tool that needs a window only the desktop editor owns is flagged, so
    /// the client can hide it instead of dispatching a command that cannot work.
    /// </summary>
    [Fact]
    public void ToolMenu_FlagsDesktopOnlyTools()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text = "module top();\nendmodule\n";
        string uri = WriteFile("top.sv", text);
        DidOpen(handler, uri, text);

        JsonElement result = RequestMenu(handler, uri, 1, 0);

        JsonElement check = FindEntry(result, "connectionCheck");
        Assert.True(check.GetProperty("desktopOnly").GetBoolean());
        Assert.False(check.GetProperty("enabled").GetBoolean());

        Assert.False(FindEntry(result, "alwaysFF").GetProperty("desktopOnly").GetBoolean());
    }

    /// <summary>
    /// The desktop editor's own list (the Shift+Space menu) must keep the
    /// selection-based port inversion: <c>CodeView</c> only appends the case
    /// transforms itself, so filtering the editor list on "needs a selection"
    /// would silently drop PortInvert from the editor's menu.
    /// </summary>
    [Fact]
    public void EditorToolList_KeepsPortInvert_AndDoesNotDuplicateCaseTransforms()
    {
        var ids = new System.Collections.Generic.List<string>();
        foreach (pluginVerilog.CoreBridge.ToolMenuCatalog.Entry entry
                 in pluginVerilog.CoreBridge.ToolMenuCatalog.EditorTools)
        {
            ids.Add(entry.Id);
        }

        Assert.Contains(pluginVerilog.CoreBridge.ToolMenuCatalog.ToolId.PortInvert, ids);
        // CodeView.ShowToolSelectionPopupMenu appends these itself
        Assert.DoesNotContain(pluginVerilog.CoreBridge.ToolMenuCatalog.ToolId.ToUpper, ids);
        Assert.DoesNotContain(pluginVerilog.CoreBridge.ToolMenuCatalog.ToolId.ToLower, ids);
    }

    /// <summary>
    /// An unknown file must not crash the request; the client shows the message.
    /// </summary>
    [Fact]
    public void ToolMenu_UnknownFile_ReturnsMessage()
    {
        LspHandler handler = CreateInitializedHandler();

        JsonElement result = RequestMenu(
            handler, new Uri(Path.Combine(_root, "missing.sv")).AbsoluteUri, 0, 0);

        Assert.Equal(0, result.GetProperty("entries").GetArrayLength());
        Assert.False(string.IsNullOrEmpty(result.GetProperty("message").GetString()));
    }
}
