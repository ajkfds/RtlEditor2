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

    /// <summary>
    /// VS Code reports the workspace folder with a lower-case, percent-encoded
    /// drive letter (file:///d%3A/Document/x). Uri.LocalPath kept that as
    /// "/d:/Document/x", which does not exist on Windows: the scan found 0
    /// files and the RTL Hierarchy tree was always empty. This is the exact
    /// form the real client sends, so it must list the workspace.
    /// </summary>
    [Fact]
    public void LowerCasePercentEncodedDriveLetter_ListsTheWholeWorkspace()
    {
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; endmodule\n");

        // Emulate the exact URI VS Code sends: "file:///" + path where the
        // drive letter is lower-case and its colon is percent-encoded
        // (file:///c%3A/Users/...). Uri.LocalPath turns that into
        // "/c:/Users/..." which does not exist on Windows.
        string forward = _root.Replace('\\', '/');
        string vscodeUri = "file:///" + forward;
        if (forward.Length >= 2 && forward[1] == ':')
        {
            vscodeUri = "file:///"
                + char.ToLowerInvariant(forward[0])
                + "%3A"
                + forward.Substring(2);
        }
        // sanity: on Windows this must really carry the escaped colon
        if (OperatingSystem.IsWindows())
        {
            Assert.DoesNotContain("file:///C:", vscodeUri, StringComparison.OrdinalIgnoreCase);
        }

        LspHandler handler = new(new InMemorySystemVerilogCore());
        string initialize = JsonSerializer.Serialize(new
        {
            processId = 1,
            rootUri = vscodeUri,
            workspaceFolders = new[] { new { uri = vscodeUri, name = "mySample" } },
            capabilities = new { },
        });
        using (JsonDocument doc = JsonDocument.Parse(initialize))
        {
            handler.TryHandleRequest("initialize", doc.RootElement, default, out _);
        }

        JsonElement result = Query(handler, new { nodePath = Array.Empty<int>() });
        Assert.Equal(new[] { "top.sv" }, Labels(result, "file"));
    }

    /// <summary>
    /// Drives <c>rtl/hierarchy</c>. The tests describe the node they want with
    /// the old, readable parameters (<c>folderPath</c> / <c>uri</c> +
    /// <c>nodePath</c>); this helper translates them into the opaque
    /// <c>expandToken</c> the real client would echo back, so the tests keep
    /// reading naturally while exercising the real protocol.
    /// </summary>
    private static JsonElement Query(LspHandler handler, object parameters)
    {
        string json = JsonSerializer.Serialize(parameters);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string? expandToken = null;
        if (root.TryGetProperty("folderPath", out JsonElement folder)
            && folder.ValueKind == JsonValueKind.String)
        {
            expandToken = ExpandToken.ForFolder(folder.GetString() ?? string.Empty);
        }
        else if (root.TryGetProperty("uri", out JsonElement uri)
            && uri.ValueKind == JsonValueKind.String)
        {
            string fileUri = uri.GetString() ?? string.Empty;
            List<string> segments = new();
            if (root.TryGetProperty("nodePath", out JsonElement path)
                && path.ValueKind == JsonValueKind.Array)
            {
                // the old protocol addressed children by index; resolve them to
                // names by walking the tree the same way the client would
                JsonElement current = Query(handler, new { uri = fileUri });
                foreach (JsonElement indexElement in path.EnumerateArray())
                {
                    int index = indexElement.GetInt32();
                    JsonElement nodes = current.GetProperty("nodes");
                    if (index < 0 || index >= nodes.GetArrayLength()) break;
                    JsonElement node = nodes[index];
                    segments.Add(node.GetProperty("label").GetString() ?? string.Empty);
                    if (!node.TryGetProperty("expandToken", out JsonElement token)
                        || token.ValueKind != JsonValueKind.String)
                    {
                        break;
                    }
                    current = Query(handler, new { expandToken = token.GetString() });
                }
            }
            expandToken = segments.Count == 0
                ? ExpandToken.ForFile(fileUri)
                : ExpandToken.ForInstance(fileUri, segments);
        }

        bool rescan = root.TryGetProperty("rescan", out JsonElement r) && r.ValueKind == JsonValueKind.True;
        Dictionary<string, object> request = new();
        if (expandToken != null) request["expandToken"] = expandToken;
        if (rescan) request["rescan"] = true;

        using JsonDocument requestDoc = JsonDocument.Parse(JsonSerializer.Serialize(request));
        handler.TryHandleRequest("rtl/hierarchy", requestDoc.RootElement, default, out object? result);
        // re-serialize so the test does not depend on internal DTO types
        string resultJson = JsonSerializer.Serialize(result);
        return JsonDocument.Parse(resultJson).RootElement.Clone();
    }

    /// <summary>Top level of the tree (no expand token = the workspace root).</summary>
    private static JsonElement QueryTop(LspHandler handler) => Query(handler, new { });

    /// <summary>
    /// Expands the node with the given label in <paramref name="parent"/> by
    /// echoing back the opaque expand token the server issued for it. This is
    /// exactly what the client does, so the tests exercise the real protocol.
    /// </summary>
    private static JsonElement QueryChild(LspHandler handler, JsonElement parent, string label)
    {
        foreach (JsonElement node in parent.GetProperty("nodes").EnumerateArray())
        {
            if (node.GetProperty("label").GetString() != label) continue;
            if (!node.TryGetProperty("expandToken", out JsonElement token)
                || token.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException($"node '{label}' has no expandToken");
            }
            return Query(handler, new { expandToken = token.GetString() });
        }
        throw new InvalidOperationException($"node '{label}' not found");
    }

    /// <summary>
    /// A client reports the same folder through both rootUri and
    /// workspaceFolders. The root must be registered once, otherwise every root
    /// is listed once per duplicate report.
    /// </summary>
    [Fact]
    public void DuplicateRootReports_ListTheRootOnce()
    {
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; endmodule\n");

        LspHandler handler = new(new InMemorySystemVerilogCore());
        string rootUri = new Uri(_root).AbsoluteUri;
        string initialize = JsonSerializer.Serialize(new
        {
            processId = 1,
            rootUri,
            workspaceFolders = new[] { new { uri = rootUri, name = "root" } },
            capabilities = new { },
        });
        using (JsonDocument doc = JsonDocument.Parse(initialize))
        {
            handler.TryHandleRequest("initialize", doc.RootElement, default, out _);
        }

        JsonElement result = Query(handler, new { nodePath = Array.Empty<int>() });
        // single root: its contents are listed directly, not a root node
        Assert.Equal(new[] { "top.sv" }, Labels(result, "file"));
        Assert.Equal(new[] { "src" }, Labels(result, "folder"));
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

    /// <summary>
    /// The tree is produced by the editor's own NavigatePanel update code
    /// (VerilogCommon.Updater), so it must expose the same nested instance
    /// hierarchy the editor shows: expanding a node returns the instances
    /// inside the instantiated module, not just the first level.
    /// </summary>
    [Fact]
    public void InstanceTree_IsNestedLikeTheEditorNavigatePanel()
    {
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top; mid u_mid(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "mid.sv"),
            "module mid; leaf u_leaf(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "leaf.sv"),
            "module leaf; endmodule\n");

        LspHandler handler = CreateInitializedHandler();
        string uri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        // first level: u_mid
        JsonElement level0 = Query(handler, new { uri, nodePath = Array.Empty<int>() });
        Assert.Equal(new[] { "u_mid" }, Labels(level0, "instance"));
        Assert.True(level0.GetProperty("nodes")[0].GetProperty("hasChildren").GetBoolean());

        // second level: u_leaf inside u_mid
        JsonElement level1 = Query(handler, new { uri, nodePath = new[] { 0 } });
        Assert.Equal(new[] { "u_leaf" }, Labels(level1, "instance"));

        // third level: leaf has no instances
        JsonElement level2 = Query(handler, new { uri, nodePath = new[] { 0, 0 } });
        Assert.Empty(Labels(level2, "instance"));
    }

    /// <summary>
    /// A file that declares several building blocks lists one entry per
    /// building block (the editor's addSubItemsMultiBuildingBlock rule), each
    /// expandable into the instances inside that block.
    /// </summary>
    [Fact]
    public void MultiBuildingBlockFile_ListsOneEntryPerBuildingBlock()
    {
        File.WriteAllText(Path.Combine(_root, "multi.sv"),
            "module a; leaf u_leaf_a(); endmodule\n" +
            "module b; leaf u_leaf_b(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "leaf.sv"), "module leaf; endmodule\n");

        LspHandler handler = CreateInitializedHandler();
        string uri = new Uri(Path.Combine(_root, "multi.sv")).AbsoluteUri;

        JsonElement level0 = Query(handler, new { uri, nodePath = Array.Empty<int>() });
        // the editor's Updater adds building blocks in dictionary order, which
        // is not source order, so compare as a set
        string[] blocks = Labels(level0, "instance");
        Assert.Equal(new[] { "a", "b" }, blocks.OrderBy(x => x, StringComparer.Ordinal).ToArray());

        // resolve the index of "a" so the assertion does not depend on the
        // Updater's dictionary order
        int indexOfA = Array.IndexOf(blocks, "a");
        JsonElement level1 = Query(handler, new { uri, nodePath = new[] { indexOfA } });
        Assert.Equal(new[] { "u_leaf_a" }, Labels(level1, "instance"));
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
    /// A large workspace (> MaxEagerParseFiles) is parsed in the background at
    /// initialize, so a dependency may still be unparsed when the user expands a
    /// node. The expand must force the remaining files to be built; otherwise
    /// the Updater silently drops the unresolved instantiation and the level is
    /// empty (and only fills in after a second expand).
    /// </summary>
    [Fact]
    public void ExpandBeforeBackgroundParseFinishes_ShowsInstances()
    {
        // push the workspace over the eager-parse threshold so initialize
        // parses in the background and the first expand races it
        for (int i = 0; i < 250; i++)
        {
            File.WriteAllText(Path.Combine(_root, "src", "rtl", $"m{i}.sv"), $"module m{i}; endmodule\n");
        }
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; mid u_mid(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "mid.sv"), "module mid; leaf u_leaf(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "leaf.sv"), "module leaf; endmodule\n");
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        LspHandler handler = CreateInitializedHandler();

        // expand IMMEDIATELY, while the background parse may still be running
        JsonElement level0 = Query(handler, new { uri = topUri, nodePath = Array.Empty<int>() });
        Assert.Equal(new[] { "u_mid" }, Labels(level0, "instance"));
        Assert.True(level0.GetProperty("nodes")[0].GetProperty("hasChildren").GetBoolean());

        Assert.Equal(new[] { "u_leaf" }, Labels(Query(handler, new { uri = topUri, nodePath = new[] { 0 } }), "instance"));
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
    /// An interface may instantiate modules (e.g. "interface bus_if; leaf
    /// u_leaf(); endinterface"). The interface item dispatcher used to reject the
    /// identifier as an "illegal interface item", so a module instance inside an
    /// interface never appeared in the hierarchy. Expanding an interface instance
    /// must now show the modules it instantiates.
    /// </summary>
    [Fact]
    public void InterfaceInstance_ShowsInstancesInsideTheInterface()
    {
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; bus_if u_bus(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "bus_if.sv"), "interface bus_if; leaf u_leaf(); endinterface\n");
        File.WriteAllText(Path.Combine(_root, "leaf.sv"), "module leaf; endmodule\n");
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        LspHandler handler = CreateInitializedHandler();
        JsonElement level0 = Query(handler, new { uri = topUri, nodePath = Array.Empty<int>() });
        Assert.Equal(new[] { "u_bus" }, Labels(level0, "instance"));
        Assert.True(level0.GetProperty("nodes")[0].GetProperty("hasChildren").GetBoolean());

        JsonElement level1 = Query(handler, new { uri = topUri, nodePath = new[] { 0 } });
        Assert.Equal(new[] { "u_leaf" }, Labels(level1, "instance"));
    }

    /// <summary>
    /// Opening a file (didOpen) and expanding its node in the RTL Hierarchy must
    /// operate on the SAME entry. VS Code sends the drive-letter colon escaped
    /// and lower-case (file:///c%3A/...) while the workspace scan registers
    /// file:///C:/...; without URI normalisation the same file was two entries,
    /// so an opened (and edited) file's re-parse never reached the tree.
    /// </summary>
    [Fact]
    public void DidOpenAndScan_ShareOneFileEntry()
    {
        string path = Path.Combine(_root, "top.sv");
        File.WriteAllText(path, "module top; leaf u_a(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "leaf.sv"), "module leaf; endmodule\n");

        // exactly the URI VS Code sends in didOpen
        string forward = path.Replace('\\', '/');
        string vscodeUri = "file:///" + char.ToLowerInvariant(forward[0]) + "%3A" + forward.Substring(2);

        LspHandler handler = CreateInitializedHandler();
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = vscodeUri, languageId = "systemverilog", version = 1, text = "module top; leaf u_b(); endmodule\n" },
        });

        // the scan-form uri must resolve to the same (edited) entry
        string scannedUri = new Uri(path).AbsoluteUri;
        JsonElement level0 = Query(handler, new { uri = scannedUri, nodePath = Array.Empty<int>() });
        Assert.Equal(new[] { "u_b" }, Labels(level0, "instance"));
    }

    /// <summary>
    /// Semantic tokens (syntax highlighting) must be returned for the escaped
    /// URI VS Code sends. Before URI normalisation the request looked up a
    /// different entry than didOpen registered, so the highlight data was empty.
    /// </summary>
    [Fact]
    public void SemanticTokens_ReturnedForEscapedUri()
    {
        string path = Path.Combine(_root, "top.sv");
        File.WriteAllText(path, "module top;\n  logic [7:0] data;\nendmodule\n");
        string forward = path.Replace('\\', '/');
        string vscodeUri = "file:///" + char.ToLowerInvariant(forward[0]) + "%3A" + forward.Substring(2);

        LspHandler handler = CreateInitializedHandler();
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = vscodeUri, languageId = "systemverilog", version = 1, text = File.ReadAllText(path) },
        });

        using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(new { textDocument = new { uri = vscodeUri } }));
        handler.TryHandleRequest("textDocument/semanticTokens/full", doc.RootElement, default, out object? result);
        JsonElement tokens = JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement.Clone();
        // 5 integers per token; at least the module / logic / data tokens
        Assert.True(tokens.GetProperty("data").GetArrayLength() >= 5);
    }

    /// <summary>
    /// A node must only advertise an expand arrow (hasChildren) when expanding
    /// it actually shows children. An instance of an undefined module has no
    /// instantiable children (the editor's Updater skips the unresolved
    /// module), so it must not be expandable - otherwise the tree shows a
    /// collapse arrow that opens to an empty list.
    /// </summary>
    [Fact]
    public void InstanceOfUndefinedModule_IsNotExpandable()
    {
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; mid u_mid(); endmodule\n");
        // mid instantiates "missing", which is declared nowhere
        File.WriteAllText(Path.Combine(_root, "mid.sv"), "module mid; missing u_x(); endmodule\n");
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        LspHandler handler = CreateInitializedHandler();
        JsonElement level0 = Query(handler, new { uri = topUri, nodePath = Array.Empty<int>() });
        Assert.Equal(new[] { "u_mid" }, Labels(level0, "instance"));
        Assert.False(level0.GetProperty("nodes")[0].GetProperty("hasChildren").GetBoolean());

        // and the expansion really is empty
        Assert.Empty(Labels(Query(handler, new { uri = topUri, nodePath = new[] { 0 } }), "instance"));
    }

    /// <summary>
    /// The expand arrow and the expansion must agree: a node that reports
    /// hasChildren expands to a non-empty list.
    /// </summary>
    [Fact]
    public void HasChildren_MatchesTheExpansion()
    {
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; mid u_mid(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "mid.sv"), "module mid; leaf u_leaf(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "leaf.sv"), "module leaf; endmodule\n");
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        LspHandler handler = CreateInitializedHandler();
        JsonElement level0 = Query(handler, new { uri = topUri, nodePath = Array.Empty<int>() });
        Assert.True(level0.GetProperty("nodes")[0].GetProperty("hasChildren").GetBoolean());
        Assert.Equal(new[] { "u_leaf" }, Labels(Query(handler, new { uri = topUri, nodePath = new[] { 0 } }), "instance"));
    }

    /// <summary>
    /// An instance array of a module that declares its own instances must be
    /// expandable (each element expands to the module's instances).
    /// </summary>
    [Fact]
    public void InstanceArrayOfModuleWithChildren_IsExpandable()
    {
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; mid u_a[1:0](); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "mid.sv"), "module mid; leaf u_leaf(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "leaf.sv"), "module leaf; endmodule\n");
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        LspHandler handler = CreateInitializedHandler();
        JsonElement level0 = Query(handler, new { uri = topUri, nodePath = Array.Empty<int>() });
        Assert.Equal(new[] { "u_a[0]", "u_a[1]" }, Labels(level0, "instance"));
        Assert.True(level0.GetProperty("nodes")[0].GetProperty("hasChildren").GetBoolean());
        Assert.Equal(new[] { "u_leaf" }, Labels(Query(handler, new { uri = topUri, nodePath = new[] { 0 } }), "instance"));
    }

    /// <summary>
    /// Adding a folder to the window must extend the tree to that folder.
    /// </summary>
    [Fact]
    public void Hierarchy_UpdatesAfterDidChange()
    {
        // didChange is the LSP equivalent of "parse hierarchy ran": the edited
        // file is re-parsed, and expanding a file whose instance list changed
        // must show the new instances (not the previous parse).
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; leaf u_a(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "leaf.sv"), "module leaf; endmodule\n");
        string uri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        LspHandler handler = CreateInitializedHandler();
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri, languageId = "systemverilog", version = 1, text = "module top; leaf u_a(); endmodule\n" },
        });
        Assert.Equal(new[] { "u_a" }, Labels(Query(handler, new { uri, nodePath = Array.Empty<int>() }), "instance"));

        SendNotification(handler, "textDocument/didChange", new
        {
            textDocument = new { uri, version = 2 },
            contentChanges = new[] { new { text = "module top; leaf u_b(); leaf u_c(); endmodule\n" } },
        });
        Assert.Equal(new[] { "u_b", "u_c" }, Labels(Query(handler, new { uri, nodePath = Array.Empty<int>() }), "instance"));
    }

    /// <summary>
    /// Editing a DEPENDENCY module must refresh the instance tree under every
    /// parent that expands it. The instance caches the parse result of its
    /// source file, so without invalidation a parent would keep showing the
    /// previous instantiations after the dependency was edited.
    /// </summary>
    [Fact]
    public void Hierarchy_RefreshesNestedInstancesAfterDependencyEdit()
    {
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; mid u_mid(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "mid.sv"), "module mid; leaf u_old(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "leaf.sv"), "module leaf; endmodule\n");
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;
        string midUri = new Uri(Path.Combine(_root, "mid.sv")).AbsoluteUri;

        LspHandler handler = CreateInitializedHandler();
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = midUri, languageId = "systemverilog", version = 1, text = "module mid; leaf u_old(); endmodule\n" },
        });

        Assert.Equal(new[] { "u_mid" }, Labels(Query(handler, new { uri = topUri, nodePath = Array.Empty<int>() }), "instance"));
        Assert.Equal(new[] { "u_old" }, Labels(Query(handler, new { uri = topUri, nodePath = new[] { 0 } }), "instance"));

        SendNotification(handler, "textDocument/didChange", new
        {
            textDocument = new { uri = midUri, version = 2 },
            contentChanges = new[] { new { text = "module mid; leaf u_new(); endmodule\n" } },
        });

        Assert.Equal(new[] { "u_new" }, Labels(Query(handler, new { uri = topUri, nodePath = new[] { 0 } }), "instance"));
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
