using SystemVerilogCore;
using SystemVerilogCore.Documents;
using SystemVerilogLanguageServer.Server;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// Verifies the cross-cutting behaviour of the in-memory SystemVerilogCore
/// adapters. The plugin-backed implementations live in
/// <c>CodeEditor2VerilogPlugin.CoreBridge</c> and cannot be referenced
/// directly from a UI-free test project, so these tests use the same
/// <see cref="InMemorySystemVerilogCore"/> that hosts the LSP to verify
/// that <i>any</i> <see cref="ISystemVerilogProject"/> implementation
/// behaves correctly when wired into the language server.
/// </summary>
public class AdapterBehaviorTests
{
    [Fact]
    public async Task Symbol_Range_ContainsIsHalfOpen()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = await core.GetProjectAsync("default");
        InMemoryProject typed = (InMemoryProject)project;
        InMemoryFile file = typed.AddOrUpdateFile("file:///x.sv", "/x.sv", "module m; endmodule", isSystemVerilog: true);
        file.AddSymbol(new RangeSymbol("m", SystemVerilogNamedElementKind.Module, new SystemVerilogRange(7, 1)));

        // The start index is inclusive.
        ISystemVerilogNamedElement? atStart = await project.FindDefinitionAsync(file, 7);
        Assert.NotNull(atStart);

        // The end index (start + length) is exclusive.
        ISystemVerilogNamedElement? atEnd = await project.FindDefinitionAsync(file, 8);
        Assert.Null(atEnd);

