using System;
using System.IO;
using System.Text.Json;
using SystemVerilogLanguageServer.Server;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// The standard LSP features that were wired up (foldingRange, documentHighlight,
/// rename, workspace/symbol, documentLink, selectionRange, semanticTokens/range,
/// signatureHelp, formatting) must answer with the right wire shape even when
/// there is nothing to report, and must produce real results where the parser
/// already has the data.
/// </summary>
public class StandardLspFeatureTests : IDisposable
{
    private readonly string _root;

    public StandardLspFeatureTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "svlsp_feat_" + Guid.NewGuid().ToString("N"));
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
        // round-trip through JSON so the assertions see the wire shape
        string serialized = JsonSerializer.Serialize(result);
        return JsonDocument.Parse(serialized).RootElement;
    }

    [Fact]
    public void FoldingRange_ReturnsTheModuleBody()
    {
        const string text = "module top;\n  wire a;\n  wire b;\nendmodule\n";
        string uri = WriteFile("fold.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "textDocument/foldingRange",
            new { textDocument = new { uri } });

        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.True(result.GetArrayLength() > 0, "expected at least the module body fold");
        // the parser records the fold from the first body line to the line
        // before `endmodule`, so the range must be a real multi-line span
        JsonElement first = result[0];
        int startLine = first.GetProperty("startLine").GetInt32();
        int endLine = first.GetProperty("endLine").GetInt32();
        Assert.True(startLine >= 0 && startLine <= 1, $"unexpected start line {startLine}");
        Assert.True(endLine > startLine, $"expected a multi-line fold, got {startLine}..{endLine}");
        Assert.True(endLine <= 3, $"fold must stay inside the module, got end {endLine}");
    }

    [Fact]
    public void DocumentHighlight_ReturnsDeclarationAndUses()
    {
        const string text = "module top;\n  wire sig;\n  assign sig = 1'b0;\nendmodule\n";
        string uri = WriteFile("hl.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        // caret on the declaration of `sig` (line 1, character 7)
        JsonElement result = Request(handler, "textDocument/documentHighlight", new
        {
            textDocument = new { uri },
            position = new { line = 1, character = 7 },
        });

        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.True(result.GetArrayLength() >= 2, "expected the declaration and at least one use");
    }

    [Fact]
    public void Rename_ProducesEditsForEveryOccurrence()
    {
        const string text = "module top;\n  wire sig;\n  assign sig = 1'b0;\nendmodule\n";
        string uri = WriteFile("ren.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "textDocument/rename", new
        {
            textDocument = new { uri },
            position = new { line = 1, character = 7 },
            newName = "renamed",
        });

        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        JsonElement changes = result.GetProperty("changes");
        Assert.True(changes.TryGetProperty(uri, out JsonElement edits));
        Assert.True(edits.GetArrayLength() >= 2);
        foreach (JsonElement edit in edits.EnumerateArray())
        {
            Assert.Equal("renamed", edit.GetProperty("newText").GetString());
        }
    }

    [Fact]
    public void PrepareRename_ReturnsTheSymbolRange()
    {
        const string text = "module top;\n  wire sig;\nendmodule\n";
        string uri = WriteFile("prep.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "textDocument/prepareRename", new
        {
            textDocument = new { uri },
            position = new { line = 1, character = 7 },
        });

        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal("sig", result.GetProperty("placeholder").GetString());
    }

    [Fact]
    public void WorkspaceSymbol_FindsDeclarationsByName()
    {
        const string text = "module top;\n  wire sig;\nendmodule\n";
        string uri = WriteFile("sym.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "workspace/symbol", new { query = "sig" });

        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        bool found = false;
        foreach (JsonElement s in result.EnumerateArray())
        {
            if (s.GetProperty("name").GetString() == "sig") { found = true; break; }
        }
        Assert.True(found, "expected the `sig` declaration in workspace/symbol");
    }

    [Fact]
    public void DocumentLink_ResolvesIncludeTarget()
    {
        File.WriteAllText(Path.Combine(_root, "defs.svh"), "`define A 1\n");
        const string text = "`include \"defs.svh\"\nmodule top;\nendmodule\n";
        string uri = WriteFile("link.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "textDocument/documentLink",
            new { textDocument = new { uri } });

        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.True(result.GetArrayLength() > 0, "expected a link on the include target");
        string target = result[0].GetProperty("target").GetString() ?? "";
        Assert.Contains("defs.svh", target);
    }

    [Fact]
    public void SelectionRange_ReturnsWordLineDocumentChain()
    {
        const string text = "module top;\n  wire sig;\nendmodule\n";
        string uri = WriteFile("sel.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "textDocument/selectionRange", new
        {
            textDocument = new { uri },
            positions = new[] { new { line = 1, character = 8 } },
        });

        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(1, result.GetArrayLength());
        JsonElement range = result[0];
        Assert.True(range.TryGetProperty("range", out _));
        Assert.True(range.TryGetProperty("parent", out JsonElement parent));
        Assert.True(parent.TryGetProperty("parent", out _), "expected word -> line -> document");
    }

    [Fact]
    public void SemanticTokensRange_ReturnsOnlyTheRequestedLines()
    {
        const string text = "module top;\n  wire sig;\nendmodule\n";
        string uri = WriteFile("tok.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "textDocument/semanticTokens/range", new
        {
            textDocument = new { uri },
            range = new
            {
                start = new { line = 1, character = 0 },
                end = new { line = 1, character = 20 },
            },
        });

        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.True(result.TryGetProperty("data", out JsonElement data));
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        // every token must start on line 1 (delta line 0 after the first)
        int line = 0;
        for (int i = 0; i + 4 < data.GetArrayLength(); i += 5)
        {
            line += data[i].GetInt32();
            Assert.Equal(1, line);
        }
    }

    [Fact]
    public void HierarchyNodes_CarryPresentationMetadata()
    {
        File.WriteAllText(Path.Combine(_root, "leaf.sv"), "module leaf; endmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top;\n  leaf u_leaf();\nendmodule\n");
        LspHandler handler = CreateInitializedHandler();

        // top level: the workspace folder listing (response is { nodes: [...] })
        JsonElement topResult = Request(handler, "rtl/hierarchy", new { nodePath = Array.Empty<int>() });
        JsonElement top = topResult.GetProperty("nodes");
        Assert.Equal(JsonValueKind.Array, top.ValueKind);
        Assert.True(top.GetArrayLength() > 0);

        JsonElement fileNode = default;
        foreach (JsonElement n in top.EnumerateArray())
        {
            if (n.GetProperty("label").GetString() == "top.sv") { fileNode = n; break; }
        }
        Assert.NotEqual(JsonValueKind.Undefined, fileNode.ValueKind);
        Assert.Equal("rtlFile", fileNode.GetProperty("contextValue").GetString());
        Assert.Equal("file", fileNode.GetProperty("icon").GetString());
        Assert.Equal("collapsed", fileNode.GetProperty("collapsible").GetString());
        Assert.True(fileNode.TryGetProperty("fsPath", out JsonElement fsPath));
        Assert.EndsWith("top.sv", fsPath.GetString());
        Assert.True(fileNode.TryGetProperty("dirPath", out _));

        // expand the file: the instance node must carry the same metadata
        string uri = fileNode.GetProperty("uri").GetString() ?? "";
        JsonElement childResult = Request(handler, "rtl/hierarchy", new { uri, nodePath = Array.Empty<int>() });
        JsonElement children = childResult.GetProperty("nodes");
        Assert.True(children.GetArrayLength() > 0);
        JsonElement inst = children[0];
        Assert.Equal("rtlInstance", inst.GetProperty("contextValue").GetString());
        Assert.Equal("symbol-class", inst.GetProperty("icon").GetString());
        Assert.Equal("u_leaf", inst.GetProperty("instancePath").GetString());
        Assert.Contains("u_leaf", inst.GetProperty("tooltip").GetString() ?? "");
    }

    [Fact]
    public void DidSave_PushesHierarchyInvalidated()
    {
        const string text = "module top;\nendmodule\n";
        string uri = WriteFile("push.sv", text);
        LspHandler handler = CreateInitializedHandler();

        // capture the server-initiated notifications
        System.Collections.Generic.List<LspMessage> pushed = new();
        handler.NotificationSender = (msg) => pushed.Add(msg);

        DidOpen(handler, uri, text);
        pushed.Clear();

        string json = JsonSerializer.Serialize(new
        {
            textDocument = new { uri },
            text = "module top;\n  wire a;\nendmodule\n",
        });
        using JsonDocument doc = JsonDocument.Parse(json);
        handler.TryHandleNotification("textDocument/didSave", doc.RootElement, default);

        bool found = false;
        foreach (LspMessage m in pushed)
        {
            if (m.Method == "rtl/hierarchyInvalidated") { found = true; break; }
        }
        Assert.True(found, "expected rtl/hierarchyInvalidated after didSave");
    }

    [Fact]
    public void DidChangeWatchedFiles_PushesHierarchyInvalidated()
    {
        LspHandler handler = CreateInitializedHandler();
        System.Collections.Generic.List<LspMessage> pushed = new();
        handler.NotificationSender = (msg) => pushed.Add(msg);

        string json = JsonSerializer.Serialize(new
        {
            changes = new[] { new { uri = new Uri(Path.Combine(_root, "new.sv")).AbsoluteUri, type = 1 } },
        });
        using JsonDocument doc = JsonDocument.Parse(json);
        handler.TryHandleNotification("workspace/didChangeWatchedFiles", doc.RootElement, default);

        bool found = false;
        foreach (LspMessage m in pushed)
        {
            if (m.Method == "rtl/hierarchyInvalidated") { found = true; break; }
        }
        Assert.True(found, "expected rtl/hierarchyInvalidated after watched-file change");
    }

    [Fact]
    public void UnimplementedFeatures_ReturnEmptyResults()
    {
        const string text = "module top;\nendmodule\n";
        string uri = WriteFile("empty.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        // codeAction / codeLens / inlayHint are still stubs: they must answer
        // with an empty array (not null / not an error) so the client is happy.
        foreach (string method in new[] { "textDocument/codeAction", "textDocument/codeLens", "textDocument/inlayHint" })
        {
            JsonElement result = Request(handler, method, new
            {
                textDocument = new { uri },
                range = new
                {
                    start = new { line = 0, character = 0 },
                    end = new { line = 0, character = 0 },
                },
                context = new { diagnostics = Array.Empty<object>() },
            });
            Assert.Equal(JsonValueKind.Array, result.ValueKind);
            Assert.Equal(0, result.GetArrayLength());
        }
    }
}
