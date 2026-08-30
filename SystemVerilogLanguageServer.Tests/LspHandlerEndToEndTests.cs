using System.Text;
using System.Text.Json;
using SystemVerilogCore;
using SystemVerilogCore.Documents;
using SystemVerilogLanguageServer.Server;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// End-to-end tests that drive <see cref="LspHandler"/> through the same
/// JSON message shapes the language client uses. The tests use the
/// in-memory core; the parser-backed adapter is covered separately by
/// the editor's own test suite.
/// </summary>
public class LspHandlerEndToEndTests
{
    [Fact]
    public void DidOpen_RegistersFile()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        LspHandler handler = new LspHandler(core);
        string didOpen = SerializeParams(new
        {
            textDocument = new
            {
                uri = "file:///test.sv",
                languageId = "systemverilog",
                version = 1,
                text = "module foo; endmodule"
            }
        });
        using JsonDocument doc = JsonDocument.Parse(didOpen);
        bool handled = handler.TryHandleNotification("textDocument/didOpen", doc.RootElement, default);

        Assert.True(handled);
        ISystemVerilogProject project = core.GetOrCreateProjectPublic("default");
        ISystemVerilogFile? file = project.FindFile("file:///test.sv");
        Assert.NotNull(file);
        Assert.Equal("module foo; endmodule", file!.CodeDocument.GetText());
    }

    [Fact]
    public void DidOpen_ThenDidChange_UpdatesText()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        LspHandler handler = new LspHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = "file:///test.sv", text = "old" }
        });

        SendNotification(handler, "textDocument/didChange", new
        {
            textDocument = new { uri = "file:///test.sv", version = 2 },
            contentChanges = new[] { new { text = "new content" } }
        });

        ISystemVerilogProject project = core.GetOrCreateProjectPublic("default");
        ISystemVerilogFile file = project.FindFile("file:///test.sv")!;
        Assert.Equal("new content", file.CodeDocument.GetText());
    }

    [Fact]
    public void DidClose_RemovesFile()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        LspHandler handler = new LspHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = "file:///test.sv", text = "module m; endmodule" }
        });
        Assert.NotNull(core.GetOrCreateProjectPublic("default").FindFile("file:///test.sv"));

        SendNotification(handler, "textDocument/didClose", new
        {
            textDocument = new { uri = "file:///test.sv" }
        });
        Assert.Null(core.GetOrCreateProjectPublic("default").FindFile("file:///test.sv"));
    }

    [Fact]
    public void Definition_AfterDidOpen_ReturnsRegisteredSymbol()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        LspHandler handler = new LspHandler(core);
        // Pre-register a symbol at offset 7 (the 'm' in 'module bar').
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = "file:///foo.sv", text = "module bar; endmodule" }
        });
        ISystemVerilogProject project = core.GetOrCreateProjectPublic("default");
        InMemoryFile file = (InMemoryFile)project.FindFile("file:///foo.sv")!;
        file.AddSymbol(new DefinitionTestSymbol("bar", SystemVerilogNamedElementKind.Module, new SystemVerilogRange(7, 3)));

        // Build a textDocument/definition request.
        object? result = SendRequest(handler, "textDocument/definition", new
        {
            textDocument = new { uri = "file:///foo.sv" },
            position = new { line = 0, character = 7 }
        });

        Assert.NotNull(result);
        Location[]? locations = result as Location[];
        Assert.NotNull(locations);
        Assert.Single(locations!);
        Assert.Equal("file:///foo.sv", locations![0].Uri);
        Assert.Equal(0, locations[0].Range.Start.Line);
        Assert.Equal(7, locations[0].Range.Start.Character);
        Assert.Equal(0, locations[0].Range.End.Line);
        Assert.Equal(10, locations[0].Range.End.Character);
    }

    [Fact]
    public void DocumentSymbol_ReturnsRootNamespace()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        LspHandler handler = new LspHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = "file:///foo.sv", text = "module bar; endmodule" }
        });

        object? result = SendRequest(handler, "textDocument/documentSymbol", new
        {
            textDocument = new { uri = "file:///foo.sv" }
        });

        Assert.NotNull(result);
        DocumentSymbol[]? symbols = result as DocumentSymbol[];
        Assert.NotNull(symbols);
        Assert.Single(symbols!);
        Assert.Equal("$root", symbols![0].Name);
    }

    [Fact]
    public void Hover_ReturnsMarkdownWithDefinition()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        LspHandler handler = new LspHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = "file:///foo.sv", text = "module bar; endmodule" }
        });
        ISystemVerilogProject project = core.GetOrCreateProjectPublic("default");
        InMemoryFile file = (InMemoryFile)project.FindFile("file:///foo.sv")!;
        file.AddSymbol(new DefinitionTestSymbol("bar", SystemVerilogNamedElementKind.Module, new SystemVerilogRange(7, 3)));

        object? result = SendRequest(handler, "textDocument/hover", new
        {
            textDocument = new { uri = "file:///foo.sv" },
            position = new { line = 0, character = 7 }
        });

        Hover? hover = result as Hover;
        Assert.NotNull(hover);
        Assert.Equal("markdown", hover!.Contents.Kind);
        Assert.Contains("module bar", hover.Contents.Value);
    }

    [Fact]
    public void Hover_OffSymbol_ReturnsNull()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        LspHandler handler = new LspHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = "file:///foo.sv", text = "module bar; endmodule" }
        });

        object? result = SendRequest(handler, "textDocument/hover", new
        {
            textDocument = new { uri = "file:///foo.sv" },
            position = new { line = 0, character = 0 }
        });

        Assert.Null(result);
    }

    [Fact]
    public void MultipleFiles_ProjectEnumerateAll()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        LspHandler handler = new LspHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = "file:///a.sv", text = "// a" }
        });
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = "file:///b.sv", text = "// b" }
        });
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = "file:///c.sv", text = "// c" }
        });

        ISystemVerilogProject project = core.GetOrCreateProjectPublic("default");
        Assert.Equal(3, project.Files.Count);
    }

    [Fact]
    public void Reopen_OverwritesText()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        LspHandler handler = new LspHandler(core);
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = "file:///a.sv", text = "old" }
        });
        SendNotification(handler, "textDocument/didOpen", new
        {
            textDocument = new { uri = "file:///a.sv", text = "new" }
        });

        ISystemVerilogProject project = core.GetOrCreateProjectPublic("default");
        ISystemVerilogFile file = project.FindFile("file:///a.sv")!;
        Assert.Equal("new", file.CodeDocument.GetText());
    }

    [Fact]
    public void SymbolKind_SerializesCorrectly()
    {
        // Cross-check that the JSON shape we expect from the LSP client
        // matches what the server emits. The numbers are defined by the
        // LSP spec.
        Assert.Equal(2, (int)SymbolKind.Module);
        Assert.Equal(3, (int)SymbolKind.Namespace);
        Assert.Equal(7, (int)SymbolKind.Property);
    }

    /// <summary>
    /// Sends an LSP notification (no response) by JSON-round-tripping
    /// the parameter shape the client would send.
    /// </summary>
    private static void SendNotification(LspHandler handler, string method, object parameters)
    {
        string json = SerializeParams(parameters);
        using JsonDocument doc = JsonDocument.Parse(json);
        bool handled = handler.TryHandleNotification(method, doc.RootElement, default);
        Assert.True(handled, $"notification {method} was not handled");
    }

    /// <summary>
    /// Sends an LSP request and returns the result the handler produced.
    /// </summary>
    private static object? SendRequest(LspHandler handler, string method, object parameters)
    {
        string json = SerializeParams(parameters);
        using JsonDocument doc = JsonDocument.Parse(json);
        bool handled = handler.TryHandleRequest(method, doc.RootElement, default, out object? result);
        Assert.True(handled, $"request {method} was not handled");
        return result;
    }

    private static string SerializeParams(object value)
    {
        return JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private sealed class DefinitionTestSymbol : ISystemVerilogNamedElement
    {
        public DefinitionTestSymbol(string name, SystemVerilogNamedElementKind kind, SystemVerilogRange range)
        {
            Name = name;
            Kind = kind;
            DefinitionRange = range;
        }

        public string Name { get; }
        public SystemVerilogNamedElementKind Kind { get; }
        public SystemVerilogRange? DefinitionRange { get; }
        public ISystemVerilogBuildingBlock? Owner => null;
        public ISystemVerilogFile? File => null;
    }
}
