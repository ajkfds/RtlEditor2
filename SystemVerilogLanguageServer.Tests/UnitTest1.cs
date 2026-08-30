using System.Text.Json;
using SystemVerilogCore;
using SystemVerilogCore.Documents;
using SystemVerilogCore.Diagnostics;
using SystemVerilogLanguageServer.Server;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// Verifies that the in-memory SystemVerilogCore and the LSP dispatcher
/// cooperate correctly for the simple cases that the language server
/// supports out of the box. These tests do not exercise the parser-backed
/// adapter; that one lives in the CodeEditor2VerilogPlugin submodule and is
/// covered by the editor's own test suite.
/// </summary>
public class InMemorySystemVerilogCoreTests
{
    [Fact]
    public async Task GetProjectAsync_ReturnsSameInstance()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject first = await core.GetProjectAsync("default");
        ISystemVerilogProject second = await core.GetProjectAsync("default");
        Assert.Same(first, second);
    }

    [Fact]
    public async Task FindDefinition_ReturnsSymbolAtIndex()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = await core.GetProjectAsync("default");
        InMemoryProject typed = (InMemoryProject)project;
        InMemoryFile file = typed.AddOrUpdateFile("file:///test.sv", "/test.sv", "module m; endmodule", isSystemVerilog: true);
        InMemorySymbol module = new InMemorySymbol("m", SystemVerilogNamedElementKind.Module, new SystemVerilogRange(7, 1));
        file.AddSymbol(module);

        ISystemVerilogNamedElement? def = await project.FindDefinitionAsync(file, 7);
        Assert.NotNull(def);
        Assert.Equal("m", def!.Name);
        Assert.Equal(SystemVerilogNamedElementKind.Module, def.Kind);
    }

    [Fact]
    public async Task FindDefinition_OffSymbol_ReturnsNull()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = await core.GetProjectAsync("default");
        InMemoryProject typed = (InMemoryProject)project;
        InMemoryFile file = typed.AddOrUpdateFile("file:///test.sv", "/test.sv", "module m; endmodule", isSystemVerilog: true);
        InMemorySymbol module = new InMemorySymbol("m", SystemVerilogNamedElementKind.Module, new SystemVerilogRange(7, 1));
        file.AddSymbol(module);

        // index 0 is the 'm' of 'module', which is not the declaration token.
        ISystemVerilogNamedElement? def = await project.FindDefinitionAsync(file, 0);
        Assert.Null(def);
    }

    [Fact]
    public async Task FindReferences_ReturnsDeclarationOnly()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = await core.GetProjectAsync("default");
        InMemoryProject typed = (InMemoryProject)project;
        InMemoryFile file = typed.AddOrUpdateFile("file:///test.sv", "/test.sv", "module m; endmodule", isSystemVerilog: true);
        InMemorySymbol module = new InMemorySymbol("m", SystemVerilogNamedElementKind.Module, new SystemVerilogRange(7, 1));
        file.AddSymbol(module);

        IReadOnlyList<ISystemVerilogNamedElement> refs = await project.FindReferencesAsync(file, 7);
        Assert.Single(refs);
        Assert.Equal("m", refs[0].Name);
    }

    [Fact]
    public void HoverContent_BuildsModuleSignature()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = core.GetOrCreateProjectPublic("default");
        InMemoryProject typed = (InMemoryProject)project;
        InMemoryFile file = typed.AddOrUpdateFile("file:///foo.sv", "/foo.sv", "module bar; endmodule", isSystemVerilog: true);
        InMemorySymbolWithFile symbol = new InMemorySymbolWithFile(
            "bar", SystemVerilogNamedElementKind.Module, new SystemVerilogRange(7, 3), file);

        string? content = HoverContent.Build(symbol);
        Assert.NotNull(content);
        Assert.Contains("```systemverilog", content);
        Assert.Contains("module bar", content);
        Assert.Contains("_Defined in:", content);
        Assert.Contains("/foo.sv", content);
    }

    [Fact]
    public void HoverContent_BuildsParameterSignature()
    {
        InMemorySymbol param = new InMemorySymbol("WIDTH", SystemVerilogNamedElementKind.Parameter, new SystemVerilogRange(10, 5));
        string? content = HoverContent.Build(param);
        Assert.NotNull(content);
        Assert.Contains("parameter WIDTH", content);
    }

    [Fact]
    public void HoverContent_EmptyElement_ReturnsNull()
    {
        InMemorySymbol anon = new InMemorySymbol("", SystemVerilogNamedElementKind.Unknown, new SystemVerilogRange(0, 0));
        Assert.Null(HoverContent.Build(anon));
    }

    [Fact]
    public void HoverContent_CustomProvider_TakesPrecedence()
    {
        try
        {
            HoverContent.RegisterProvider(new TestHoverProvider());
            InMemorySymbol param = new InMemorySymbol("WIDTH", SystemVerilogNamedElementKind.Parameter, new SystemVerilogRange(0, 5));
            string? content = HoverContent.Build(param);
            Assert.NotNull(content);
            Assert.Equal("CUSTOM", content);
        }
        finally
        {
            HoverContent.RegisterProvider(null);
        }
    }

    [Fact]
    public async Task GetDocument_ReturnsEmptyDocument()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = await core.GetProjectAsync("default");
        InMemoryProject typed = (InMemoryProject)project;
        InMemoryFile file = typed.AddOrUpdateFile("file:///test.sv", "/test.sv", "module m; endmodule", isSystemVerilog: true);

        ISystemVerilogDocument? doc = await project.GetDocumentAsync(file);
        Assert.NotNull(doc);
        Assert.Empty(doc!.Diagnostics);
    }

    [Fact]
    public async Task FindFile_ReturnsRegisteredFile()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = await core.GetProjectAsync("default");
        InMemoryProject typed = (InMemoryProject)project;
        InMemoryFile file = typed.AddOrUpdateFile("file:///test.sv", "/test.sv", "module m; endmodule", isSystemVerilog: true);

        ISystemVerilogFile? found = project.FindFile("file:///test.sv");
        Assert.Same(file, found);
    }

    [Fact]
    public void LspHandler_InitializeReturnsCapabilities()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        LspHandler handler = new LspHandler(core);
        using JsonDocument doc = JsonDocument.Parse("{}");
        bool handled = handler.TryHandleRequest("initialize", doc.RootElement, default, out object? result);
        Assert.True(handled);
        Assert.NotNull(result);
    }

    [Fact]
    public void LspHandler_UnknownMethod_ReturnsFalse()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        LspHandler handler = new LspHandler(core);
        using JsonDocument doc = JsonDocument.Parse("{}");
        bool handled = handler.TryHandleRequest("nonsense/method", doc.RootElement, default, out object? result);
        Assert.False(handled);
        Assert.Null(result);
    }

    [Fact]
    public async Task CodeDocument_Length_MatchesText()
    {
        InMemorySystemVerilogCore core = new InMemorySystemVerilogCore();
        ISystemVerilogProject project = await core.GetProjectAsync("default");
        InMemoryProject typed = (InMemoryProject)project;
        string text = "abc\ndef\nghi";
        InMemoryFile file = typed.AddOrUpdateFile("file:///test.sv", "/test.sv", text, isSystemVerilog: true);

        Assert.Equal(text.Length, file.CodeDocument.Length);
        Assert.Equal(3, file.CodeDocument.LineCount);
        Assert.Equal("abc", file.CodeDocument.GetLineText(0));
        Assert.Equal("def", file.CodeDocument.GetLineText(1));
        Assert.Equal("ghi", file.CodeDocument.GetLineText(2));
    }

    /// <summary>
    /// Minimal symbol implementation used by the in-memory core tests.
    /// Mirrors the shape of the parser-backed adapters without depending on
    /// the plugin or Avalonia.
    /// </summary>
    private sealed class InMemorySymbol : ISystemVerilogNamedElement
    {
        public InMemorySymbol(string name, SystemVerilogNamedElementKind kind, SystemVerilogRange range)
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
    /// Variant of <see cref="InMemorySymbol"/> that exposes a backing
    /// <see cref="ISystemVerilogFile"/>, used to verify the hover helper
    /// includes the declared file path.
    /// </summary>
    private sealed class InMemorySymbolWithFile : ISystemVerilogNamedElement
    {
        public InMemorySymbolWithFile(
            string name,
            SystemVerilogNamedElementKind kind,
            SystemVerilogRange range,
            ISystemVerilogFile file)
        {
            Name = name;
            Kind = kind;
            DefinitionRange = range;
            File = file;
        }

        public string Name { get; }
        public SystemVerilogNamedElementKind Kind { get; }
        public SystemVerilogRange? DefinitionRange { get; }
        public ISystemVerilogBuildingBlock? Owner => null;
        public ISystemVerilogFile? File { get; }
    }

    /// <summary>
    /// Verifies that the custom provider extension point is honoured.
    /// </summary>
    private sealed class TestHoverProvider : IHoverContentProvider
    {
        public string? Build(ISystemVerilogNamedElement element) => "CUSTOM";
    }
}
