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

        // the tree mirrors the project layout (NavigateView), so the ordinary
        // "build" folder is listed - but the build output directory inside it
        // ("obj") is not
        Assert.Equal(new[] { "build", "src" }, Labels(result, "folder"));
        Assert.Equal(new[] { "top.sv" }, Labels(result, "file"));

        JsonElement build = Query(handler, new { folderPath = "build" });
        Assert.Empty(Labels(build, "folder"));
        Assert.Empty(Labels(build, "file"));
    }

    /// <summary>
    /// The tree mirrors the project layout like RtlEditor2's NavigateView: every
    /// file and folder is listed, not only the SystemVerilog sources.
    /// </summary>
    [Fact]
    public void TopLevel_ListsEveryFileAndFolder_NotOnlySystemVerilog()
    {
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; endmodule\n");
        File.WriteAllText(Path.Combine(_root, "README.md"), "# readme\n");
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "notes\n");
        Directory.CreateDirectory(Path.Combine(_root, "docs"));

        LspHandler handler = CreateInitializedHandler();
        JsonElement result = Query(handler, new { nodePath = Array.Empty<int>() });

        Assert.Equal(new[] { "docs", "src" }, Labels(result, "folder"));
        Assert.Equal(new[] { "notes.txt", "README.md", "top.sv" }, Labels(result, "file"));
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

    /// <summary>
    /// A client that opens a single file without opening a folder sends no
    /// rootUri / workspaceFolders. The hierarchy must still cover the project
    /// the file belongs to, otherwise the tree only ever shows the opened
    /// document.
    /// </summary>
    [Fact]
    public void TopLevel_InfersWorkspaceRootFromOpenedFile_WhenNoFolderWasOpened()
    {
        // a project marker so the root is recognised above the file's folder
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; endmodule\n");
        File.WriteAllText(Path.Combine(_root, "src", "rtl", "leaf.sv"), "module leaf; endmodule\n");

        LspHandler handler = new(new InMemorySystemVerilogCore());
        // initialize without any workspace root, like "File > Open File"
        using (JsonDocument doc = JsonDocument.Parse("{\"processId\":1,\"capabilities\":{}}"))
        {
            handler.TryHandleRequest("initialize", doc.RootElement, default, out _);
        }

        string uri = new Uri(Path.Combine(_root, "src", "rtl", "leaf.sv")).AbsoluteUri;
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri, languageId = "systemverilog", version = 1, text = "module leaf; endmodule\n" },
        });

        JsonElement result = Query(handler, new { nodePath = Array.Empty<int>() });

        // the whole project is listed, not just the opened file
        Assert.Equal(new[] { "src" }, Labels(result, "folder"));
        Assert.Equal(new[] { "top.sv" }, Labels(result, "file"));
    }

    /// <summary>
    /// Inferring the workspace root from the opened file must not parse the
    /// whole project synchronously: the hierarchy request has to answer quickly
    /// even for a large tree, otherwise the view renders empty while the scan
    /// runs. Files are parsed lazily when a node is expanded instead.
    /// </summary>
    [Fact]
    public void TopLevel_IsFast_EvenWithManySourceFiles()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        // a few hundred sources would take a long time to parse eagerly
        for (int i = 0; i < 300; i++)
        {
            File.WriteAllText(
                Path.Combine(_root, "src", "rtl", $"m{i}.sv"),
                $"module m{i}; leaf u_leaf(); endmodule\n");
        }

        LspHandler handler = new(new InMemorySystemVerilogCore());
        using (JsonDocument doc = JsonDocument.Parse("{\"processId\":1,\"capabilities\":{}}"))
        {
            handler.TryHandleRequest("initialize", doc.RootElement, default, out _);
        }

        string uri = new Uri(Path.Combine(_root, "src", "rtl", "m0.sv")).AbsoluteUri;
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri, languageId = "systemverilog", version = 1, text = "module m0; endmodule\n" },
        });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        JsonElement result = Query(handler, new { nodePath = Array.Empty<int>() });
        stopwatch.Stop();

        Assert.Equal(new[] { "src" }, Labels(result, "folder"));
        // generous bound: an eager scan+parse of 300 files takes several seconds
        Assert.True(stopwatch.ElapsedMilliseconds < 3000,
            $"top level took {stopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// Expanding a file that was never opened must load and parse it on demand
    /// and return its instance hierarchy.
    /// </summary>
    [Fact]
    public void ExpandingFile_LoadsAndParsesItLazily()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        string path = Path.Combine(_root, "src", "rtl", "mid.sv");
        File.WriteAllText(path, "module mid; leaf u_leaf(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "src", "rtl", "leaf.sv"), "module leaf; endmodule\n");

        LspHandler handler = CreateInitializedHandler();
        JsonElement folders = Query(handler, new { nodePath = Array.Empty<int>() });
        Assert.Equal(new[] { "src" }, Labels(folders, "folder"));

        // walk down to the file and expand it
        JsonElement rtl = Query(handler, new { folderPath = "src/rtl" });
        JsonElement mid = default;
        foreach (JsonElement node in rtl.GetProperty("nodes").EnumerateArray())
        {
            if (node.GetProperty("label").GetString() == "mid.sv") mid = node.Clone();
        }
        Assert.Equal("mid.sv", mid.GetProperty("label").GetString());
        Assert.True(mid.GetProperty("hasChildren").GetBoolean());

        JsonElement instances = Query(handler, new { uri = mid.GetProperty("uri").GetString(), nodePath = Array.Empty<int>() });
        Assert.Equal(new[] { "u_leaf" }, Labels(instances, "instance"));
    }

    /// <summary>
    /// Adding a folder to the window must extend the tree to that folder.
    /// </summary>
    [Fact]
    public void DidChangeWorkspaceFolders_AddsTheNewRootToTheTree()
    {
        LspHandler handler = CreateInitializedHandler();
        Assert.Empty(Labels(Query(handler, new { nodePath = Array.Empty<int>() }), "file"));

        string extra = Path.Combine(_root, "extra");
        Directory.CreateDirectory(extra);
        File.WriteAllText(Path.Combine(extra, "added.sv"), "module added; endmodule\n");

        SendNotification(handler, "workspace/didChangeWorkspaceFolders", new
        {
            @event = new
            {
                added = new[] { new { uri = new Uri(extra).AbsoluteUri, name = "extra" } },
                removed = Array.Empty<object>(),
            },
        });

        JsonElement result = Query(handler, new { nodePath = Array.Empty<int>() });
        // two roots are open now, so each root is listed as a folder node
        string[] folders = Labels(result, "folder");
        Assert.Contains("extra", folders);
        Assert.Contains(Path.GetFileName(_root), folders);
    }

    private static void SendNotification(LspHandler handler, string method, object parameters)
    {
        using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(parameters));
        handler.TryHandleNotification(method, doc.RootElement, default);
    }
}
