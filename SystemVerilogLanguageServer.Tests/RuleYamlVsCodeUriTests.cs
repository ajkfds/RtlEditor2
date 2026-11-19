using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SystemVerilogLanguageServer.Server;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// A <c>.rule.yaml</c> file must work in the VS Code host, not only in the
/// editor and in tests that build the URI with
/// <c>new Uri(path).AbsoluteUri</c>.
/// </summary>
/// <remarks>
/// <para>
/// VS Code does not send <c>file:///D:/dir/x.sv</c>; it sends
/// <c>file:///d%3A/dir/x.sv</c> (lowercase drive letter, percent-encoded
/// colon). This project has repeatedly broken on that difference - the server
/// resolved the path wrongly, scanned 0 files and every per-file lookup fell
/// back to a default - so the rule-file lookup is pinned against the real form.
/// </para>
/// <para>
/// The failure mode this guards is silent: the parse succeeds and the
/// diagnostics appear, but the severity is the built-in default instead of what
/// the <c>.rule.yaml</c> asked for. Nothing is logged.
/// </para>
/// </remarks>
public class RuleYamlVsCodeUriTests : IDisposable
{
    private readonly string _root;

    public RuleYamlVsCodeUriTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "svlsp_ruleuri_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* best effort */ }
    }

    /// <summary>The exact shape VS Code sends: <c>file:///c%3A/dir/x.sv</c>.</summary>
    private static string VsCodeUri(string absolutePath)
    {
        string p = absolutePath.Replace('\\', '/');
        if (p.Length > 1 && p[1] == ':')
        {
            string drive = char.ToLowerInvariant(p[0]).ToString();
            p = "/" + drive + "%3A" + p.Substring(2);
        }
        return "file://" + p;
    }

    [Fact]
    public void RuleYaml_Applies_WithVsCodeStyleUri()
    {
        const string source =
            "module m;\n" +
            "  wire [3:0] a;\n" +
            "  wire [7:0] b;\n" +
            "  assign a = b;\n" +
            "endmodule\n";

        string strictDir = Path.Combine(_root, "strict");
        Directory.CreateDirectory(strictDir);
        File.WriteAllText(Path.Combine(strictDir, ".rule.yaml"),
            "rules:\n" +
            "  - name: AssignmentBitwidthMismatch\n" +
            "    severity: Error\n");
        File.WriteAllText(Path.Combine(strictDir, "wide.sv"), source);

        string uri = VsCodeUri(Path.Combine(strictDir, "wide.sv"));
        Assert.Contains("%3A", uri);

        InMemorySystemVerilogCore core = new();
        LspHandler handler = new(core);
        string initialize = JsonSerializer.Serialize(new
        {
            processId = 1,
            rootUri = VsCodeUri(_root),
            capabilities = new { },
        });
        using (JsonDocument doc = JsonDocument.Parse(initialize))
        {
            handler.TryHandleRequest("initialize", doc.RootElement, default, out _);
        }

        List<string> pushed = new();
        handler.NotificationSender = m => pushed.Add(JsonSerializer.Serialize(m));

        string open = JsonSerializer.Serialize(new
        {
            textDocument = new { uri, languageId = "systemverilog", version = 1, text = source },
        });
        using (JsonDocument doc = JsonDocument.Parse(open))
        {
            handler.TryHandleNotification("textDocument/didOpen", doc.RootElement, default);
        }

        List<int> severities = new();
        foreach (string json in pushed)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("params", out JsonElement prm)) continue;
            if (!prm.TryGetProperty("diagnostics", out JsonElement items)) continue;
            foreach (JsonElement it in items.EnumerateArray())
            {
                if (!(it.GetProperty("message").GetString() ?? "")
                    .Contains("bit width mismatch", StringComparison.Ordinal)) continue;
                severities.Add(it.GetProperty("severity").GetInt32());
            }
        }

        Assert.True(severities.Count > 0,
            "no bit width mismatch diagnostic was pushed at all (uri=" + uri + ")");
        Assert.Equal(1, severities[0]); // Error => the .rule.yaml was applied
    }

    /// <summary>
    /// The legacy <c>.rule</c> spelling must work in the VS Code host too, so a
    /// workspace that predates the rename keeps its severities.
    /// </summary>
    [Fact]
    public void LegacyRule_Applies_WithVsCodeStyleUri()
    {
        const string source =
            "module m;\n" +
            "  wire [3:0] a;\n" +
            "  wire [7:0] b;\n" +
            "  assign a = b;\n" +
            "endmodule\n";

        string dir = Path.Combine(_root, "legacy");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".rule"),
            "rules:\n" +
            "  - name: AssignmentBitwidthMismatch\n" +
            "    severity: Error\n");
        File.WriteAllText(Path.Combine(dir, "wide.sv"), source);

        string uri = VsCodeUri(Path.Combine(dir, "wide.sv"));

        InMemorySystemVerilogCore core = new();
        LspHandler handler = new(core);
        string initialize = JsonSerializer.Serialize(new
        {
            processId = 1,
            rootUri = VsCodeUri(_root),
            capabilities = new { },
        });
        using (JsonDocument doc = JsonDocument.Parse(initialize))
        {
            handler.TryHandleRequest("initialize", doc.RootElement, default, out _);
        }

        List<string> pushed = new();
        handler.NotificationSender = m => pushed.Add(JsonSerializer.Serialize(m));

        string open = JsonSerializer.Serialize(new
        {
            textDocument = new { uri, languageId = "systemverilog", version = 1, text = source },
        });
        using (JsonDocument doc = JsonDocument.Parse(open))
        {
            handler.TryHandleNotification("textDocument/didOpen", doc.RootElement, default);
        }

        List<int> severities = new();
        foreach (string json in pushed)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("params", out JsonElement prm)) continue;
            if (!prm.TryGetProperty("diagnostics", out JsonElement items)) continue;
            foreach (JsonElement it in items.EnumerateArray())
            {
                if (!(it.GetProperty("message").GetString() ?? "")
                    .Contains("bit width mismatch", StringComparison.Ordinal)) continue;
                severities.Add(it.GetProperty("severity").GetInt32());
            }
        }

        Assert.True(severities.Count > 0, "no bit width mismatch diagnostic was pushed (uri=" + uri + ")");
        Assert.Equal(1, severities[0]);
    }
}
