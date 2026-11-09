using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SystemVerilogLanguageServer.Server;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// `include resolution in the LSP host.
///
/// <para>
/// The plugin resolves an include target only through the plugin
/// <c>CodeEditor2.Data.Project</c> item tree (<c>WordScanner.parseInclude</c> ->
/// <c>Project.FindItems</c> / <c>Project.SearchFile</c>, and
/// <c>VerilogHeaderInstance.Create</c> -> <c>Project.GetItem</c>). The editor
/// builds that tree in <c>Folder.UpdateAsync</c>; the LSP host kept only its
/// own <see cref="InMemoryFile"/> table, so every include reported
/// "file not found" in VS Code even though the same project resolved includes
/// in RtlEditor.
/// </para>
/// </summary>
public class IncludeResolutionTests : IDisposable
{
    private readonly string _root;

    public IncludeResolutionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "svlsp_inc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "rtl"));
        Directory.CreateDirectory(Path.Combine(_root, "include"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* best effort */ }
    }

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

    private void DidOpen(LspHandler handler, string absolutePath, string text)
    {
        string uri = new Uri(absolutePath).AbsoluteUri;
        string parameters = JsonSerializer.Serialize(new
        {
            textDocument = new
            {
                uri,
                languageId = "systemverilog",
                version = 1,
                text,
            },
        });
        using JsonDocument doc = JsonDocument.Parse(parameters);
        handler.TryHandleNotification("textDocument/didOpen", doc.RootElement, default);
    }

    /// <summary>
    /// Diagnostics of the given file, as reported by the in-memory core.
    /// </summary>
    private static List<string> DiagnosticsOf(InMemorySystemVerilogCore core, string absolutePath)
    {
        InMemoryProject project = core.GetOrCreateProject("default");
        string uri = new Uri(absolutePath).AbsoluteUri;
        InMemoryFile file = (InMemoryFile)project.FindFile(uri)!;
        return file.Diagnostics.Select(d => d.Message).ToList();
    }

    /// <summary>
    /// A header in the same folder as the including file must resolve. This is
    /// the plain case the editor handles and the LSP host did not.
    /// </summary>
    [Fact]
    public void IncludeInSameFolder_Resolves()
    {
        string topPath = Path.Combine(_root, "rtl", "top.sv");
        string headerPath = Path.Combine(_root, "rtl", "defs.svh");
        File.WriteAllText(headerPath, "`define WIDTH 8\n");
        string topText =
            """
            `include "defs.svh"
            module top;
                logic [`WIDTH-1:0] data;
            endmodule
            """;
        File.WriteAllText(topPath, topText);

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        DidOpen(handler, topPath, topText);

        List<string> diagnostics = DiagnosticsOf(core, topPath);
        Assert.DoesNotContain(diagnostics, m => m.Contains("file not found", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A header in a sibling folder, included with a relative path, must
    /// resolve. The same-folder lookup cannot match here, so this exercises the
    /// project-wide fallback (which needs the item tree as well).
    /// </summary>
    [Fact]
    public void IncludeWithRelativePath_Resolves()
    {
        string topPath = Path.Combine(_root, "rtl", "top.sv");
        string headerPath = Path.Combine(_root, "include", "defs.svh");
        File.WriteAllText(headerPath, "`define WIDTH 8\n");
        string topText =
            """
            `include "../include/defs.svh"
            module top;
                logic [`WIDTH-1:0] data;
            endmodule
            """;
        File.WriteAllText(topPath, topText);

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        DidOpen(handler, topPath, topText);

        List<string> diagnostics = DiagnosticsOf(core, topPath);
        Assert.DoesNotContain(diagnostics, m => m.Contains("file not found", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// An include written INSIDE a module body must resolve too.
    ///
    /// <para>
    /// A file-scope include is parsed only by the real pass, but an include
    /// inside a design element is also seen by the prototype pass, which
    /// registers a <c>VerilogHeaderInstance</c> for that position. The real
    /// pass then found that instance and read
    /// <c>VerilogHeaderInstance.VerilogParsedDocument</c>, which resolves
    /// through the SOURCE file's instanced-parse table
    /// (<c>VerilogFile.GetInstancedParsedDocument</c>) - a table
    /// <c>addIncludeFile</c> never fills (it only assigns the instance's own
    /// <c>_parsedDocument</c>). The document was therefore always null, the
    /// method returned without consuming the file-name token, and the module
    /// reported "illegal module item" for the include line.
    /// </para>
    /// </summary>
    [Fact]
    public void IncludeInsideModuleBody_Resolves()
    {
        string topPath = Path.Combine(_root, "rtl", "top.sv");
        string headerPath = Path.Combine(_root, "rtl", "defs.svh");
        File.WriteAllText(headerPath, "`define WIDTH 8\n");
        string topText =
            """
            module top;
            `include "defs.svh"
                logic [`WIDTH-1:0] data;
            endmodule
            """;
        File.WriteAllText(topPath, topText);

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        DidOpen(handler, topPath, topText);

        List<string> diagnostics = DiagnosticsOf(core, topPath);
        Assert.DoesNotContain(diagnostics, m => m.Contains("illegal module item", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(diagnostics, m => m.Contains("file not found", StringComparison.OrdinalIgnoreCase));
        // the macro from the header must have been expanded (no "unsupported macro call")
        Assert.DoesNotContain(diagnostics, m => m.Contains("unsupported macro call", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A header that does not exist must still be reported: the fix must not
    /// silence the diagnostic by resolving anything.
    /// </summary>
    [Fact]
    public void MissingInclude_StillReportsFileNotFound()
    {
        string topPath = Path.Combine(_root, "rtl", "top.sv");
        string topText =
            """
            `include "does_not_exist.svh"
            module top;
            endmodule
            """;
        File.WriteAllText(topPath, topText);

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        DidOpen(handler, topPath, topText);

        List<string> diagnostics = DiagnosticsOf(core, topPath);
        Assert.Contains(diagnostics, m => m.Contains("file not found", StringComparison.OrdinalIgnoreCase));
    }
}
