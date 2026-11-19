using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SystemVerilogCore.Diagnostics;
using SystemVerilogLanguageServer.Server;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// A <c>.rule</c> file holds the severity of the parser rules as YAML
/// (name / severity pairs) and applies to every source file below the
/// directory that contains it. The rule set is looked up per file while
/// parsing, so the same diagnostic can be an Error in one directory and the
/// built-in Warning elsewhere.
/// </summary>
public class RuleSetTests : IDisposable
{
    private readonly string _root;

    public RuleSetTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "svlsp_rule_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
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

    private static void SendNotification(LspHandler handler, string method, object parameters)
    {
        string json = JsonSerializer.Serialize(parameters);
        using JsonDocument doc = JsonDocument.Parse(json);
        handler.TryHandleNotification(method, doc.RootElement, default);
    }

    /// <summary>
    /// The diagnostics pushed for <paramref name="uri"/> as (message, severity)
    /// pairs. LSP severity: 1 Error / 2 Warning / 3 Information / 4 Hint.
    /// </summary>
    private static System.Collections.Generic.List<(string Message, int Severity)> PushedDiagnostics(
        System.Collections.Generic.List<string> pushed, string uri)
    {
        System.Collections.Generic.List<(string, int)> result = new();
        string normalizedUri = uri.Replace("\\", "/", StringComparison.Ordinal);
        foreach (string json in pushed)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("params", out JsonElement prm)) continue;
            if (!prm.TryGetProperty("uri", out JsonElement pushedUri)) continue;
            if (!string.Equals(
                (pushedUri.GetString() ?? "").Replace("\\", "/", StringComparison.Ordinal),
                normalizedUri,
                StringComparison.OrdinalIgnoreCase)) continue;
            if (!prm.TryGetProperty("diagnostics", out JsonElement items)) continue;
            foreach (JsonElement it in items.EnumerateArray())
            {
                result.Add((
                    it.GetProperty("message").GetString() ?? "",
                    it.GetProperty("severity").GetInt32()));
            }
        }
        return result;
    }

    /// <summary>
    /// A <c>.rule</c> file that raises AssignmentBitwidthMismatch to Error must
    /// affect only the files below its directory: the same source outside that
    /// directory keeps the built-in Warning.
    /// </summary>
    [Fact]
    public void RuleFile_AppliesToTheFilesBelowItsDirectory()
    {
        const string source =
            "module m;\n" +
            "  wire [3:0] a;\n" +
            "  wire [7:0] b;\n" +
            "  assign a = b;\n" +
            "endmodule\n";

        string strictDirectory = Path.Combine(_root, "strict");
        Directory.CreateDirectory(strictDirectory);
        File.WriteAllText(Path.Combine(strictDirectory, ".rule.yaml"),
            "rules:\n" +
            "  - name: AssignmentBitwidthMismatch\n" +
            "    severity: Error\n");
        File.WriteAllText(Path.Combine(strictDirectory, "wide.sv"), source);
        File.WriteAllText(Path.Combine(_root, "wide.sv"), source);

        string strictUri = new Uri(Path.Combine(strictDirectory, "wide.sv")).AbsoluteUri;
        string rootUri = new Uri(Path.Combine(_root, "wide.sv")).AbsoluteUri;

        InMemorySystemVerilogCore core = new();
        LspHandler handler = CreateInitializedHandler(core);
        var pushed = new System.Collections.Generic.List<string>();
        handler.NotificationSender = message => pushed.Add(JsonSerializer.Serialize(message));

        foreach ((string uri, string path) in new[]
        {
            (strictUri, Path.Combine(strictDirectory, "wide.sv")),
            (rootUri, Path.Combine(_root, "wide.sv")),
        })
        {
            SendNotification(handler, "textDocument/didOpen", new
            {
                textDocument = new
                {
                    uri,
                    languageId = "systemverilog",
                    version = 1,
                    text = File.ReadAllText(path),
                },
            });
        }

        var strictDiagnostics = PushedDiagnostics(pushed, strictUri);
        var rootDiagnostics = PushedDiagnostics(pushed, rootUri);

        Assert.Contains(strictDiagnostics, d => d.Message.Contains("bit width mismatch 4 <- 8", StringComparison.Ordinal));
        Assert.Contains(rootDiagnostics, d => d.Message.Contains("bit width mismatch 4 <- 8", StringComparison.Ordinal));

        int strictSeverity = strictDiagnostics
            .First(d => d.Message.Contains("bit width mismatch 4 <- 8", StringComparison.Ordinal)).Severity;
        int rootSeverity = rootDiagnostics
            .First(d => d.Message.Contains("bit width mismatch 4 <- 8", StringComparison.Ordinal)).Severity;

        Assert.Equal(1, strictSeverity); // Error (raised by strict/.rule.yaml)
        Assert.Equal(2, rootSeverity);   // Warning (built-in default)
    }

    /// <summary>
    /// A <c>.rule</c> file may list a subset of the rules; the remaining rules
    /// keep their built-in severity. Unknown names and invalid severities are
    /// reported but do not prevent the valid entries from being applied.
    /// </summary>
    [Fact]
    public void ParseYaml_OverridesOnlyTheListedRules()
    {
        pluginVerilog.RuleSet? ruleSet = pluginVerilog.RuleSet.ParseYaml(
            "rules:\n" +
            "  - name: ImplicitNetDeclaration\n" +
            "    severity: Notice\n" +
            "  - name: NoSuchRule\n" +
            "    severity: Error\n" +
            "  - name: ImplicitModportDirection\n" +
            "    severity: Bogus\n",
            out string? errorMessage);

        Assert.NotNull(ruleSet);
        Assert.Equal(pluginVerilog.Rule.SeverityEnum.Notice,
            ruleSet!.ImplicitNetDeclaretion.Severity);
        // not listed -> built-in default
        Assert.Equal(pluginVerilog.Rule.SeverityEnum.Warning,
            ruleSet.AssignmentBitwidthMismatch.Severity);
        // invalid severity -> keeps the built-in default
        Assert.Equal(pluginVerilog.Rule.SeverityEnum.Warning,
            ruleSet.ImplicitModportDirection.Severity);

        Assert.NotNull(errorMessage);
        Assert.Contains("NoSuchRule", errorMessage!, StringComparison.Ordinal);
        Assert.Contains("Bogus", errorMessage!, StringComparison.Ordinal);
    }

    /// <summary>
    /// Writing a rule set produces a YAML file that loads back to the same
    /// severities (the file type's "new file" template and the round trip the
    /// editor uses).
    /// </summary>
    [Fact]
    public void SaveFile_RoundTrips()
    {
        string path = Path.Combine(_root, "roundtrip.rule.yaml");
        pluginVerilog.RuleSet original = new();
        original.SaveFile(path);

        Assert.True(File.Exists(path));
        pluginVerilog.RuleSet? loaded = pluginVerilog.RuleSet.LoadFile(path, out string? errorMessage);
        Assert.Null(errorMessage);
        Assert.NotNull(loaded);

        foreach (pluginVerilog.Rule rule in original.GetRules())
        {
            pluginVerilog.Rule? reloaded = loaded!.GetRule(rule.Name);
            Assert.NotNull(reloaded);
            Assert.Equal(rule.Severity, reloaded!.Severity);
        }
        // every rule of the set must be written (name / severity pairs)
        Assert.Equal(original.GetRules().Count, loaded!.GetRules().Count);
    }

    /// <summary>
    /// A directory without a <c>.rule</c> file gets the project-wide default,
    /// and a deeper <c>.rule</c> wins over the one above it.
    /// </summary>
    [Fact]
    public void GetRuleSet_DeepestDirectoryWins()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src", "rtl"));
        File.WriteAllText(Path.Combine(_root, "src", ".rule.yaml"),
            "rules:\n" +
            "  - name: AssignmentBitwidthMismatch\n" +
            "    severity: Error\n");
        File.WriteAllText(Path.Combine(_root, "src", "rtl", ".rule.yaml"),
            "rules:\n" +
            "  - name: AssignmentBitwidthMismatch\n" +
            "    severity: Notice\n");

        InMemorySystemVerilogCore core = new();
        CreateInitializedHandler(core);

        InMemoryProject project = core.GetOrCreateProject("default");
        pluginVerilog.ProjectProperty property =
            (pluginVerilog.ProjectProperty)project.PluginProject.ProjectProperties[pluginVerilog.Plugin.StaticID];

        Assert.Equal(pluginVerilog.Rule.SeverityEnum.Error,
            property.GetRuleSet("src/top.sv").AssignmentBitwidthMismatch.Severity);
        Assert.Equal(pluginVerilog.Rule.SeverityEnum.Notice,
            property.GetRuleSet("src/rtl/leaf.sv").AssignmentBitwidthMismatch.Severity);
        // outside src/ there is no .rule.yaml file -> built-in default
        Assert.Equal(pluginVerilog.Rule.SeverityEnum.Warning,
            property.GetRuleSet("top.sv").AssignmentBitwidthMismatch.Severity);
    }

    /// <summary>
    /// The older <c>.rule</c> spelling keeps working. A workspace that already
    /// has one must not silently lose its severities because the extension
    /// changed - that would look like the parser ignoring the file.
    /// </summary>
    [Fact]
    public void LegacyRuleExtension_IsStillApplied()
    {
        Directory.CreateDirectory(Path.Combine(_root, "legacy"));
        File.WriteAllText(Path.Combine(_root, "legacy", ".rule"),
            "rules:\n" +
            "  - name: AssignmentBitwidthMismatch\n" +
            "    severity: Error\n");

        InMemorySystemVerilogCore core = new();
        CreateInitializedHandler(core);

        InMemoryProject project = core.GetOrCreateProject("default");
        pluginVerilog.ProjectProperty property =
            (pluginVerilog.ProjectProperty)project.PluginProject.ProjectProperties[pluginVerilog.Plugin.StaticID];

        Assert.Equal(pluginVerilog.Rule.SeverityEnum.Error,
            property.GetRuleSet("legacy/leaf.sv").AssignmentBitwidthMismatch.Severity);
    }

    /// <summary>
    /// A rule file is recognised by either spelling, and nothing else is.
    /// </summary>
    [Fact]
    public void IsRuleFilePath_AcceptsBothSpellings()
    {
        Assert.True(pluginVerilog.ProjectProperty.IsRuleFilePath("a/.rule.yaml"));
        Assert.True(pluginVerilog.ProjectProperty.IsRuleFilePath("a/.rule"));
        Assert.True(pluginVerilog.ProjectProperty.IsRuleFilePath("A/.RULE.YAML"));

        Assert.False(pluginVerilog.ProjectProperty.IsRuleFilePath("a/top.sv"));
        Assert.False(pluginVerilog.ProjectProperty.IsRuleFilePath("a/rules.yaml"));
        Assert.False(pluginVerilog.ProjectProperty.IsRuleFilePath(""));
    }
}
