using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// The extension's hover grammar must be exactly what
/// <see cref="pluginVerilog.CoreBridge.HoverGrammar"/> generates, must be
/// contributed by the manifest, and must use the parser's palette.
///
/// <para>
/// A hover cannot be coloured any other way (VS Code's markdown sanitizer keeps
/// no <c>style</c> / <c>class</c> attribute), so the grammar <em>is</em> the
/// hover's colour. A stale copy would silently paint hover code blocks in an
/// old palette, and a grammar contributed for the editor's own language would
/// replace the semantic-token colouring instead. Both failure modes are
/// invisible in C#, so they are checked mechanically here.
/// </para>
/// </summary>
public class HoverGrammarTests
{
    /// <summary>The committed grammar must match the generator.</summary>
    [Fact]
    public void CommittedGrammar_IsUpToDate()
    {
        string root = FindExtensionRoot();

        pluginVerilog.CoreBridge.HoverGrammarResult result =
            pluginVerilog.CoreBridge.HoverGrammar.Write(root, write: false);

        Assert.False(result.Failed,
            result.Message + ". Run: dotnet SystemVerilogLanguageServer.dll " +
            "--write-hover-grammar " + root);
    }

    /// <summary>
    /// The manifest must contribute the grammar for a dedicated language id,
    /// and must not inject it into another scope.
    /// </summary>
    /// <remarks>
    /// Injecting into <c>source.systemverilog</c> (or contributing a grammar
    /// for the <c>systemverilog</c> language) would take syntax highlighting
    /// away from the semantic tokens, which carry the real parser's colours -
    /// the exact regression recorded in memory/state.md
    /// ("カラーリングが pluginVerilog のものではなく textmate での
    /// カラーリングになっている"). A separate id keeps TextMate confined to
    /// hover code blocks.
    /// </remarks>
    [Fact]
    public void Manifest_ContributesTheGrammarForADedicatedLanguage()
    {
        string root = FindExtensionRoot();
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "package.json")));
        JsonElement contributes = manifest.RootElement.GetProperty("contributes");

        string expectedPath = "syntaxes/" + pluginVerilog.CoreBridge.HoverGrammar.FileName;
        string? grammarLanguage = null;
        bool found = false;
        foreach (JsonElement grammar in contributes.GetProperty("grammars").EnumerateArray())
        {
            string path = (grammar.GetProperty("path").GetString() ?? "")
                .Replace("./", "").Replace('\\', '/');
            if (path != expectedPath) continue;

            found = true;
            grammarLanguage = grammar.GetProperty("language").GetString();
            Assert.Equal(pluginVerilog.CoreBridge.HoverGrammar.ScopeName,
                grammar.GetProperty("scopeName").GetString());
            Assert.False(grammar.TryGetProperty("injectTo", out _),
                "the hover grammar must not be injected into another scope");
        }

        Assert.True(found, "package.json does not contribute " + expectedPath);
        Assert.NotNull(grammarLanguage);
        Assert.NotEqual("systemverilog", grammarLanguage);
        Assert.NotEqual("verilog", grammarLanguage);

        // The fence language needs a language contribution, otherwise VS Code
        // has no grammar to tokenise the hover block with.
        bool declared = false;
        foreach (JsonElement language in contributes.GetProperty("languages").EnumerateArray())
        {
            if (language.TryGetProperty("id", out JsonElement id) &&
                id.GetString() == grammarLanguage)
            {
                declared = true;
            }
        }
        Assert.True(declared,
            "contributes.languages does not declare '" + grammarLanguage + "'");
    }

    /// <summary>
    /// Every colour in the grammar must be a palette value, so the hover and the
    /// editor cannot drift apart.
    /// </summary>
    [Fact]
    public void Grammar_ColoursComeFromTheParserPalette()
    {
        HashSet<string> palette = new(
            pluginVerilog.CoreBridge.ElementLabelAdapter.GetPalette().Values,
            StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(palette);

        using JsonDocument grammar = JsonDocument.Parse(
            pluginVerilog.CoreBridge.HoverGrammar.Build());

        int rules = 0;
        foreach (JsonElement setting in grammar.RootElement.GetProperty("settings").EnumerateArray())
        {
            string scope = setting.GetProperty("scope").GetString() ?? "";
            string foreground = setting.GetProperty("settings").GetProperty("foreground").GetString() ?? "";
            Assert.Matches("^#[0-9A-Fa-f]{6}$", foreground);
            Assert.True(palette.Contains(foreground),
                $"'{scope}' uses {foreground}, which is not in the parser palette");
            rules++;
        }
        Assert.True(rules > 0);
    }

    /// <summary>
    /// Every scope the grammar emits must have a colour rule: a scope without
    /// one silently inherits the default foreground, which hides a mistake.
    /// </summary>
    [Fact]
    public void Grammar_ColoursEveryScopeItEmits()
    {
        using JsonDocument grammar = JsonDocument.Parse(
            pluginVerilog.CoreBridge.HoverGrammar.Build());

        HashSet<string> coloured = new(StringComparer.Ordinal);
        foreach (JsonElement setting in grammar.RootElement.GetProperty("settings").EnumerateArray())
        {
            coloured.Add(setting.GetProperty("scope").GetString() ?? "");
        }

        HashSet<string> emitted = new(StringComparer.Ordinal);
        CollectScopes(grammar.RootElement.GetProperty("repository"), emitted);
        Assert.NotEmpty(emitted);

        foreach (string scope in emitted)
        {
            Assert.True(coloured.Contains(scope), "no colour rule for scope '" + scope + "'");
        }
    }

    /// <summary>
    /// All regexes must compile: VS Code drops a rule whose pattern is invalid,
    /// and the hover would then silently lose that colour.
    /// </summary>
    [Fact]
    public void Grammar_RegexesCompile()
    {
        using JsonDocument grammar = JsonDocument.Parse(
            pluginVerilog.CoreBridge.HoverGrammar.Build());

        int checkedRules = 0;
        // Walk the repository's entries (each rule), not the repository object
        // itself - it has no name / match of its own.
        foreach (JsonProperty entry in grammar.RootElement.GetProperty("repository").EnumerateObject())
        {
            foreach ((string scope, string pattern) in Patterns(entry.Value))
            {
                if (pattern.Length == 0) continue;
                _ = new Regex(pattern); // throws on an invalid pattern
                checkedRules++;
            }
        }
        Assert.True(checkedRules > 0, "no regex was found in the grammar");
    }

    private static void CollectScopes(JsonElement repository, HashSet<string> scopes)
    {
        foreach (JsonProperty entry in repository.EnumerateObject())
        {
            foreach ((string scope, string _) in Patterns(entry.Value))
            {
                if (scope.Length > 0) scopes.Add(scope);
            }
        }
    }

    private static IEnumerable<(string Scope, string Pattern)> Patterns(JsonElement node)
    {
        if (node.TryGetProperty("name", out JsonElement name) &&
            name.GetString() is string scope)
        {
            string pattern = node.TryGetProperty("match", out JsonElement match)
                ? match.GetString() ?? ""
                : "";
            yield return (scope, pattern);
        }
        if (node.TryGetProperty("patterns", out JsonElement patterns) &&
            patterns.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in patterns.EnumerateArray())
            {
                foreach ((string Scope, string Pattern) item in Patterns(child))
                {
                    yield return item;
                }
            }
        }
    }

    private static string FindExtensionRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "RtlEditorVscodeExtension");
            if (File.Exists(Path.Combine(candidate, "package.json"))) return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "RtlEditorVscodeExtension folder not found from " + AppContext.BaseDirectory);
    }
}
