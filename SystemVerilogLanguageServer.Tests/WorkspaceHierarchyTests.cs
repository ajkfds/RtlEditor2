using System;
using System.IO;
using System.Text.Json;
using SystemVerilogLanguageServer.Server;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// The "RTL Hierarchy" tree view must show the whole workspace, not only the
/// documents the client has open, so these tests drive <c>rtl/hierarchy</c>
/// against a real temp workspace on disk.
/// </summary>
public class WorkspaceHierarchyTests : IDisposable
{
    private readonly string _root;

    public WorkspaceHierarchyTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "svlsp_hier_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "src", "rtl"));
        Directory.CreateDirectory(Path.Combine(_root, "src", "tb"));
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

    private static JsonElement Query(LspHandler handler, object parameters)
    {
        using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(parameters));
        handler.TryHandleRequest("rtl/hierarchy", doc.RootElement, default, out object? result);
        // re-serialize so the test does not depend on internal DTO types
        string json = JsonSerializer.Serialize(result);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static string[] Labels(JsonElement result, string kind)
    {
        List<string> labels = new();
        foreach (JsonElement node in result.GetProperty("nodes").EnumerateArray())
        {
            if (node.GetProperty("kind").GetString() == kind)
            {
                labels.Add(node.GetProperty("label").GetString() ?? string.Empty);
            }
        }
        return labels.ToArray();
    }

    [Fact]
    public void TopLevel_ListsWholeWorkspaceWithoutOpeningAnyDocument()
    {
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; endmodule\n");
        File.WriteAllText(Path.Combine(_root, "src", "rtl", "leaf.sv"), "module leaf; endmodule\n");

        LspHandler handler = CreateInitializedHandler();
        JsonElement result = Query(handler, new { nodePath = Array.Empty<int>() });

        Assert.Equal(new[] { "src" }, Labels(result, "folder"));
        Assert.Equal(new[] { "top.sv" }, Labels(result, "file"));
    }

    [Fact]
    public void Folder_ExpandsToItsSubFoldersAndFiles()
    {
        File.WriteAllText(Path.Combine(_root, "src", "rtl", "leaf.sv"), "module leaf; endmodule\n");
        File.WriteAllText(Path.Combine(_root, "src", "tb", "tb_top.sv"), "module tb_top; endmodule\n");

        LspHandler handler = CreateInitializedHandler();
        Assert.Equal(new[] { "rtl", "tb" }, Labels(Query(handler, new { folderPath = "src" }), "folder"));
        // a nested folder must not re-advertise an ancestor folder
        Assert.Empty(Labels(Query(handler, new { folderPath = "src/rtl" }), "folder"));
        Assert.Equal(new[] { "leaf.sv" }, Labels(Query(handler, new { folderPath = "src/rtl" }), "file"));
    }

    [Fact]
    public void BuildOutputDirectories_AreNotListed()
    {
        Directory.CreateDirectory(Path.Combine(_root, "build", "obj"));
        File.WriteAllText(Path.Combine(_root, "build", "obj", "generated.sv"), "module generated; endmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; endmodule\n");

        LspHandler handler = CreateInitializedHandler();
        JsonElement result = Query(handler, new { nodePath = Array.Empty<int>() });

        Assert.Empty(Labels(result, "folder"));
        Assert.Equal(new[] { "top.sv" }, Labels(result, "file"));
    }

    [Fact]
    public void InstanceTree_IsBuiltForFilesThatWereNeverOpened()
    {
        File.WriteAllText(Path.Combine(_root, "src", "rtl", "mid.sv"), "module mid; leaf u_leaf(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "src", "rtl", "leaf.sv"), "module leaf; endmodule\n");

        LspHandler handler = CreateInitializedHandler();
        string uri = new Uri(Path.Combine(_root, "src", "rtl", "mid.sv")).AbsoluteUri;
        JsonElement result = Query(handler, new { uri, nodePath = Array.Empty<int>() });

        Assert.Equal(new[] { "u_leaf" }, Labels(result, "instance"));
    }

    [Fact]
    public void Rescan_PicksUpFilesCreatedAfterInitialize()
    {
        LspHandler handler = CreateInitializedHandler();
        Assert.Empty(Labels(Query(handler, new { nodePath = Array.Empty<int>() }), "file"));

        File.WriteAllText(Path.Combine(_root, "added_later.sv"), "module added_later; endmodule\n");

        // the extension sends rescan on refresh (and at the top level)
        JsonElement result = Query(handler, new { nodePath = Array.Empty<int>(), rescan = true });
        Assert.Equal(new[] { "added_later.sv" }, Labels(result, "file"));
    }

    [Fact]
    public void DidClose_KeepsTheFileInTheWorkspaceTree()
    {
        string path = Path.Combine(_root, "kept.sv");
        File.WriteAllText(path, "module kept; endmodule\n");
        string uri = new Uri(path).AbsoluteUri;

        LspHandler handler = CreateInitializedHandler();
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri, languageId = "systemverilog", version = 1, text = "module kept; endmodule\n" },
        });
        SendNotification(handler, "textDocument/didClose", new
        {
            textDocument = new { uri },
        });

        // closing a document must not shrink the tree back to "open files only"
        JsonElement result = Query(handler, new { nodePath = Array.Empty<int>() });
        Assert.Equal(new[] { "kept.sv" }, Labels(result, "file"));
    }

    [Fact]
    public void DidClose_RemovesFileDeletedOnDisk()
    {
        string path = Path.Combine(_root, "gone.sv");
        File.WriteAllText(path, "module gone; endmodule\n");
        string uri = new Uri(path).AbsoluteUri;

        LspHandler handler = CreateInitializedHandler();
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri, languageId = "systemverilog", version = 1, text = "module gone; endmodule\n" },
        });

        File.Delete(path);
        SendNotification(handler, "textDocument/didClose", new
        {
            textDocument = new { uri },
        });

        JsonElement result = Query(handler, new { nodePath = Array.Empty<int>() });
        Assert.Empty(Labels(result, "file"));
    }

    [Fact]
    public void Rescan_DropsFilesDeletedFromDisk()
    {
        string path = Path.Combine(_root, "removed.sv");
        File.WriteAllText(path, "module removed; endmodule\n");

        LspHandler handler = CreateInitializedHandler();
        Assert.Equal(new[] { "removed.sv" }, Labels(Query(handler, new { nodePath = Array.Empty<int>() }), "file"));

        File.Delete(path);
        JsonElement result = Query(handler, new { nodePath = Array.Empty<int>(), rescan = true });
        Assert.Empty(Labels(result, "file"));
    }

    private static void SendNotification(LspHandler handler, string method, object parameters)
    {
        using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(parameters));
        handler.TryHandleNotification(method, doc.RootElement, default);
    }
}
