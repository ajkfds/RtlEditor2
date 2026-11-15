using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// The VS Code extension registers its semantic-token provider with a
/// placeholder legend during <c>activate</c>, before the LSP handshake has
/// answered. VS Code freezes the legend at registration time, so the
/// placeholder must list the token types in exactly the server's order.
/// </summary>
/// <remarks>
/// <para>
/// This is not cosmetic: the extension decodes the server's token stream with
/// that legend, so a missing entry shifts every later token type by one. The
/// array was missing <c>reference</c>, which made the server's index 12
/// (<c>reference</c>, the lightweight scanner's generic fallback) render with
/// the <c>annotation</c> colour while the real annotation tokens (index 13)
/// fell outside the legend and lost their colour - the reported symptom.
/// </para>
/// <para>
/// The test reads the literal out of <c>RtlEditorVscodeExtension/extension.js</c>
/// so the two lists cannot drift again without a failing test.
/// </para>
/// </remarks>
public class ExtensionLegendTests
{
    [Fact]
    public void ExtensionPlaceholderLegend_MatchesTheServerLegend()
    {
        string[] serverLegend = SystemVerilogLanguageServer.Server.LspHandler.SemanticTokenTypes;
        Assert.True(serverLegend.Length > 0);

        string[] extensionLegend = ReadExtensionLegend();
        Assert.Equal(serverLegend, extensionLegend);
    }

    /// <summary>
    /// Walks up from the test binary directory until the extension folder
    /// (containing <c>extension.js</c> and <c>package.json</c>) is found.
    /// </summary>
    private static string FindExtensionRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "RtlEditorVscodeExtension");
            if (File.Exists(Path.Combine(candidate, "extension.js"))
                && File.Exists(Path.Combine(candidate, "package.json")))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("RtlEditorVscodeExtension folder not found from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Extracts the <c>tokenTypesByIndex</c> literal from extension.js.
    /// </summary>
    private static string[] ReadExtensionLegend()
    {
        string path = Path.Combine(FindExtensionRoot(), "extension.js");
        string text = File.ReadAllText(path);

        // let tokenTypesByIndex = ['keyword', 'comment', ...];
        Match match = Regex.Match(
            text,
            @"let\s+tokenTypesByIndex\s*=\s*\[(?<items>[^\]]*)\]",
            RegexOptions.Singleline);
        Assert.True(match.Success, "could not find the tokenTypesByIndex literal in extension.js");

        List<string> items = new();
        foreach (Match item in Regex.Matches(match.Groups["items"].Value, @"'([^']*)'"))
        {
            items.Add(item.Groups[1].Value);
        }
        return items.ToArray();
    }
}
