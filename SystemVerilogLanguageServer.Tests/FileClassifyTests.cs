using System;
using System.IO;
using System.Text.Json;
using SystemVerilogLanguageServer.Server;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// <c>.fileClassify</c> lets a workspace override how a path is classified, so
/// e.g. <c>*.inc</c> can be parsed as a Verilog header. The editor honoured it
/// from the start; the LSP host did not, because it built its plugin items from
/// the file extension instead of asking the project.
/// </summary>
/// <remarks>
/// <para>
/// Two independent faults made it silently inert in VS Code:
/// </para>
/// <list type="number">
/// <item><c>FileClassify</c> resolved <c>.fileClassify</c> against
/// <c>Project.RootPath</c> when the project was constructed, which is before
/// <c>initialize</c> tells a headless host the root - so it looked in the wrong
/// directory and found nothing. Changing <c>RootPath</c> now re-reads it.</item>
/// <item><c>GetOrCreatePluginItem</c> chose the item type from the extension,
/// never calling <c>Project.GetFileType</c>, so the override was not consulted
/// even once the file had been read.</item>
/// </list>
/// <para>
/// Both failures are silent: the file parses, no diagnostic is emitted, and the
/// only symptom is that an <c>include</c> of it does not resolve - which reads
/// like a missing file.
/// </para>
/// </remarks>
public class FileClassifyTests : IDisposable
{
    private readonly string _root;

    public FileClassifyTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "svlsp_fc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* best effort */ }
    }

    private LspHandler CreateHandler(InMemorySystemVerilogCore core)
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

    private void DidOpen(LspHandler handler, string path, string languageId)
    {
        string uri = new Uri(path).AbsoluteUri;
        string open = JsonSerializer.Serialize(new
        {
            textDocument = new
            {
                uri,
                languageId,
                version = 1,
                text = File.ReadAllText(path),
            },
        });
        using JsonDocument doc = JsonDocument.Parse(open);
        handler.TryHandleNotification("textDocument/didOpen", doc.RootElement, default);
    }

    /// <summary>
    /// The syntax is <c>+ TypeName</c> to apply and <c>- TypeName</c> to remove,
    /// each followed by the wildcard it applies to.
    /// </summary>
    [Fact]
    public void FileClassify_OverridesTheFileTypeInTheLspHost()
    {
        File.WriteAllText(Path.Combine(_root, ".fileClassify"),
            "+ VerilogHeaderFile\n*.inc\n");

        string incPath = Path.Combine(_root, "defs.inc");
        File.WriteAllText(incPath, "`define WIDTH 8\n");

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateHandler(core);
        DidOpen(handler, incPath, "verilog");

        InMemoryProject project = core.GetOrCreateProject("default");

        // the override is loaded (it was empty before the RootPath fix)
        Assert.True(project.PluginProject.FileClassify.HasDefinition());
        Assert.Equal(
            pluginVerilog.FileTypes.VerilogHeaderFile.TypeID,
            project.PluginProject.GetFileType("defs.inc")?.ID);

        // and the item tree built from it is a header, not a plain Verilog file
        Assert.Equal(
            "VerilogHeaderFile",
            project.GetPluginItem("defs.inc")?.GetType().Name);
    }

    /// <summary>
    /// A path the override does not mention keeps its extension-based type, so
    /// adding a <c>.fileClassify</c> cannot change unrelated files.
    /// </summary>
    [Fact]
    public void FileClassify_LeavesUnmentionedPathsAlone()
    {
        File.WriteAllText(Path.Combine(_root, ".fileClassify"),
            "+ VerilogHeaderFile\n*.inc\n");

        string svPath = Path.Combine(_root, "top.sv");
        File.WriteAllText(svPath, "module top; endmodule\n");

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateHandler(core);
        DidOpen(handler, svPath, "systemverilog");

        InMemoryProject project = core.GetOrCreateProject("default");

        Assert.Equal(
            "VerilogFile",
            project.GetPluginItem("top.sv")?.GetType().Name);
    }

    /// <summary>
    /// No <c>.fileClassify</c> in the workspace: nothing is overridden and the
    /// extension decides, exactly as before.
    /// </summary>
    [Fact]
    public void WithoutFileClassify_TheExtensionDecides()
    {
        string incPath = Path.Combine(_root, "defs.inc");
        File.WriteAllText(incPath, "`define WIDTH 8\n");

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateHandler(core);
        DidOpen(handler, incPath, "verilog");

        InMemoryProject project = core.GetOrCreateProject("default");

        Assert.False(project.PluginProject.FileClassify.HasDefinition());
        Assert.Equal("VerilogFile", project.GetPluginItem("defs.inc")?.GetType().Name);
    }
}