        // The index just before the start is not covered.
        ISystemVerilogNamedElement? atBefore = await project.FindDefinitionAsync(file, 6);
        Assert.Null(atBefore);
    }

    [Fact]
    public async Task MultipleSymbols_AtDifferentRanges()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = await core.GetProjectAsync("default");
        InMemoryProject typed = (InMemoryProject)project;
        InMemoryFile file = typed.AddOrUpdateFile("file:///x.sv", "/x.sv", "module a; module b; endmodule", isSystemVerilog: true);
        file.AddSymbol(new RangeSymbol("a", SystemVerilogNamedElementKind.Module, new SystemVerilogRange(7, 1)));
        file.AddSymbol(new RangeSymbol("b", SystemVerilogNamedElementKind.Module, new SystemVerilogRange(17, 1)));

        ISystemVerilogNamedElement? defA = await project.FindDefinitionAsync(file, 7);
        ISystemVerilogNamedElement? defB = await project.FindDefinitionAsync(file, 17);

        Assert.NotNull(defA);
        Assert.Equal("a", defA!.Name);
        Assert.NotNull(defB);
        Assert.Equal("b", defB!.Name);
    }

    [Fact]
    public async Task FileLookup_AbsolutePathAndRelative()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = await core.GetProjectAsync("default");
        InMemoryProject typed = (InMemoryProject)project;
        typed.AddOrUpdateFile("file:///abs/path.sv", "/abs/path.sv", "module m; endmodule", isSystemVerilog: true);

        // The LSP server keys files by their URI; the adapter surface
        // exposes AbsolutePath so consumers that work with disk paths can
        // resolve back to a file.
        ISystemVerilogFile? file = project.FindFile("file:///abs/path.sv");
        Assert.NotNull(file);
        Assert.Equal("/abs/path.sv", file!.AbsolutePath);
    }

    [Fact]
    public async Task Project_EnumeratesAllRegisteredFiles()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = await core.GetProjectAsync("default");
        InMemoryProject typed = (InMemoryProject)project;
        typed.AddOrUpdateFile("file:///a.sv", "/a.sv", "module a; endmodule", isSystemVerilog: true);
        typed.AddOrUpdateFile("file:///b.sv", "/b.sv", "module b; endmodule", isSystemVerilog: true);
        typed.AddOrUpdateFile("file:///c.sv", "/c.sv", "module c; endmodule", isSystemVerilog: true);

        Assert.Equal(3, project.Files.Count);

        // The Files collection is a snapshot. Re-adding the same file
        // should not grow the project.
        typed.AddOrUpdateFile("file:///a.sv", "/a.sv", "module a; endmodule", isSystemVerilog: true);
        Assert.Equal(3, project.Files.Count);
    }

    [Fact]
    public async Task HoverContent_PreservesKindFromRegistry()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = await core.GetProjectAsync("default");
        InMemoryProject typed = (InMemoryProject)project;
        InMemoryFile file = typed.AddOrUpdateFile("file:///foo.sv", "/foo.sv", "package pkg; endpackage", isSystemVerilog: true);
        RangeSymbol sym = new RangeSymbol(
            "pkg", SystemVerilogNamedElementKind.Package, new SystemVerilogRange(8, 3));
        file.AddSymbol(sym);

        string? content = HoverContent.Build(sym);
        Assert.NotNull(content);
        Assert.Contains("package pkg", content);
    }

    [Fact]
    public async Task Diagnostics_EmptyByDefault()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = await core.GetProjectAsync("default");
        InMemoryProject typed = (InMemoryProject)project;
        InMemoryFile file = typed.AddOrUpdateFile("file:///x.sv", "/x.sv", "module m; endmodule", isSystemVerilog: true);
        ISystemVerilogDocument? doc = await project.GetDocumentAsync(file);

        Assert.NotNull(doc);
        Assert.Empty(doc!.Diagnostics);
    }

    [Fact]
    public void DocumentSymbol_NestedBuildingBlocks_ProduceNestedTree()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        LspHandler handler = new LspHandler(core);
        handler.TryHandleNotification(
            "textDocument/didOpen",
            ParseJson(new { textDocument = new { uri = "file:///top.sv", text = "module top; module inner; endmodule endmodule" } }),
            default);

        ISystemVerilogProject project = core.GetOrCreateProjectPublic("default");
        InMemoryFile file = (InMemoryFile)project.FindFile("file:///top.sv")!;

        // Build a tiny hierarchy: top -> inner -> leaf variable.
        InMemoryBuildingBlock top = new InMemoryBuildingBlock(
            "top", SystemVerilogBuildingBlockKind.Module, new SystemVerilogRange(7, 3), file);
        InMemoryBuildingBlock inner = new InMemoryBuildingBlock(
            "inner", SystemVerilogBuildingBlockKind.Module, new SystemVerilogRange(20, 5), file);
        // Manually wire inner as a child of top.
        top.AddChild(inner);
        inner.AddMember(new RangeSymbol(
            "leaf", SystemVerilogNamedElementKind.Variable, new SystemVerilogRange(45, 4)));
        file.AddBuildingBlock(top);

        object? result = handler.TryHandleRequest(
            "textDocument/documentSymbol",
            ParseJson(new { textDocument = new { uri = "file:///top.sv" } }),
            default,
            out object? outResult) ? outResult : null;

        DocumentSymbol? root = Assert.Single(((System.Collections.IList)result!)[0] is DocumentSymbol r ? new[] { r } : System.Array.Empty<object>()) as DocumentSymbol;
        // The above cast is awkward; we re-fetch cleanly:
        Assert.NotNull(result);
        System.Collections.IList? list = result as System.Collections.IList;
        Assert.NotNull(list);
        DocumentSymbol? rootSymbol = list![0] as DocumentSymbol;
        Assert.NotNull(rootSymbol);
        Assert.NotNull(rootSymbol!.Children);
        DocumentSymbol? topSymbol = Assert.Single(rootSymbol.Children!);
        Assert.Equal("top", topSymbol.Name);
        Assert.NotNull(topSymbol.Children);
        DocumentSymbol? innerSymbol = Assert.Single(topSymbol.Children!);
        Assert.Equal("inner", innerSymbol.Name);
        Assert.NotNull(innerSymbol.Children);
        DocumentSymbol? leafSymbol = Assert.Single(innerSymbol.Children!);
        Assert.Equal("leaf", leafSymbol.Name);
        Assert.Equal(SymbolKind.Variable, leafSymbol.Kind);
    }

    private static System.Text.Json.JsonElement ParseJson(object value)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(value,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        return System.Text.Json.JsonDocument.Parse(json).RootElement;
    }

    /// <summary>
    /// Range-bearing <see cref="ISystemVerilogNamedElement"/> used by
    /// the tests. Mirrors the shape of the parser-backed adapters
    /// without depending on the plugin.
    /// </summary>
    private sealed class RangeSymbol : ISystemVerilogNamedElement
    {
        public RangeSymbol(string name, SystemVerilogNamedElementKind kind, SystemVerilogRange range)
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

    /// <summary>
    /// Building block with mutable children. Used to verify the
    /// documentSymbol provider recurses into nested blocks.
    /// </summary>
    private sealed class InMemoryBuildingBlock : ISystemVerilogBuildingBlock
    {
        private readonly Dictionary<string, ISystemVerilogBuildingBlock> _children = new();
        private readonly List<ISystemVerilogNamedElement> _members = new();

        public InMemoryBuildingBlock(
            string name,
            SystemVerilogBuildingBlockKind kind,
            SystemVerilogRange range,
            ISystemVerilogFile file)
        {
            Name = name;
            Kind = kind;
            DefinitionRange = range;
            File = file;
        }

        public string Name { get; }
        public SystemVerilogBuildingBlockKind Kind { get; }
        public SystemVerilogRange? DefinitionRange { get; }
        public ISystemVerilogFile? File { get; }
        public ISystemVerilogBuildingBlock? Owner => null;
        public IReadOnlyDictionary<string, ISystemVerilogBuildingBlock> BuildingBlocks => _children;
        public IReadOnlyList<ISystemVerilogNamedElement> Members => _members;

        public void AddChild(InMemoryBuildingBlock child) => _children[child.Name] = child;
        public void AddMember(ISystemVerilogNamedElement member) => _members.Add(member);

        SystemVerilogNamedElementKind ISystemVerilogNamedElement.Kind => SystemVerilogNamedElementKind.Unknown;
    }
}
