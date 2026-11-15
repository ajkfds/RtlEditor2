using System;
using System.IO;
using System.Text.Json;
using SystemVerilogLanguageServer.Server;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// <c>rtl/hierarchyMenu</c> / <c>rtl/hierarchyMenuInvoke</c> must serve the
/// RTL Hierarchy node menu that the plugin defines, so a menu entry written in
/// C# reaches VS Code without any client change.
///
/// <para>
/// VS Code declares <c>view/item/context</c> entries statically in
/// <c>package.json</c>, so the client can only offer a fixed set of rows. The
/// extension therefore registers one static "Show Menu..." command and asks the
/// server which rows apply to the selected node; the rows come from the plugin's
/// <c>CoreBridge.HierarchyMenuCatalog</c>. These tests pin that contract.
/// </para>
/// </summary>
public class HierarchyMenuTests : IDisposable
{
    private readonly string _root;

    public HierarchyMenuTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "svlsp_hierarchymenu_" + Guid.NewGuid().ToString("N"));
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
        Assert.True(handler.TryHandleRequest(method, doc.RootElement, default, out object? result),
            method + " was not handled");
        return JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement;
    }

    /// <summary>
    /// The catalog is the single source of truth: whatever it holds must come
    /// back over the wire, ids and labels included. A new entry added in C#
    /// therefore shows up in VS Code with no client change.
    /// </summary>
    [Fact]
    public void Menu_ServesTheCatalogEntries()
    {
        LspHandler handler = CreateInitializedHandler();

        JsonElement result = Request(handler, "rtl/hierarchyMenu", new { kind = "instance" });
        JsonElement entries = result.GetProperty("entries");
        Assert.True(entries.GetArrayLength() > 0, "the instance menu must not be empty");

        foreach (pluginVerilog.CoreBridge.HierarchyMenuCatalog.Entry entry
                 in pluginVerilog.CoreBridge.HierarchyMenuCatalog.All)
        {
            if ((entry.Targets & pluginVerilog.CoreBridge.HierarchyMenuCatalog.AppliesTo.Instance) == 0) continue;
            // A submenu only groups rows. The native VS Code menu renders it as
            // a flyout; a QuickPick cannot nest, so rtl/hierarchyMenu lists its
            // children (prefixed with the submenu label) instead of the
            // grouping row itself.
            if (entry.IsSubmenu) continue;

            bool found = false;
            foreach (JsonElement e in entries.EnumerateArray())
            {
                if (e.GetProperty("id").GetString() == entry.Id) { found = true; break; }
            }
            Assert.True(found, "menu is missing catalog entry " + entry.Id);
        }

        // the submenu's children are listed, so the row is still reachable
        // from the QuickPick fallback
        bool hasNested = false;
        foreach (JsonElement e in entries.EnumerateArray())
        {
            if (e.GetProperty("id").GetString()
                == pluginVerilog.CoreBridge.HierarchyMenuCatalog.MenuId.SimulationSetup) hasNested = true;
        }
        Assert.True(hasNested, "the submenu's child row must be listed in the QuickPick");
    }

    /// <summary>
    /// The simulation setup row is the C#-side feature this menu exists for: it
    /// must be offered for a module file and produce the collected file list.
    /// </summary>
    [Fact]
    public void SimulationSetupEntry_IsOfferedAndListsTheFiles()
    {
        // The files must exist BEFORE initialize: initialize scans the workspace
        // roots into the project, and SimulationSetup only walks the files the
        // project knows (a file created later is picked up by
        // workspace/didChangeWatchedFiles, which this test does not exercise).
        string leafText = "module leaf(input a, output b);\n  assign b = a;\nendmodule\n";
        string topText = "module top;\n  leaf u_leaf(.a(1'b0), .b());\nendmodule\n";
        WriteFile("leaf.sv", leafText);
        string topUri = WriteFile("top.sv", topText);

        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, topUri, topText);

        JsonElement menu = Request(handler, "rtl/hierarchyMenu", new
        {
            kind = "file",
            uri = topUri,
            label = "top",
        });
        bool offered = false;
        foreach (JsonElement entry in menu.GetProperty("entries").EnumerateArray())
        {
            if (entry.GetProperty("id").GetString()
                != pluginVerilog.CoreBridge.HierarchyMenuCatalog.MenuId.SimulationSetup) continue;
            offered = true;
            Assert.True(entry.GetProperty("enabled").GetBoolean(),
                "simulation setup must be enabled for a parsed module file");
        }
        Assert.True(offered, "the simulation setup row must be offered for a file node");

        JsonElement invoked = Request(handler, "rtl/hierarchyMenuInvoke", new
        {
            id = pluginVerilog.CoreBridge.HierarchyMenuCatalog.MenuId.SimulationSetup,
            kind = "file",
            uri = topUri,
            label = "top",
        });

        string text = invoked.GetProperty("text").GetString() ?? string.Empty;
        Assert.Contains("top: top", text);
        // the instantiated module's source is part of the compile list
        Assert.Contains("leaf.sv", text);
    }

    /// <summary>
    /// Copying the instance path is a text result, so the client only has to put
    /// it on the clipboard: the server decides the value.
    /// </summary>
    [Fact]
    public void CopyInstancePath_ReturnsTheInstancePath()
    {
        LspHandler handler = CreateInitializedHandler();

        JsonElement result = Request(handler, "rtl/hierarchyMenuInvoke", new
        {
            id = pluginVerilog.CoreBridge.HierarchyMenuCatalog.MenuId.CopyInstancePath,
            kind = "instance",
            instancePath = "u_mid.u_leaf",
        });

        Assert.Equal("u_mid.u_leaf", result.GetProperty("clipboardText").GetString());
    }

    /// <summary>
    /// An entry that does not apply to the node must be reported with a reason
    /// instead of silently producing nothing (the QuickPick greys it out).
    /// </summary>
    [Fact]
    public void CopyInstancePath_WithoutAnInstancePath_IsDisabledWithAReason()
    {
        LspHandler handler = CreateInitializedHandler();

        JsonElement menu = Request(handler, "rtl/hierarchyMenu", new { kind = "instance" });
        foreach (JsonElement entry in menu.GetProperty("entries").EnumerateArray())
        {
            if (entry.GetProperty("id").GetString()
                != pluginVerilog.CoreBridge.HierarchyMenuCatalog.MenuId.CopyInstancePath) continue;
            Assert.False(entry.GetProperty("enabled").GetBoolean());
            Assert.False(string.IsNullOrEmpty(entry.GetProperty("reason").GetString()));
            return;
        }
        Assert.Fail("the copy instance path row must be listed");
    }

    /// <summary>
    /// Folder nodes only get the file-system style entries: nothing that needs a
    /// parse result may be offered there.
    /// </summary>
    [Fact]
    public void FolderMenu_DoesNotOfferParseDependentEntries()
    {
        LspHandler handler = CreateInitializedHandler();

        JsonElement menu = Request(handler, "rtl/hierarchyMenu", new { kind = "folder" });
        foreach (JsonElement entry in menu.GetProperty("entries").EnumerateArray())
        {
            string id = entry.GetProperty("id").GetString() ?? string.Empty;
            Assert.NotEqual(pluginVerilog.CoreBridge.HierarchyMenuCatalog.MenuId.SimulationSetup, id);
            Assert.NotEqual(pluginVerilog.CoreBridge.HierarchyMenuCatalog.MenuId.CopyInstancePath, id);
        }
    }

    /// <summary>
    /// A plugin can append its own row; the server serves it and the client
    /// needs no change (this is the "add a menu from C#" requirement).
    /// </summary>
    [Fact]
    public void Register_AddsAnEntryFromAnotherPlugin()
    {
        const string id = "testPluginRunSomething";
        pluginVerilog.CoreBridge.HierarchyMenuCatalog.Register(
            new pluginVerilog.CoreBridge.HierarchyMenuCatalog.Entry
            {
                Id = id,
                Label = "Run Something",
                Detail = "added by a plugin",
                Targets = pluginVerilog.CoreBridge.HierarchyMenuCatalog.AppliesTo.Instance,
                Group = 99,
            });

        try
        {
            LspHandler handler = CreateInitializedHandler();
            JsonElement menu = Request(handler, "rtl/hierarchyMenu", new { kind = "instance" });
            bool found = false;
            foreach (JsonElement entry in menu.GetProperty("entries").EnumerateArray())
            {
                if (entry.GetProperty("id").GetString() != id) continue;
                found = true;
                Assert.Equal("Run Something", entry.GetProperty("label").GetString());
            }
            Assert.True(found, "a registered entry must be served by rtl/hierarchyMenu");
        }
        finally
        {
            pluginVerilog.CoreBridge.HierarchyMenuCatalog.Unregister(id);
        }
    }
}
