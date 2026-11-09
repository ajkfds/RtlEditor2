using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SystemVerilogCore.Diagnostics;
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

    private LspHandler CreateInitializedHandler() => CreateInitializedHandler(new InMemorySystemVerilogCore());

    /// <summary>
    /// Same as <see cref="CreateInitializedHandler()"/> but keeps the caller's
    /// core, so a test can inspect the parse results (diagnostics) the handler
    /// produced.
    /// </summary>
    private LspHandler CreateInitializedHandler(InMemorySystemVerilogCore core)
    {
        LspHandler handler = new(core);
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
    /// Sends <c>rtl/hierarchy</c> with the parameters verbatim, without the
    /// readable-parameter translation <see cref="Query"/> performs.
    ///
    /// <para>
    /// <see cref="Query"/> builds the expand token itself from
    /// <c>folderPath</c> / <c>uri</c> + <c>nodePath</c>, so it can never
    /// exercise a token the SERVER emitted. Tests that must follow the real
    /// client (store the token, echo it back) use this instead.
    /// </para>
    /// </summary>
    private static JsonElement QueryRaw(LspHandler handler, object parameters)
    {
        using JsonDocument requestDoc = JsonDocument.Parse(JsonSerializer.Serialize(parameters));
        handler.TryHandleRequest("rtl/hierarchy", requestDoc.RootElement, default, out object? result);
        string resultJson = JsonSerializer.Serialize(result);
        return JsonDocument.Parse(resultJson).RootElement.Clone();
    }

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
    /// The expand token the server issues for an INSTANCE node must resolve back
    /// to that instance.
    ///
    /// <para>
    /// The client never builds a token itself: it stores the one the server sent
    /// and echoes it back verbatim. So the token has to name the file the
    /// instance LIVES IN (the file being expanded) together with the instance
    /// path inside it. Naming the instantiated module's own source file instead
    /// makes the next request look for the instance inside the wrong file: the
    /// expand arrow is drawn but the level comes back empty.
    /// </para>
    ///
    /// <para>
    /// The node's <c>uri</c> (the click target) is a different thing: it is the
    /// instantiated module's own source, matching the editor's NavigatePanel.
    /// Both are asserted here so the two cannot drift back together.
    /// </para>
    ///
    /// <para>
    /// The other hierarchy tests build their tokens by hand from the file they
    /// queried, which is why they never caught this: only the server-emitted
    /// token is exercised here.
    /// </para>
    /// </summary>
    [Fact]
    public void InstanceNode_ExpandToken_ExpandsToItsChildren()
    {
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; mid u_mid(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "mid.sv"), "module mid; leaf u_leaf(); endmodule\n");
        File.WriteAllText(Path.Combine(_root, "leaf.sv"), "module leaf; endmodule\n");

        LspHandler handler = CreateInitializedHandler();

        // top level -> the top.sv file node
        JsonElement fileNode = FindNode(QueryTop(handler), "top.sv");
        string fileToken = fileNode.GetProperty("expandToken").GetString() ?? "";
        Assert.False(string.IsNullOrEmpty(fileToken));

        // expand the file: u_mid
        JsonElement level0 = QueryRaw(handler, new { expandToken = fileToken });
        JsonElement mid = FindNode(level0, "u_mid");
        Assert.True(mid.GetProperty("hasChildren").GetBoolean());

        // Clicking the instance node opens the INSTANTIATED module's own source
        // (mid.sv), exactly like the editor's NavigatePanel: selecting a module
        // instance shows the instantiated module, not the file the
        // instantiation is written in. The expand token, by contrast, still
        // addresses the instance inside top.sv (checked below).
        Assert.EndsWith("mid.sv", mid.GetProperty("uri").GetString() ?? "");

        // expand u_mid with the token the SERVER issued (exactly what the client
        // does: it never builds a token itself)
        string instanceToken = mid.GetProperty("expandToken").GetString() ?? "";
        Assert.False(string.IsNullOrEmpty(instanceToken), "instance node must carry an expand token");
        JsonElement level1 = QueryRaw(handler, new { expandToken = instanceToken });
        Assert.Equal(new[] { "u_leaf" }, Labels(level1, "instance"));
    }

    /// <summary>Finds the node with the given label in a hierarchy response.</summary>
    private static JsonElement FindNode(JsonElement result, string label)
    {
        foreach (JsonElement node in result.GetProperty("nodes").EnumerateArray())
        {
            if (node.GetProperty("label").GetString() == label) return node;
        }
        throw new InvalidOperationException($"node '{label}' not found");
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
    /// The semantic-token stream must be a valid LSP stream: strictly
    /// increasing, no duplicate start offsets and no overlaps.
    /// </summary>
    /// <remarks>
    /// The real parser colours the same span more than once. A module instance
    /// port connection such as <c>.AA ( aaaa )</c> gets both the generic
    /// reference colour and the data-object colour for <c>AA</c> / <c>aaaa</c>
    /// at the exact same offset. VS Code then logs
    /// <c>Overlapping semantic tokens detected at lineNumber N, column M</c> and
    /// drops the token, so the span silently loses its colour ("the code below
    /// the module instance is not coloured"). Because the duplicates only occur
    /// for declared objects (which the editor also draws with the concrete type),
    /// folding them keeps the most specific type.
    /// </remarks>
    /// <summary>
    /// Every line of a file header must be coloured: a run of <c>///</c>
    /// comments and the compiler directives after it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The plugin's <c>ParseEngine.ParseOnceAsync</c> copied the text into the
    /// parser's document a second time before parsing. That copy re-raised
    /// <c>TextDocument.TextChanged</c>, and the resulting edit bookkeeping threw
    /// away the colour entries of the lines a comment token spans. A file that
    /// starts with a multi-line header therefore kept only its FIRST line
    /// coloured; the rest of the header and the <c>`include</c> lines below it
    /// came back with no tokens at all (reported on
    /// <c>verilog_sample/scr1/src/top/scr1_top_axi.sv</c>, lines 2-6).
    /// </para>
    /// <para>
    /// The parse itself was correct throughout (same building blocks, same
    /// diagnostics), so only a colour check catches this.
    /// </para>
    /// </remarks>
    [Fact]
    public void SemanticTokens_ColourEveryHeaderLine()
    {
        string text =
            "/// Copyright notice\n" +
            "/// @file       <top.sv>\n" +
            "/// @brief      header\n" +
            "///\n" +
            "\n" +
            "`include \"defs.svh\"\n" +
            "\n" +
            "module top;\n" +
            "endmodule\n";
        File.WriteAllText(Path.Combine(_root, "defs.svh"), "// header\n");
        string path = Path.Combine(_root, "top.sv");
        File.WriteAllText(path, text);
        string uri = new Uri(path).AbsoluteUri;

        LspHandler handler = CreateInitializedHandler();
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri, languageId = "systemverilog", version = 1, text },
        });

        using JsonDocument request = JsonDocument.Parse(JsonSerializer.Serialize(new { textDocument = new { uri } }));
        handler.TryHandleRequest("textDocument/semanticTokens/full", request.RootElement, default, out object? result);
        JsonElement data = JsonDocument.Parse(JsonSerializer.Serialize(result))
            .RootElement.GetProperty("data");

        // count the tokens that start on each 0-based line
        string[] lines = text.Split('\n');
        int[] perLine = new int[lines.Length];
        int line = 0;
        for (int i = 0; i + 4 < data.GetArrayLength(); i += 5)
        {
            line += data[i].GetInt32();
            if (line < perLine.Length) perLine[line]++;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string source = lines[i].TrimEnd('\r');
            if (source.Length == 0) continue;
            Assert.True(perLine[i] > 0,
                $"line {i + 1} has no colour: '{source}' " +
                $"(tokens per line: {string.Join(",", perLine)})");
        }
    }

    [Fact]
    public void SemanticTokens_NoDuplicateOrOverlappingTokens()
    {
        // a parameterised instance with named port connections: every port
        // reference is emitted twice by the parser (reference + data object)
        string text =
            "module top;\n" +
            "  wire [2:0] aaaa;\n" +
            "  wire [4:0] bb;\n" +
            "  child #(\n" +
            "    .P_WIDTH ( 5 )\n" +
            "  ) SS (\n" +
            "    .AA      ( aaaa ),\n" +
            "    .CLK_I   ( bb ),\n" +
            "    .RST_X_I (  )\n" +
            "  );\n" +
            "endmodule\n";
        string childText =
            "module child #(parameter P_WIDTH = 8) (\n" +
            "  output [P_WIDTH-1:0] AA,\n" +
            "  input                CLK_I,\n" +
            "  input                RST_X_I\n" +
            ");\n" +
            "endmodule\n";

        string path = Path.Combine(_root, "top.sv");
        File.WriteAllText(path, text);
        File.WriteAllText(Path.Combine(_root, "child.sv"), childText);
        string forward = path.Replace('\\', '/');
        string vscodeUri = "file:///" + char.ToLowerInvariant(forward[0]) + "%3A" + forward.Substring(2);
        string childForward = Path.Combine(_root, "child.sv").Replace('\\', '/');
        string childUri = "file:///" + char.ToLowerInvariant(childForward[0]) + "%3A" + childForward.Substring(2);

        LspHandler handler = CreateInitializedHandler();
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = childUri, languageId = "systemverilog", version = 1, text = childText },
        });
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = vscodeUri, languageId = "systemverilog", version = 1, text },
        });

        using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(new { textDocument = new { uri = vscodeUri } }));
        handler.TryHandleRequest("textDocument/semanticTokens/full", doc.RootElement, default, out object? result);
        JsonElement tokens = JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement.Clone();
        JsonElement data = tokens.GetProperty("data");

        var lines = text.Split('\n');
        int count = data.GetArrayLength();
        Assert.True(count >= 5, "expected semantic tokens");

        int line = 0, character = 0;
        int previousEndLine = -1, previousEndCharacter = -1;
        for (int i = 0; i + 4 < count; i += 5)
        {
            int deltaLine = data[i].GetInt32();
            int deltaCharacter = data[i + 1].GetInt32();
            int length = data[i + 2].GetInt32();
            line += deltaLine;
            character = (deltaLine == 0) ? character + deltaCharacter : deltaCharacter;

            Assert.True(deltaLine >= 0, $"negative deltaLine at token #{i / 5}");
            // the very first token may legitimately start at 0:0
            if (i > 0)
                Assert.False(deltaLine == 0 && deltaCharacter == 0,
                    $"duplicate start offset at {line}:{character} (token #{i / 5})");
            Assert.True(length > 0, $"zero-length token at {line}:{character}");

            if (line == previousEndLine)
                Assert.True(character >= previousEndCharacter,
                    $"overlapping token at {line}:{character} (previous end {previousEndCharacter})");

            previousEndLine = line;
            previousEndCharacter = character + length;
        }

        // and the instance must not break the colouring of what follows it
        bool sawEndModule = false;
        line = 0; character = 0;
        for (int i = 0; i + 4 < count; i += 5)
        {
            int deltaLine = data[i].GetInt32();
            int deltaCharacter = data[i + 1].GetInt32();
            int length = data[i + 2].GetInt32();
            line += deltaLine;
            character = (deltaLine == 0) ? character + deltaCharacter : deltaCharacter;
            if (line < lines.Length)
            {
                string source = lines[line].TrimEnd('\r');
                if (character >= 0 && character + length <= source.Length &&
                    source.Substring(character, length) == "endmodule")
                    sawEndModule = true;
            }
        }
        Assert.True(sawEndModule, "the endmodule after the parameterised instance lost its colour");
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
    /// Clicking an instance node must parse the document AS that instance, so
    /// the parameter overrides written at the instantiation are applied.
    ///
    /// <para>
    /// The editor opens the INSTANCE, not the file: selecting a module instance
    /// calls <c>SetTextFileAsync(instance)</c> and the instance's parser is
    /// created with the instantiation's parameter overrides. A plain file parse
    /// uses the declared defaults, so a module instantiated with
    /// <c>#(.WIDTH(4))</c> would still be shown with <c>WIDTH = 8</c> and every
    /// width derived from it would be wrong.
    /// </para>
    ///
    /// <para>
    /// The test drives <c>rtl/reparse</c> with the <c>instancePathSegments</c>
    /// the client echoes back, then checks the diagnostics: with the override
    /// applied the port width matches the connected signal, without it the
    /// default width produces a mismatch.
    /// </para>
    /// </summary>
    [Fact]
    public void ReparseAsInstance_AppliesParameterOverrides()
    {
        // WIDTH defaults to 8; the instance overrides it to 4.
        File.WriteAllText(Path.Combine(_root, "child.sv"),
            "module child #(parameter WIDTH = 8) (input [WIDTH-1:0] d);\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top;\n  wire [3:0] sig;\n  child #(.WIDTH(4)) u_child(.d(sig));\nendmodule\n");
        string childUri = new Uri(Path.Combine(_root, "child.sv")).AbsoluteUri;
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);

        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new
            {
                uri = childUri,
                languageId = "systemverilog",
                version = 1,
                text = File.ReadAllText(Path.Combine(_root, "child.sv")),
            },
        });

        // plain file parse: the declared default
        Assert.Equal("8", ParameterValue(core, childUri, "child", "WIDTH"));

        // instance parse: the override written at the instantiation. The
        // instance path is relative to the file the instantiation is written in
        // (top.sv), which is a different file from the one being opened
        // (child.sv), so parentUri is sent too.
        using (JsonDocument reparse = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            uri = childUri,
            instancePathSegments = new[] { "u_child" },
            parentUri = topUri,
        })))
        {
            handler.TryHandleRequest("rtl/reparse", reparse.RootElement, default, out object? result);
            string json = JsonSerializer.Serialize(result);
            using JsonDocument doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("instance").GetBoolean(),
                "rtl/reparse must report that it parsed the document as an instance");
        }

        Assert.Equal("4", ParameterValue(core, childUri, "child", "WIDTH"));
    }

    /// <summary>
    /// Editing a document that was opened AS an instance must keep the
    /// instantiation's parameter overrides (the editor keeps editing the
    /// instance, not the file).
    /// </summary>
    /// <remarks>
    /// A didChange re-parses the edited document. Parsing it as a PLAIN file
    /// uses the declared parameter defaults, so the overridden value silently
    /// reverted after the first keystroke: diagnostics, widths and colours all
    /// jumped back to the declaration. The re-parse has to reuse the recorded
    /// instance selection instead.
    /// </remarks>
    [Fact]
    public void DidChangeAfterInstanceClick_KeepsParameterOverrides()
    {
        File.WriteAllText(Path.Combine(_root, "child.sv"),
            "module child #(parameter WIDTH = 8) (input [WIDTH-1:0] d);\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top;\n  wire [3:0] sig;\n  child #(.WIDTH(4)) u_child(.d(sig));\nendmodule\n");
        string childUri = new Uri(Path.Combine(_root, "child.sv")).AbsoluteUri;
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);

        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new
            {
                uri = childUri,
                languageId = "systemverilog",
                version = 1,
                text = File.ReadAllText(Path.Combine(_root, "child.sv")),
            },
        });
        Assert.Equal("8", ParameterValue(core, childUri, "child", "WIDTH"));

        // open the document AS the instance (the click)
        using (JsonDocument reparse = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            uri = childUri,
            instancePathSegments = new[] { "u_child" },
            parentUri = topUri,
        })))
        {
            handler.TryHandleRequest("rtl/reparse", reparse.RootElement, default, out _);
        }
        Assert.Equal("4", ParameterValue(core, childUri, "child", "WIDTH"));

        // now EDIT the opened instance: the override must survive the re-parse
        string editedText = File.ReadAllText(Path.Combine(_root, "child.sv"))
            + "// edited\n";
        SendNotification(handler, "textDocument/didChange", new
        {
            textDocument = new { uri = childUri, version = 2 },
            contentChanges = new[] { new { text = editedText } },
        });

        Assert.Equal("4", ParameterValue(core, childUri, "child", "WIDTH"));
    }

    /// <summary>
    /// Clicking a node that is NESTED two levels deep must still parse the
    /// document AS that instance (parameter overrides applied).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The node's <c>instancePathSegments</c> are a chain from the root of the
    /// file the expansion request was made on (the file whose tree was walked),
    /// so they only resolve against THAT file. Emitting the immediate parent as
    /// the base (and sending it back as <c>parentUri</c>) made the pair
    /// inconsistent for depth &gt;= 2: the chain still carried the ancestors
    /// ("u_mid/u_leaf") while the base was mid.sv, which does not declare
    /// "u_mid". The lookup failed and the click silently degraded to a plain
    /// file parse, so the overridden parameter reverted to its declared default
    /// (reported as "the lower-level module instance does not honour the
    /// parameter override").
    /// </para>
    /// <para>
    /// This drives the SERVER-emitted tokens, exactly like the client: it never
    /// builds a token itself.
    /// </para>
    /// </remarks>
    [Fact]
    public void ReparseAsNestedInstance_AppliesParameterOverrides()
    {
        // top -> u_mid -> u_leaf, where u_leaf overrides P_WIDTH to 5.
        File.WriteAllText(Path.Combine(_root, "leaf.sv"),
            "module leaf #(parameter P_WIDTH = 8) (output [P_WIDTH-1:0] AA);\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "mid.sv"),
            "module mid;\n  wire [4:0] w;\n  leaf #(.P_WIDTH(5)) u_leaf(.AA(w));\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top;\n  mid u_mid();\nendmodule\n");
        string leafUri = new Uri(Path.Combine(_root, "leaf.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);

        // expand top.sv -> u_mid -> u_leaf with the tokens the SERVER issued
        JsonElement fileNode = FindNode(QueryTop(handler), "top.sv");
        JsonElement level0 = QueryRaw(handler, new
        {
            expandToken = fileNode.GetProperty("expandToken").GetString() ?? "",
        });
        JsonElement mid = FindNode(level0, "u_mid");
        JsonElement level1 = QueryRaw(handler, new
        {
            expandToken = mid.GetProperty("expandToken").GetString() ?? "",
        });
        JsonElement leaf = FindNode(level1, "u_leaf");

        // the chain is rooted at top.sv (the file that was expanded), so the
        // base the client echoes back has to be top.sv as well
        string[] segments = leaf.GetProperty("instancePathSegments")
            .EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
        Assert.Equal(new[] { "u_mid", "u_leaf" }, segments);
        string? parentUri = leaf.GetProperty("parentUri").GetString();
        Assert.NotNull(parentUri);
        Assert.EndsWith("top.sv", parentUri);

        // plain file parse: the declared default
        Assert.Equal("8", ParameterValue(core, leafUri, "leaf", "P_WIDTH"));

        using (JsonDocument reparse = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            uri = leafUri,
            instancePathSegments = segments,
            parentUri,
        })))
        {
            handler.TryHandleRequest("rtl/reparse", reparse.RootElement, default, out object? result);
            using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
            Assert.True(doc.RootElement.GetProperty("instance").GetBoolean(),
                "rtl/reparse must parse the document as the nested instance");
        }

        // the override written at the instantiation (not the default 8)
        Assert.Equal("5", ParameterValue(core, leafUri, "leaf", "P_WIDTH"));
    }

    /// <summary>
    /// Hierarchy nodes must carry the NavigatePanel-style badge: the editor
    /// draws an exclamation triangle (bottom-left) for errors / warnings and a
    /// shine (top-right) for unsaved edits, at most one, in the priority order
    /// error &gt; warning &gt; dirty.
    /// </summary>
    /// <remarks>
    /// VS Code's <c>TreeItem.iconPath</c> can only hold one icon and one theme
    /// colour, so the overlay cannot be drawn by the client. The server reports
    /// a <c>badge</c> name and the extension resolves
    /// <c>icons/{icon}-{badge}.svg</c>, which has the overlay composited in
    /// (<c>scripts/generate-badge-icons.js</c>). Before this the tree only tinted
    /// the whole icon, so there was no badge and no dirty indicator at all.
    /// </remarks>
    [Fact]
    public void HierarchyNodes_CarryBadge()
    {
        File.WriteAllText(Path.Combine(_root, "broken.sv"),
            "module broken;\n  assign x = ;\nendmodule\n");        // error
        // A continuous assignment width mismatch is a WARNING; the net
        // initializer form is only a hint (see Net.cs).
        File.WriteAllText(Path.Combine(_root, "warned.sv"),
            "module warned;\n  wire [4:0] bb;\n  assign bb = 8'h00;\n  initial $display(bb);\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "clean.sv"),
            "module clean;\n  wire a;\n  assign a = 1'b0;\nendmodule\n");

        string brokenUri = new Uri(Path.Combine(_root, "broken.sv")).AbsoluteUri;
        string warnedUri = new Uri(Path.Combine(_root, "warned.sv")).AbsoluteUri;
        string cleanUri = new Uri(Path.Combine(_root, "clean.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        foreach (var name in new[] { "broken.sv", "warned.sv", "clean.sv" })
        {
            string uri = new Uri(Path.Combine(_root, name)).AbsoluteUri;
            SendNotification(handler, "textDocument/didOpen", new
            {
                textDocument = new { uri, languageId = "systemverilog", version = 1, text = File.ReadAllText(Path.Combine(_root, name)) },
            });
        }

        Assert.Equal("error", BadgeOf(handler, brokenUri));
        Assert.Equal("warning", BadgeOf(handler, warnedUri));
        Assert.Null(BadgeOf(handler, cleanUri));

        // an unsaved edit adds the dirty badge
        string cleanText = File.ReadAllText(Path.Combine(_root, "clean.sv"));
        SendNotification(handler, "textDocument/didChange", new
        {
            textDocument = new { uri = cleanUri, version = 2 },
            contentChanges = new[] { new { text = cleanText + "\n// edited\n" } },
        });
        Assert.Equal("dirty", BadgeOf(handler, cleanUri));

        // saving clears it
        SendNotification(handler, "textDocument/didSave", new
        {
            textDocument = new { uri = cleanUri },
            text = cleanText + "\n// edited\n",
        });
        Assert.Null(BadgeOf(handler, cleanUri));

        // dirty never wins over a diagnostic
        SendNotification(handler, "textDocument/didChange", new
        {
            textDocument = new { uri = warnedUri, version = 2 },
            contentChanges = new[] { new { text = File.ReadAllText(Path.Combine(_root, "warned.sv")) + "\n// edited\n" } },
        });
        Assert.Equal("warning", BadgeOf(handler, warnedUri));
    }

    /// <summary>badge of the top-level node whose <c>uri</c> is <paramref name="uri"/>.</summary>
    private static string? BadgeOf(LspHandler handler, string uri)
    {
        using JsonDocument request = JsonDocument.Parse(JsonSerializer.Serialize(new { }));
        handler.TryHandleRequest("rtl/hierarchy", request.RootElement, default, out object? result);
        using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        foreach (JsonElement node in doc.RootElement.GetProperty("nodes").EnumerateArray())
        {
            if (!node.TryGetProperty("uri", out JsonElement u)) continue;
            if (!string.Equals(u.GetString(), uri, StringComparison.OrdinalIgnoreCase)) continue;
            if (!node.TryGetProperty("badge", out JsonElement b)) return null;
            return b.ValueKind == JsonValueKind.Null ? null : b.GetString();
        }
        throw new Xunit.Sdk.XunitException("node not found for " + uri);
    }

    /// <summary>
    /// The severity of each diagnostic must match the colours the editor uses
    /// for the same message, so the Messages pane and the editor marks agree:
    /// <list type="bullet">
    ///   <item><c>unused</c> → Information (blue)</item>
    ///   <item><c>bit width mismatch</c> → Warning (orange)</item>
    ///   <item>parse errors → Error (red)</item>
    ///   <item><c>Size mismatch</c> (net initializer width) → Hint (grey)</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// "Size mismatch" was reported through <c>AddWarning</c>, so it showed the
    /// same orange as a real "bit width mismatch" even though it is only an
    /// informational remark about a declaration initializer. It now uses
    /// <c>AddHint</c> (Hint → grey).
    /// </remarks>
    [Fact]
    public void Diagnostics_SeverityMatchesTheEditorColours()
    {
        File.WriteAllText(Path.Combine(_root, "mod2.v"),
            "module mod2;\n" +                               // 1
            "  wire [4:0] bb = 8'h00;\n" +                   // 2  Size mismatch (Hint)
            "  wire [2:0] aaaa;\n" +                         // 3  unused (Information)
            "  wire [3:0] driven;\n" +                       // 4  used below
            "  assign driven = 4'h0;\n" +                    // 5
            "endmodule\n");

        string mod2Uri = new Uri(Path.Combine(_root, "mod2.v")).AbsoluteUri;
        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        var pushed = new System.Collections.Generic.List<string>();
        handler.NotificationSender = message => pushed.Add(JsonSerializer.Serialize(message));

        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = mod2Uri, languageId = "systemverilog", version = 1, text = File.ReadAllText(Path.Combine(_root, "mod2.v")) },
        });

        // severity by message text (LSP: 1 Error / 2 Warning / 3 Information / 4 Hint)
        var severities = new System.Collections.Generic.Dictionary<string, int>();
        foreach (string json in pushed)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("params", out JsonElement prm)) continue;
            if (!prm.TryGetProperty("diagnostics", out JsonElement items)) continue;
            foreach (JsonElement it in items.EnumerateArray())
            {
                string msg = it.GetProperty("message").GetString() ?? "";
                severities[msg] = it.GetProperty("severity").GetInt32();
            }
        }

        Assert.True(severities.ContainsKey("Size mismatch 5 <- 8"),
            "expected a Size mismatch message (got: " + string.Join(" | ", severities.Keys) + ")");
        Assert.Equal(4, severities["Size mismatch 5 <- 8"]);   // Hint → grey
        Assert.Equal(3, severities["unused"]);                // Information → blue
    }

    /// <summary>
    /// A parameter override written at the instantiation must drive the port
    /// checks, so a port whose width is derived from the overridden parameter
    /// reports a mismatch, and that mismatch must be a Warning (orange).
    /// </summary>
    [Fact]
    public void BitWidthMismatch_IsWarning()
    {
        File.WriteAllText(Path.Combine(_root, "leaf2.sv"),
            "module leaf2 #(parameter WIDTH = 8) (output [WIDTH-1:0] d);\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "top2.sv"),
            "module top2;\n" +
            "  wire [2:0] w;\n" +
            "  leaf2 #(.WIDTH(5)) u_leaf (.d(w));\n" +
            "endmodule\n");

        string topUri = new Uri(Path.Combine(_root, "top2.sv")).AbsoluteUri;
        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        var pushed = new System.Collections.Generic.List<string>();
        handler.NotificationSender = message => pushed.Add(JsonSerializer.Serialize(message));

        foreach (var name in new[] { "leaf2.sv", "top2.sv" })
        {
            string uri = new Uri(Path.Combine(_root, name)).AbsoluteUri;
            SendNotification(handler, "textDocument/didOpen", new
            {
                textDocument = new { uri, languageId = "systemverilog", version = 1, text = File.ReadAllText(Path.Combine(_root, name)) },
            });
        }

        bool found = false;
        foreach (string json in pushed)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("params", out JsonElement prm)) continue;
            if (!prm.TryGetProperty("diagnostics", out JsonElement items)) continue;
            foreach (JsonElement it in items.EnumerateArray())
            {
                string msg = it.GetProperty("message").GetString() ?? "";
                if (!msg.StartsWith("bit width mismatch 5", StringComparison.Ordinal)) continue;
                Assert.Equal(2, it.GetProperty("severity").GetInt32());   // Warning → orange
                found = true;
            }
        }
        Assert.True(found, "expected a 'bit width mismatch 5' diagnostic");
    }

    /// <summary>
    /// Clicking an instance node must make the EDITOR's instance view resolve
    /// its parse result, i.e.
    /// <c>InstanceTextFile.ParsedDocument</c> (which looks up
    /// <c>GetInstancedParsedDocument(KeyGenerator(source, name, overrides))</c>)
    /// must return the parameter-overridden parse, not null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The editor opens the INSTANCE, so its document comes from
    /// <c>InstanceTextFile.ParsedDocument</c> → <c>VerilogFile.GetInstancedParsedDocument(key)</c>.
    /// That table is filled by
    /// <c>VerilogFile.AcceptParsedDocumentAsync</c> in the editor. The LSP host
    /// only parsed the clicked instance through <c>ParseEngine</c>, which never
    /// registered anything, so every other instance (and the clicked one after
    /// reopening) read back null and the editor fell back to the plain file
    /// parse — showing the declared parameter values instead of the overrides.
    /// </para>
    /// </remarks>
    [Fact]
    public void InstanceClick_RegistersInstanceParsedDocuments()
    {
        File.WriteAllText(Path.Combine(_root, "leaf.sv"),
            "module leaf #(parameter WIDTH = 8) (output [WIDTH-1:0] d);\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "child.sv"),
            "module child;\n" +
            "  wire [4:0] w;\n" +
            "  leaf #(.WIDTH(5)) u_leaf (.d(w));\n" +
            "endmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top;\n" +
            "  child u_child();\n" +
            "  child u_child2();\n" +
            "endmodule\n");

        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;
        string childUri = new Uri(Path.Combine(_root, "child.sv")).AbsoluteUri;
        string leafUri = new Uri(Path.Combine(_root, "leaf.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = new(core);
        string rootUri = new Uri(_root).AbsoluteUri;
        using (JsonDocument init = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            processId = 1,
            rootUri,
            workspaceFolders = new[] { new { uri = rootUri, name = "ws" } },
            capabilities = new { },
        })))
            handler.TryHandleRequest("initialize", init.RootElement, default, out _);

        foreach (var name in new[] { "leaf.sv", "child.sv", "top.sv" })
        {
            string uri = new Uri(Path.Combine(_root, name)).AbsoluteUri;
            SendNotification(handler, "textDocument/didOpen", new
            {
                textDocument = new { uri, languageId = "systemverilog", version = 1, text = File.ReadAllText(Path.Combine(_root, name)) },
            });
        }

        // click "u_child" under top.sv -> opens child.sv as an instance
        using (JsonDocument reparse = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            uri = childUri,
            instancePathSegments = new[] { "u_child" },
            parentUri = topUri,
        })))
        {
            handler.TryHandleRequest("rtl/reparse", reparse.RootElement, default, out object? result);
            using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
            Assert.True(doc.RootElement.GetProperty("instance").GetBoolean());
        }

        // the initialize above keyed the project by its root URI
        InMemoryProject project = core.GetOrCreateProjectPublic("default") as InMemoryProject
            ?? throw new InvalidOperationException();
        CodeEditor2.Data.Project pluginProject = project.PluginProject;

        // the editor's Updater populates each file's Items (the LSP runs it too)
        foreach (string name in new[] { "leaf.sv", "child.sv", "top.sv" })
        {
            if (pluginProject.GetItem(name) is pluginVerilog.Data.IVerilogRelatedFile vf)
                pluginVerilog.Data.VerilogCommon.Updater
                    .UpdateAsync(vf, new System.Threading.SemaphoreSlim(1, 1))
                    .GetAwaiter().GetResult();
        }

        // the clicked instance itself
        AssertInstanceParsed(pluginProject, "top.sv", "u_child", "child");
        // a sibling instance that was never clicked: it must be resolvable too,
        // otherwise clicking IT would show the un-overridden declaration again
        AssertInstanceParsed(pluginProject, "top.sv", "u_child2", "child");
        // the nested parameter-overridden instance under child.sv
        AssertInstanceParsed(pluginProject, "child.sv", "u_leaf", "leaf", expectInstanceParse: true);
    }

    private static void AssertInstanceParsed(
        CodeEditor2.Data.Project pluginProject, string relativePath, string instanceName,
        string expectedInstanceName, bool expectInstanceParse = false)
    {
        CodeEditor2.Data.Item? file = pluginProject.GetItem(relativePath);
        if (file == null)
        {
            var names = new System.Collections.Generic.List<string>();
            var snap = (System.Collections.IEnumerable)pluginProject.Items
                .GetType().GetMethod("GetSnapShot", Type.EmptyTypes)!.Invoke(pluginProject.Items, null)!;
            foreach (object o in snap)
                if (o is CodeEditor2.Data.Item it) names.Add(it.Name);
            Assert.Fail($"GetItem('{relativePath}') returned null. Root items: {string.Join(", ", names)}");
        }
        object itemsObj = file!.GetType().GetProperty("Items")!.GetValue(file)!;
        var snapshot = (System.Collections.IEnumerable)itemsObj
            .GetType().GetMethod("GetSnapShot", Type.EmptyTypes)!
            .Invoke(itemsObj, null)!;
        CodeEditor2.Data.Item? instance = null;
        var seen = new System.Collections.Generic.List<string>();
        foreach (object child in snapshot)
        {
            if (child is CodeEditor2.Data.Item it)
            {
                seen.Add(it.Name);
                if (it.Name == instanceName) { instance = it; break; }
            }
        }
        Assert.True(instance != null,
            $"{relativePath} has no instance '{instanceName}' (items: {string.Join(", ", seen)})");

        var instanceFile = Assert.IsAssignableFrom<pluginVerilog.Data.InstanceTextFile>(instance);
        Assert.Equal(expectedInstanceName, instanceFile.InstanceName);

        pluginVerilog.Verilog.ParsedDocument? parsed = instanceFile.ParsedDocument as pluginVerilog.Verilog.ParsedDocument;
        Assert.NotNull(parsed);
        Assert.True(parsed!.Instance,
            $"'{instanceName}' must resolve to an INSTANCE parse (parameter overrides applied), not the plain file parse");
        Assert.Equal(expectedInstanceName, parsed.TargetBuildingBlockName);
    }

    /// <summary>
    /// A parameter override written at the instantiation must drive the port
    /// checks, so a port whose width is derived from the overridden parameter
    /// reports a mismatch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>GetInstancedBuildingBlock</c> only LOOKS UP an already parsed
    /// instance (keyed by module + overrides); it never parses. The instant
    /// parse that creates that document used to run only for
    /// <c>ParseModeEnum.EditParse</c>, so in the LSP's <c>BackgroundParse</c>
    /// the parameter-overridden module was never available: the port list was
    /// unknown (checks skipped) and the parameters resolved through the
    /// un-overridden declaration, so a 5-bit port stayed 8-bit and the real
    /// mismatch was hidden.
    /// </para>
    /// <para>
    /// The ordered form (<c>#(4)</c>) had a second bug: the override was only
    /// recorded during the prototype pass, so the real pass lost it.
    /// </para>
    /// </remarks>
    [Fact]
    public void ParameterOverride_DrivesPortWidthCheck()
    {
        // AA is [P_WIDTH-1:0]; overridden to 5 while the connected signal is 3 bit
        File.WriteAllText(Path.Combine(_root, "leaf.sv"),
            "module leaf #(parameter P_WIDTH = 8) (\n" +
            "  output [P_WIDTH-1:0] AA,\n" +
            "  input                CLK_I\n" +
            ");\n" +
            "endmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top;\n" +
            "  wire [2:0] w1;\n" +
            "  wire       clk;\n" +
            "  leaf #(\n" +
            "    .P_WIDTH ( 5 )\n" +
            "  ) u_leaf (\n" +
            "    .AA    ( w1 ),\n" +
            "    .CLK_I ( clk )\n" +
            "  );\n" +
            "endmodule\n");
        File.WriteAllText(Path.Combine(_root, "ordered.sv"),
            "module ordered;\n" +
            "  wire [2:0] w1;\n" +
            "  wire       clk;\n" +
            "  leaf #(5) u_leaf (w1, clk);\n" +
            "endmodule\n");

        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;
        string orderedUri = new Uri(Path.Combine(_root, "ordered.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);

        var pushed = new System.Collections.Generic.List<string>();
        handler.NotificationSender = message =>
            pushed.Add(JsonSerializer.Serialize(message));

        foreach (var name in new[] { "leaf.sv", "top.sv", "ordered.sv" })
        {
            string uri = new Uri(Path.Combine(_root, name)).AbsoluteUri;
            SendNotification(handler, "textDocument/didOpen", new
            {
                textDocument = new { uri, languageId = "systemverilog", version = 1, text = File.ReadAllText(Path.Combine(_root, name)) },
            });
        }
        // reparse the consumers now that leaf.sv is registered
        foreach (var uri in new[] { topUri, orderedUri })
        {
            using JsonDocument reparse = JsonDocument.Parse(JsonSerializer.Serialize(new { uri }));
            handler.TryHandleRequest("rtl/reparse", reparse.RootElement, default, out _);
        }

        // named form: the overridden width (5) must be compared, not the default (8)
        Assert.Contains(pushed, json => MentionsFileWithMismatch(json, "top.sv"));
        // ordered form: same, the override must survive the real parse pass
        Assert.Contains(pushed, json => MentionsFileWithMismatch(json, "ordered.sv"));
    }

    /// <summary>
    /// True when the publishDiagnostics payload is for <paramref name="fileName"/>
    /// and reports the 5 -> 3 bit width mismatch. The message is JSON-encoded, so
    /// "&lt;" arrives as \u003C.
    /// </summary>
    private static bool MentionsFileWithMismatch(string json, string fileName)
    {
        if (!json.Contains(fileName, StringComparison.Ordinal)) return false;
        return json.Contains("bit width mismatch 5", StringComparison.Ordinal)
            && json.Contains("3", StringComparison.Ordinal)
            && json.Contains("verilog/bit-width-mismatch-5-3", StringComparison.Ordinal);
    }

    /// <summary>
    /// Clicking an instance node must not lose the syntax highlighting of the
    /// opened document.
    /// </summary>
    /// <remarks>
    /// The instance reparse resolved the instance path in the parent's item
    /// tree but also fed the PARENT's <c>VerilogFile</c> to the parser. The
    /// parser then built its document (text file / SystemVerilog flag / colour
    /// table) from the parent file, so only a fraction of the opened document
    /// ended up coloured (observed on
    /// <c>verilog_sample/mySample/PartialAssign.sv</c> -> <c>aaabb</c> ->
    /// <c>mod2.v</c>: 31 tokens before the click, 11 after). The walk must use
    /// the parent and the parse must use THIS file.
    /// </remarks>
    [Fact]
    public void ReparseAsInstance_KeepsSyntaxHighlighting()
    {
        // The shapes below are the ones of verilog_sample/mySample, where the
        // bug was reported: the parent (PartialAssign.sv) instantiates mod2,
        // which itself contains a parameterised instance of moduleSample.
        // child.v is Verilog (.v) while the parent is SystemVerilog (.sv), so
        // using the parent's file object also switched the language flag.
        File.WriteAllText(Path.Combine(_root, "child.v"),
            "module child(\n" +
            "input   in_a,   // @sync clock\n" +
            "input   in_b    // @sync reset\n" +
            ");\n" +
            "\n" +
            "wire [4:0] w0 = 5'h00;\n" +
            "\n" +
            "wire [2:0] w1;\n" +
            "\n" +
            "leaf #(\n" +
            ".P_WIDTH\t( 5 )\n" +
            ") u_leaf (\n" +
            ".AA\t( w1 ),\n" +
            ".CLK_I\t( w0 ),\n" +
            ".RST_X_I\t(  )\n" +
            ");\n" +
            "\n" +
            "always begin\n" +
            "end\n" +
            "endmodule\n");
        File.WriteAllText(Path.Combine(_root, "leaf.v"),
            "module leaf #(parameter P_WIDTH = 8) (\n" +
            "  output [P_WIDTH-1:0] AA,\n" +
            "  input                CLK_I,\n" +
            "  input                RST_X_I\n" +
            ");\n" +
            "  wire [7:0] bbbb = AA;\n" +
            "  initial begin\n" +
            "  end\n" +
            "endmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "`timescale 1ns / 1ps\n" +
            "\n" +
            "module top;\n" +
            "\n" +
            "wire [3:0]\taaa;\n" +
            "assign\taaa[0] = 1'b1;\n" +
            "assign\taaa[3] = 1'b1;\n" +
            "\n" +
            "child AAA();\n" +
            "\n" +
            "child bb();\n" +
            "\n" +
            "endmodule\n");
        string childPath = Path.Combine(_root, "child.v");
        string topPath = Path.Combine(_root, "top.sv");
        string childUri = new Uri(childPath).AbsoluteUri;
        string topUri = new Uri(topPath).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);

        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = topUri, languageId = "systemverilog", version = 1, text = File.ReadAllText(topPath) },
        });
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = childUri, languageId = "verilog", version = 1, text = File.ReadAllText(childPath) },
        });

        int tokensBefore = SemanticTokenCount(handler, childUri);

        // clicking the SECOND instance ("bb"), like clicking aaabb in
        // PartialAssign.sv (the instance that is not the first one)
        using (JsonDocument reparse = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            uri = childUri,
            instancePathSegments = new[] { "bb" },
            parentUri = topUri,
        })))
        {
            handler.TryHandleRequest("rtl/reparse", reparse.RootElement, default, out object? result);
            using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
            Assert.True(doc.RootElement.GetProperty("instance").GetBoolean(),
                "rtl/reparse must parse the document as an instance");
        }

        int tokensAfter = SemanticTokenCount(handler, childUri);
        Assert.True(tokensBefore > 0, "expected semantic tokens before the click");
        Assert.Equal(tokensBefore, tokensAfter);

        // and the module / endmodule keywords must still be coloured
        JsonElement tokens = SemanticTokens(handler, childUri);
        JsonElement data = tokens.GetProperty("data");
        var lines = File.ReadAllText(childPath).Split('\n');
        bool sawModule = false, sawEndModule = false;
        int line = 0, character = 0;
        for (int i = 0; i + 4 < data.GetArrayLength(); i += 5)
        {
            int deltaLine = data[i].GetInt32();
            int deltaCharacter = data[i + 1].GetInt32();
            int length = data[i + 2].GetInt32();
            line += deltaLine;
            character = (deltaLine == 0) ? character + deltaCharacter : deltaCharacter;
            if (line >= lines.Length) continue;
            string source = lines[line].TrimEnd('\r');
            if (character < 0 || character + length > source.Length) continue;
            string token = source.Substring(character, length);
            if (token == "module") sawModule = true;
            if (token == "endmodule") sawEndModule = true;
        }
        Assert.True(sawModule, "the module keyword lost its colour after the instance click");
        Assert.True(sawEndModule, "the endmodule keyword lost its colour after the instance click");
    }

    private static JsonElement SemanticTokens(LspHandler handler, string uri)
    {
        using JsonDocument request = JsonDocument.Parse(JsonSerializer.Serialize(new { textDocument = new { uri } }));
        handler.TryHandleRequest("textDocument/semanticTokens/full", request.RootElement, default, out object? result);
        return JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement.Clone();
    }

    private static int SemanticTokenCount(LspHandler handler, string uri)
    {
        JsonElement tokens = SemanticTokens(handler, uri);
        return tokens.TryGetProperty("data", out JsonElement data) ? data.GetArrayLength() / 5 : 0;
    }

    /// <summary>
    /// An ORDERED port connection (<c>inst(sig)</c>) must run the same port
    /// checks a named connection (<c>inst(.port(sig))</c>) does, so a width
    /// mismatch is reported.
    ///
    /// <para>
    /// The ordered path used to parse the expression and drop it: it neither
    /// recorded the connection nor called the port checks, so a 4-bit signal
    /// connected to an 8-bit port produced no diagnostic at all. Only the named
    /// form was checked.
    /// </para>
    /// </summary>
    [Fact]
    public void OrderedPortConnection_ReportsBitWidthMismatch()
    {
        File.WriteAllText(Path.Combine(_root, "child.sv"),
            "module child (input [7:0] d);\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top;\n  wire [3:0] sig;\n  child u_child(sig);\nendmodule\n");
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new
            {
                uri = topUri,
                languageId = "systemverilog",
                version = 1,
                text = File.ReadAllText(Path.Combine(_root, "top.sv")),
            },
        });

        Assert.Contains(DiagnosticMessages(core, topUri),
            m => m.Contains("bit width mismatch", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The named form must keep reporting the same mismatch (the ordered fix
    /// must not have moved the check into a path the named form no longer
    /// reaches).
    /// </summary>
    [Fact]
    public void NamedPortConnection_ReportsBitWidthMismatch()
    {
        File.WriteAllText(Path.Combine(_root, "child.sv"),
            "module child (input [7:0] d);\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top;\n  wire [3:0] sig;\n  child u_child(.d(sig));\nendmodule\n");
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new
            {
                uri = topUri,
                languageId = "systemverilog",
                version = 1,
                text = File.ReadAllText(Path.Combine(_root, "top.sv")),
            },
        });

        Assert.Contains(DiagnosticMessages(core, topUri),
            m => m.Contains("bit width mismatch", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Two module instances connecting their OUTPUT ports to the same net is a
    /// multiple drive and must be reported.
    ///
    /// <para>
    /// An output port drives the connected signal exactly like a continuous
    /// assignment (IEEE 1800-2017 6.5 / 23.3.3), but the port-connection path
    /// only called <c>AssertAssigned()</c> (which fills AssignedMap for the
    /// "undriven" notice) and never ran the multiple-drive check, so a double
    /// drive through output ports produced no diagnostic at all.
    /// </para>
    /// </summary>
    [Fact]
    public void OutputPorts_DrivingTheSameNet_IsReported()
    {
        File.WriteAllText(Path.Combine(_root, "child.sv"),
            "module child (output o, input i);\n  assign o = i;\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top;\n  wire w;\n  wire a, b;\n  child u1(.o(w), .i(a));\n  child u2(.o(w), .i(b));\nendmodule\n");
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new
            {
                uri = topUri,
                languageId = "systemverilog",
                version = 1,
                text = File.ReadAllText(Path.Combine(_root, "top.sv")),
            },
        });

        Assert.Contains(DiagnosticMessages(core, topUri),
            m => m.Contains("multiple module instance output ports", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The ordered form (<c>inst(w, a)</c>) must report the same double drive
    /// as the named form.
    /// </summary>
    [Fact]
    public void OutputPorts_OrderedConnection_DrivingTheSameNet_IsReported()
    {
        File.WriteAllText(Path.Combine(_root, "child.sv"),
            "module child (output o, input i);\n  assign o = i;\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top;\n  wire w;\n  wire a, b;\n  child u1(w, a);\n  child u2(w, b);\nendmodule\n");
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new
            {
                uri = topUri,
                languageId = "systemverilog",
                version = 1,
                text = File.ReadAllText(Path.Combine(_root, "top.sv")),
            },
        });

        Assert.Contains(DiagnosticMessages(core, topUri),
            m => m.Contains("multiple module instance output ports", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// An output port plus a continuous assignment on the same net is also a
    /// multiple drive (the existing continuous-assignment wording is reused).
    /// </summary>
    [Fact]
    public void OutputPort_AndContinuousAssign_OnTheSameNet_IsReported()
    {
        File.WriteAllText(Path.Combine(_root, "child.sv"),
            "module child (output o, input i);\n  assign o = i;\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top;\n  wire w;\n  wire a;\n  child u1(.o(w), .i(a));\n  assign w = a;\nendmodule\n");
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new
            {
                uri = topUri,
                languageId = "systemverilog",
                version = 1,
                text = File.ReadAllText(Path.Combine(_root, "top.sv")),
            },
        });

        Assert.Contains(DiagnosticMessages(core, topUri),
            m => m.Contains("multiple continuous assignments", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A single instance driving a net must stay clean: the new check must not
    /// report a false positive on the ordinary case.
    /// </summary>
    [Fact]
    public void SingleOutputPort_IsNotReported()
    {
        File.WriteAllText(Path.Combine(_root, "child.sv"),
            "module child (output o, input i);\n  assign o = i;\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top;\n  wire w;\n  wire a;\n  child u1(.o(w), .i(a));\nendmodule\n");
        string topUri = new Uri(Path.Combine(_root, "top.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new
            {
                uri = topUri,
                languageId = "systemverilog",
                version = 1,
                text = File.ReadAllText(Path.Combine(_root, "top.sv")),
            },
        });

        List<string> messages = DiagnosticMessages(core, topUri);
        Assert.DoesNotContain(messages, m => m.Contains("multiple module instance output ports", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(messages, m => m.Contains("multiple continuous assignments", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Diagnostic messages the server currently holds for a file.</summary>
    private static List<string> DiagnosticMessages(InMemorySystemVerilogCore core, string uri)
    {
        InMemoryProject project = core.GetOrCreateProject("default");
        InMemoryFile file = (InMemoryFile)project.FindFile(uri)!;
        List<string> messages = new();
        foreach (ISystemVerilogDiagnostic d in file.Diagnostics)
        {
            messages.Add(d.Message);
        }
        return messages;
    }

    /// <summary>
    /// The expression text of a parameter in the file's current parse result
    /// (e.g. "8" for the declared default, "4" after the instance override).
    /// </summary>
    private static string? ParameterValue(
        InMemorySystemVerilogCore core, string uri, string blockName, string parameterName)
    {
        InMemoryProject project = core.GetOrCreateProject("default");
        InMemoryFile file = (InMemoryFile)project.FindFile(uri)!;
        pluginVerilog.Verilog.ParsedDocument? parsed = file.ParsedDocument;
        if (parsed?.Root == null) return null;
        if (!parsed.Root.BuildingBlocks.TryGetValue(blockName, out var block)) return null;
        if (!block.NamedElements.TryGetValue(parameterName, out var element)) return null;
        if (element is not pluginVerilog.Verilog.DataObjects.Constants.Constants constant) return null;
        return constant.Expression?.CreateString();
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
