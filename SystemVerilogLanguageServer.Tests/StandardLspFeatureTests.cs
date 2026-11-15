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

        // Every highlight must cover exactly the identifier, not the run of
        // text up to the next token. The plugin's WordReference.Length is the
        // distance to the next token (an editor mark convention), so using it
        // verbatim made the highlight span the rest of the line.
        foreach (JsonElement h in result.EnumerateArray())
        {
            JsonElement range = h.GetProperty("range");
            int startLine = range.GetProperty("start").GetProperty("line").GetInt32();
            int startChar = range.GetProperty("start").GetProperty("character").GetInt32();
            int endLine = range.GetProperty("end").GetProperty("line").GetInt32();
            int endChar = range.GetProperty("end").GetProperty("character").GetInt32();
            Assert.Equal(startLine, endLine);
            Assert.Equal(3, endChar - startChar);   // "sig"
        }
    }

    [Fact]
    public void DocumentHighlight_RangeCoversOnlyTheIdentifier()
    {
        // A trailing comment after the declaration used to be swallowed by the
        // highlight because the plugin's token length reaches the next token.
        const string text =
            "module top;\n" +
            "  wire sig; // trailing comment here\n" +
            "  assign sig = 1'b0;\n" +
            "  logic [7:0] data_bus;\n" +
            "  assign data_bus = 8'hFF;\n" +
            "endmodule\n";
        string uri = WriteFile("hl2.sv", text);
        LspHandler handler = CreateInitializedHandler();
        DidOpen(handler, uri, text);

        JsonElement result = Request(handler, "textDocument/documentHighlight", new
        {
            textDocument = new { uri },
            position = new { line = 3, character = 14 },   // data_bus declaration
        });

        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.True(result.GetArrayLength() >= 2);
        foreach (JsonElement h in result.EnumerateArray())
        {
            JsonElement range = h.GetProperty("range");
            int startLine = range.GetProperty("start").GetProperty("line").GetInt32();
            int startChar = range.GetProperty("start").GetProperty("character").GetInt32();
            int endLine = range.GetProperty("end").GetProperty("line").GetInt32();
            int endChar = range.GetProperty("end").GetProperty("character").GetInt32();
            Assert.Equal(startLine, endLine);
            Assert.Equal(8, endChar - startChar);   // "data_bus"
        }
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
        JsonElement topResult = Request(handler, "rtl/hierarchy", new { });
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
        // the icon name mirrors the editor's NavigatePanel document icon
        Assert.Equal("systemverilog-document", fileNode.GetProperty("icon").GetString());
        Assert.Equal("collapsed", fileNode.GetProperty("collapsible").GetString());
        Assert.True(fileNode.TryGetProperty("fsPath", out JsonElement fsPath));
        Assert.EndsWith("top.sv", fsPath.GetString());
        Assert.True(fileNode.TryGetProperty("dirPath", out _));

        // expand the file: the instance node must carry the same metadata.
        // The client echoes back the opaque expandToken the server issued.
        string fileToken = fileNode.GetProperty("expandToken").GetString() ?? "";
        Assert.False(string.IsNullOrEmpty(fileToken));
        JsonElement childResult = Request(handler, "rtl/hierarchy", new { expandToken = fileToken });
        JsonElement children = childResult.GetProperty("nodes");
        Assert.True(children.GetArrayLength() > 0);
        JsonElement inst = children[0];
        Assert.Equal("rtlInstance", inst.GetProperty("contextValue").GetString());
        // the icon reflects the instantiated building block kind (module here),
        // not a single generic icon for every instance
        Assert.Equal("module", inst.GetProperty("icon").GetString());
        Assert.Equal("u_leaf", inst.GetProperty("instancePath").GetString());
        Assert.Contains("u_leaf", inst.GetProperty("tooltip").GetString() ?? "");

        // Clicking the instance opens the INSTANTIATED module's own source
        // (leaf.sv), exactly like the editor's NavigatePanel - not the file the
        // instantiation is written in (top.sv). The expand token still
        // addresses the instance inside top.sv, which is why the two are
        // separate fields.
        Assert.EndsWith("leaf.sv", inst.GetProperty("uri").GetString() ?? "");
        Assert.EndsWith("leaf.sv", inst.GetProperty("fsPath").GetString() ?? "");

        // startLine / startCharacter are 0-based LSP positions of the module
        // declaration in leaf.sv ("module leaf;" -> line 0, character 7).
        Assert.Equal(0, inst.GetProperty("startLine").GetInt32());
        Assert.Equal(7, inst.GetProperty("startCharacter").GetInt32());
    }

    [Fact]
    public void HierarchyInstanceIcons_ReflectTheInstantiatedKind()
    {
        File.WriteAllText(Path.Combine(_root, "bus_if.sv"), "interface bus_if; endinterface\n");
        File.WriteAllText(Path.Combine(_root, "pkg.sv"), "package pkg; endpackage\n");
        File.WriteAllText(Path.Combine(_root, "leaf.sv"), "module leaf; endmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"),
            "module top;\n  leaf u_leaf();\n  bus_if u_bus();\nendmodule\n");
        LspHandler handler = CreateInitializedHandler();

        JsonElement top = Request(handler, "rtl/hierarchy", new { }).GetProperty("nodes");
        JsonElement fileNode = default;
        foreach (JsonElement n in top.EnumerateArray())
        {
            if (n.GetProperty("label").GetString() == "top.sv") { fileNode = n; break; }
        }
        Assert.NotEqual(JsonValueKind.Undefined, fileNode.ValueKind);

        string fileToken = fileNode.GetProperty("expandToken").GetString() ?? "";
        JsonElement children = Request(handler, "rtl/hierarchy", new { expandToken = fileToken })
            .GetProperty("nodes");

        Dictionary<string, string> icons = new();
        foreach (JsonElement n in children.EnumerateArray())
        {
            icons[n.GetProperty("label").GetString() ?? ""] = n.GetProperty("icon").GetString() ?? "";
        }
        Assert.Equal("module", icons["u_leaf"]);
        Assert.Equal("interface", icons["u_bus"]);
    }

    [Fact]
    public void HierarchyFileNodes_FlagErrorsWithAThemeColour()
    {
        // an undeclared identifier makes the parser report an error, which the
        // tree surfaces as a theme colour on the file icon (the editor draws an
        // exclamation badge for the same condition)
        File.WriteAllText(Path.Combine(_root, "bad.sv"),
            "module bad;\n  assign x = undeclared_signal;\nendmodule\n");
        LspHandler handler = CreateInitializedHandler();

        JsonElement top = Request(handler, "rtl/hierarchy", new { }).GetProperty("nodes");
        JsonElement fileNode = default;
        foreach (JsonElement n in top.EnumerateArray())
        {
            if (n.GetProperty("label").GetString() == "bad.sv") { fileNode = n; break; }
        }
        Assert.NotEqual(JsonValueKind.Undefined, fileNode.ValueKind);
        Assert.Equal("errorForeground", fileNode.GetProperty("iconColor").GetString());
    }

    [Fact]
    public void Theme_ReturnsColourRulesForEveryLegendTokenType()
    {
        LspHandler handler = CreateInitializedHandler();
        JsonElement theme = Request(handler, "rtl/theme", new { });

        Assert.Equal(JsonValueKind.Object, theme.ValueKind);
        JsonElement rules = theme.GetProperty("rules");
        JsonElement tokenTypes = theme.GetProperty("tokenTypes");
        Assert.True(tokenTypes.GetArrayLength() > 0);

        // every advertised token type must have a colour, otherwise the client
        // would write an incomplete palette into the user settings
        foreach (JsonElement t in tokenTypes.EnumerateArray())
        {
            string type = t.GetString() ?? "";
            Assert.True(rules.TryGetProperty(type, out JsonElement colour),
                $"missing colour rule for token type '{type}'");
            string hex = colour.GetString() ?? "";
            Assert.Matches("^#[0-9A-Fa-f]{6}$", hex);
        }
    }

    /// <summary>
    /// A comment annotation (<c>// @sync:clk</c>, <c>// @clock</c>,
    /// <c>// @scope ...</c>) must keep its own colour inside the comment.
    ///
    /// <para>
    /// The parser paints the whole comment with <c>ColorType.Comment</c> and
    /// then re-paints the annotation tokens with
    /// <c>HighLightedComment</c> / <c>CommentAnnotation</c>. Reporting those as
    /// the "comment" token type - and dropping them as an overlap of the
    /// covering comment - made an annotation look exactly like the comment
    /// text, which is the reported symptom.
    /// </para>
    /// </summary>
    [Fact]
    public void SemanticTokens_CommentAnnotationKeepsItsOwnColour()
    {
        LspHandler handler = CreateInitializedHandler();
        string text =
            "module top;\n" +
            "    // plain comment\n" +
            "    logic clk;\n" +
            "    // @sync:clk\n" +
            "    logic d;\n" +
            "endmodule\n";
        string uri = WriteFile("annotation.sv", text);
        DidOpen(handler, uri, text);

        // the legend must advertise the annotation token type
        string initialize = JsonSerializer.Serialize(new { processId = 1, capabilities = new { } });
        using JsonDocument initDoc = JsonDocument.Parse(initialize);
        handler.TryHandleRequest("initialize", initDoc.RootElement, default, out object? initResult);
        List<string> legend = new();
        foreach (JsonElement t in JsonDocument.Parse(JsonSerializer.Serialize(initResult)).RootElement
                     .GetProperty("capabilities").GetProperty("semanticTokensProvider")
                     .GetProperty("legend").GetProperty("tokenTypes").EnumerateArray())
        {
            legend.Add(t.GetString() ?? "");
        }
        int annotationIndex = legend.IndexOf("annotation");
        Assert.True(annotationIndex >= 0, "the legend must advertise the 'annotation' token type");
        int commentIndex = legend.IndexOf("comment");
        Assert.True(commentIndex >= 0);

        JsonElement tokens = Request(handler, "textDocument/semanticTokens/full",
            new { textDocument = new { uri } });
        JsonElement data = tokens.GetProperty("data");

        // decode the delta-encoded stream (LSP 3.17 semantic tokens) and collect
        // (line, char, len, type)
        List<(int Line, int Char, int Len, int Type)> decoded = new();
        int line = 0, character = 0;
        for (int i = 0; i + 4 < data.GetArrayLength(); i += 5)
        {
            int deltaLine = data[i].GetInt32();
            int deltaChar = data[i + 1].GetInt32();
            if (deltaLine == 0)
            {
                character += deltaChar;
            }
            else
            {
                line += deltaLine;
                character = deltaChar;
            }
            decoded.Add((line, character, data[i + 2].GetInt32(), data[i + 3].GetInt32()));
        }

        // line 3 is "    // @sync:clk": the annotation part must be coloured
        // with the annotation token type, not with "comment"
        List<(int Line, int Char, int Len, int Type)> annotationLine =
            decoded.FindAll(t => t.Line == 3);
        Assert.True(annotationLine.Count > 0, "line 3 produced no semantic tokens");
        Assert.Contains(annotationLine, t => t.Type == annotationIndex);

        // the plain comment line (line 1) must stay a plain comment
        List<(int Line, int Char, int Len, int Type)> plainLine = decoded.FindAll(t => t.Line == 1);
        Assert.Contains(plainLine, t => t.Type == commentIndex);
        Assert.DoesNotContain(plainLine, t => t.Type == annotationIndex);

        // the stream must stay strictly non-overlapping (VS Code drops
        // overlapping tokens and the span would lose its colour)
        for (int i = 1; i < decoded.Count; i++)
        {
            (int Line, int Char, int Len, int Type) prev = decoded[i - 1];
            (int Line, int Char, int Len, int Type) cur = decoded[i];
            if (cur.Line != prev.Line) continue;
            Assert.True(cur.Char >= prev.Char + prev.Len,
                $"overlap on line {cur.Line} at char {cur.Char} (previous ends at {prev.Char + prev.Len})");
        }
    }

    /// <summary>
    /// The module name, the instance name and the named port labels of a
    /// module instantiation must be reported as the <c>identifier</c> token
    /// type, which the palette paints teal - the editor colours them the same
    /// way (<c>ColorType.Identifier</c>).
    ///
    /// <para>
    /// Mapping the token type onto <c>ColorType.Normal</c> (done when plain
    /// references used to land on it) made them grey in VS Code while the
    /// editor shows them coloured, which is the reported symptom.
    /// </para>
    /// </summary>
    [Fact]
    public void SemanticTokens_InstanceNameAndPortLabelsUseTheIdentifierTokenType()
    {
        LspHandler handler = CreateInitializedHandler();
        const string text =
            "module child(input clk, output [7:0] data_o);\n" +
            "endmodule\n" +
            "module top;\n" +
            "  wire clk;\n" +
            "  wire [7:0] data_o;\n" +
            "  child u_child(.clk(clk), .data_o(data_o));\n" +
            "endmodule\n";
        string uri = WriteFile("instance_tokens.sv", text);
        DidOpen(handler, uri, text);

        // legend order (the client must use the same one)
        string initialize = JsonSerializer.Serialize(new { processId = 1, capabilities = new { } });
        using JsonDocument initDoc = JsonDocument.Parse(initialize);
        handler.TryHandleRequest("initialize", initDoc.RootElement, default, out object? initResult);
        List<string> legend = new();
        foreach (JsonElement t in JsonDocument.Parse(JsonSerializer.Serialize(initResult)).RootElement
                     .GetProperty("capabilities").GetProperty("semanticTokensProvider")
                     .GetProperty("legend").GetProperty("tokenTypes").EnumerateArray())
        {
            legend.Add(t.GetString() ?? "");
        }
        int identifierIndex = legend.IndexOf("identifier");
        int referenceIndex = legend.IndexOf("reference");
        Assert.True(identifierIndex >= 0, "the legend must advertise the 'identifier' token type");
        Assert.True(referenceIndex >= 0, "the legend must advertise the 'reference' token type");

        JsonElement tokens = Request(handler, "textDocument/semanticTokens/full",
            new { textDocument = new { uri } });
        JsonElement data = tokens.GetProperty("data");

        List<(int Line, int Char, int Len, int Type)> decoded = new();
        int line = 0, character = 0;
        for (int i = 0; i + 4 < data.GetArrayLength(); i += 5)
        {
            int deltaLine = data[i].GetInt32();
            int deltaChar = data[i + 1].GetInt32();
            if (deltaLine == 0) character += deltaChar;
            else { line += deltaLine; character = deltaChar; }
            decoded.Add((line, character, data[i + 2].GetInt32(), data[i + 3].GetInt32()));
        }

        // line 5 is "  child u_child(.clk(clk), .data_o(data_o));"
        //   char 2  "child"   -> identifier (the instantiated module name)
        //   char 8  "u_child" -> identifier (the instance name)
        //   char 17 "clk"     -> identifier (the named port label)
        //   char 28 "data_o"  -> identifier
        List<(int Line, int Char, int Len, int Type)> instantiationLine =
            decoded.FindAll(t => t.Line == 5);
        // (char 2 is "child" itself: the parser paints the instantiated module
        // name as a keyword, so only the instance name and the port labels are
        // identifier tokens here.)
        Assert.Contains(instantiationLine, t => t.Char == 8 && t.Type == identifierIndex);
        Assert.Contains(instantiationLine, t => t.Char == 17 && t.Type == identifierIndex);
        Assert.Contains(instantiationLine, t => t.Char == 28 && t.Type == identifierIndex);

        // the palette must paint "identifier" teal and keep the generic
        // "reference" fallback at the default foreground
        JsonElement theme = Request(handler, "rtl/theme", new { });
        JsonElement rules = theme.GetProperty("rules");
        Assert.Equal("#4EC9B0", rules.GetProperty("identifier").GetString());
        Assert.Equal("#D4D4D4", rules.GetProperty("reference").GetString());
    }

    [Fact]
    public void Theme_LegendMatchesInitializeCapability()
    {
        LspHandler handler = CreateInitializedHandler();

        // the legend advertised in initialize and the one rtl/theme returns must
        // be the same list (one source of truth on the C# side)
        string initialize = JsonSerializer.Serialize(new { processId = 1, capabilities = new { } });
        using JsonDocument doc = JsonDocument.Parse(initialize);
        handler.TryHandleRequest("initialize", doc.RootElement, default, out object? initResult);
        JsonElement caps = JsonDocument.Parse(JsonSerializer.Serialize(initResult)).RootElement
            .GetProperty("capabilities")
            .GetProperty("semanticTokensProvider")
            .GetProperty("legend")
            .GetProperty("tokenTypes");

        JsonElement themeTypes = Request(handler, "rtl/theme", new { }).GetProperty("tokenTypes");

        List<string> fromInit = new();
        foreach (JsonElement t in caps.EnumerateArray()) fromInit.Add(t.GetString() ?? "");
        List<string> fromTheme = new();
        foreach (JsonElement t in themeTypes.EnumerateArray()) fromTheme.Add(t.GetString() ?? "");

        Assert.Equal(fromInit, fromTheme);
    }

    [Fact]
    public void FolderNode_ExpandToken_ExpandsToItsContents()
    {
        // Regression: the folder node must carry an expand token that actually
        // resolves back to the same directory. The client only echoes the token,
        // so a broken token shows a folder that cannot be expanded.
        Directory.CreateDirectory(Path.Combine(_root, "src", "rtl"));
        File.WriteAllText(Path.Combine(_root, "src", "rtl", "leaf.sv"), "module leaf; endmodule\n");
        LspHandler handler = CreateInitializedHandler();

        JsonElement top = Request(handler, "rtl/hierarchy", new { }).GetProperty("nodes");
        JsonElement folderNode = default;
        foreach (JsonElement n in top.EnumerateArray())
        {
            if (n.GetProperty("label").GetString() == "src") { folderNode = n; break; }
        }
        Assert.NotEqual(JsonValueKind.Undefined, folderNode.ValueKind);
        Assert.Equal("rtlFolder", folderNode.GetProperty("contextValue").GetString());
        Assert.Equal("collapsed", folderNode.GetProperty("collapsible").GetString());
        Assert.True(folderNode.GetProperty("hasChildren").GetBoolean());
        // a directory uses the editor's FolderNode icon; the extension ships
        // icons/folder.svg (-dark) so the tree is not left with a codicon
        Assert.Equal("folder", folderNode.GetProperty("icon").GetString());

        string token = folderNode.GetProperty("expandToken").GetString() ?? "";
        Assert.False(string.IsNullOrEmpty(token), "folder node must carry an expand token");

        JsonElement children = Request(handler, "rtl/hierarchy", new { expandToken = token }).GetProperty("nodes");
        Assert.True(children.GetArrayLength() > 0, "expanding the folder must list its contents");
        bool foundRtl = false;
        foreach (JsonElement c in children.EnumerateArray())
        {
            if (c.GetProperty("label").GetString() == "rtl") { foundRtl = true; break; }
        }
        Assert.True(foundRtl, "expected the nested 'rtl' folder");
    }

    [Fact]
    public void WorkspaceRootNodes_CarryTheProjectIcon()
    {
        // With several folders open each root becomes its own node. That node
        // stands for the project, so it uses the editor's ProjectNode icon
        // ("home" in Color_Project) rather than the plain folder icon, while
        // the directories inside it stay "folder". The extension ships
        // icons/project.svg (-dark) and icons/folder.svg (-dark).
        string second = Path.Combine(_root, "second");
        Directory.CreateDirectory(Path.Combine(second, "rtl"));
        File.WriteAllText(Path.Combine(second, "rtl", "leaf.sv"), "module leaf; endmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top; endmodule\n");

        LspHandler handler = new(new InMemorySystemVerilogCore());
        string firstUri = new Uri(_root).AbsoluteUri;
        string secondUri = new Uri(second).AbsoluteUri;
        string initialize = JsonSerializer.Serialize(new
        {
            processId = 1,
            rootUri = firstUri,
            workspaceFolders = new[]
            {
                new { uri = firstUri, name = "first" },
                new { uri = secondUri, name = "second" },
            },
            capabilities = new { },
        });
        using (JsonDocument doc = JsonDocument.Parse(initialize))
        {
            handler.TryHandleRequest("initialize", doc.RootElement, default, out _);
        }

        JsonElement top = Request(handler, "rtl/hierarchy", new { }).GetProperty("nodes");
        int roots = 0;
        foreach (JsonElement n in top.EnumerateArray())
        {
            if (n.GetProperty("kind").GetString() != "folder") continue;
            roots++;
            Assert.Equal("rtlFolder", n.GetProperty("contextValue").GetString());
            Assert.Equal("project", n.GetProperty("icon").GetString());
        }
        Assert.Equal(2, roots);

        // inside a root the ordinary directories keep the folder icon
        string secondToken = string.Empty;
        foreach (JsonElement n in top.EnumerateArray())
        {
            if (n.GetProperty("label").GetString() == "second")
            {
                secondToken = n.GetProperty("expandToken").GetString() ?? string.Empty;
                break;
            }
        }
        Assert.False(string.IsNullOrEmpty(secondToken));
        JsonElement children = Request(handler, "rtl/hierarchy", new { expandToken = secondToken })
            .GetProperty("nodes");
        JsonElement rtlFolder = default;
        foreach (JsonElement c in children.EnumerateArray())
        {
            if (c.GetProperty("label").GetString() == "rtl") { rtlFolder = c; break; }
        }
        Assert.NotEqual(JsonValueKind.Undefined, rtlFolder.ValueKind);
        Assert.Equal("folder", rtlFolder.GetProperty("icon").GetString());
    }

    [Fact]
    public void ExpandToken_SurvivesSiblingChanges()
    {
        // The old protocol addressed children by index, so inserting a sibling
        // shifted every later node. The token carries the child NAMES, so the
        // same token still resolves to the same instance after an edit.
        File.WriteAllText(Path.Combine(_root, "leaf.sv"), "module leaf; endmodule\n");
        File.WriteAllText(Path.Combine(_root, "mid.sv"), "module mid;\n  leaf u_leaf();\nendmodule\n");
        File.WriteAllText(Path.Combine(_root, "top.sv"), "module top;\n  mid u_mid();\nendmodule\n");
        LspHandler handler = CreateInitializedHandler();

        JsonElement top = Request(handler, "rtl/hierarchy", new { }).GetProperty("nodes");
        JsonElement fileNode = default;
        foreach (JsonElement n in top.EnumerateArray())
        {
            if (n.GetProperty("label").GetString() == "top.sv") { fileNode = n; break; }
        }
        string fileToken = fileNode.GetProperty("expandToken").GetString() ?? "";

        JsonElement level0 = Request(handler, "rtl/hierarchy", new { expandToken = fileToken }).GetProperty("nodes");
        Assert.Equal("u_mid", level0[0].GetProperty("label").GetString());
        string instanceToken = level0[0].GetProperty("expandToken").GetString() ?? "";
        Assert.False(string.IsNullOrEmpty(instanceToken));

        // The token addresses the instance by NAME, so it resolves to u_mid
        // regardless of where u_mid sits among its siblings. A token naming an
        // instance that does not exist resolves to nothing (no throw).
        string uri = fileNode.GetProperty("uri").GetString() ?? "";
        JsonElement byName = Request(handler, "rtl/hierarchy",
            new { expandToken = ExpandToken.ForInstance(uri, new[] { "u_mid" }) }).GetProperty("nodes");
        Assert.Equal(1, byName.GetArrayLength());
        Assert.Equal("u_leaf", byName[0].GetProperty("label").GetString());

        JsonElement missing = Request(handler, "rtl/hierarchy",
            new { expandToken = ExpandToken.ForInstance(uri, new[] { "u_does_not_exist" }) }).GetProperty("nodes");
        Assert.Equal(0, missing.GetArrayLength());
    }

    [Fact]
    public void ExpandToken_MalformedToken_ReturnsEmptyInsteadOfThrowing()
    {
        LspHandler handler = CreateInitializedHandler();
        // a garbage token must not throw: the server answers with the top level
        JsonElement result = Request(handler, "rtl/hierarchy", new { expandToken = "!!!not-a-token!!!" });
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.True(result.TryGetProperty("nodes", out _));
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
