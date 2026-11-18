using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// <c>package.json</c> must contain exactly the rows
/// <see cref="pluginVerilog.CoreBridge.HierarchyMenuManifest"/> generates, in
/// the same menus. A drift here is invisible in C# and only shows up as a wrong
/// VS Code menu, so it is checked mechanically.
/// </summary>
public class HierarchyMenuManifestSyncTests
{
    [Fact]
    public void PackageJson_MatchesTheGeneratedManifest()
    {
        string root = FindExtensionRoot();
        JsonNode generated = JsonNode.Parse(
            pluginVerilog.CoreBridge.HierarchyMenuManifest.ToJson())!;
        JsonNode package = JsonNode.Parse(
            File.ReadAllText(Path.Combine(root, "package.json")))!;

        JsonObject generatedMenus = generated["menus"]!.AsObject();
        JsonObject packageMenus = package["contributes"]!["menus"]!.AsObject();

        foreach (KeyValuePair<string, JsonNode?> menu in generatedMenus)
        {
            JsonArray? actual = packageMenus[menu.Key]?.AsArray();
            Assert.True(actual != null,
                "package.json has no menus[\"" + menu.Key + "\"]");

            List<string> expected = Signatures(menu.Value!.AsArray(), generatedOnly: false);
            // package.json also holds rows the extension implements itself
            // ("Reveal in Editor", "Show Menu...", "Copy Instance Path"), which
            // are not part of the generated set and must not be compared.
            List<string> found = Signatures(actual!, generatedOnly: true);
            expected.Sort(StringComparer.Ordinal);
            found.Sort(StringComparer.Ordinal);
            Assert.Equal(expected, found);
        }
    }

    /// <summary>
    /// The generated menu must not put a submenu's children into the tree's own
    /// context menu: that is what made the Add rows render flat.
    /// </summary>
    [Fact]
    public void GeneratedManifest_KeepsSubmenuChildrenOutOfViewItemContext()
    {
        JsonNode generated = JsonNode.Parse(
            pluginVerilog.CoreBridge.HierarchyMenuManifest.ToJson())!;
        JsonObject menus = generated["menus"]!.AsObject();

        foreach (KeyValuePair<string, JsonNode?> menu in menus)
        {
            if (menu.Key == pluginVerilog.CoreBridge.HierarchyMenuManifest.ViewItemContextMenu) continue;
            // a submenu id is not a command target of the tree's menu
            Assert.DoesNotContain(
                pluginVerilog.CoreBridge.HierarchyMenuManifest.CommandPrefix,
                menu.Key);
        }

        // the tree's own menu opens the flyouts
        JsonArray viewItemContext = menus[
            pluginVerilog.CoreBridge.HierarchyMenuManifest.ViewItemContextMenu]!.AsArray();
        bool opensAdd = false;
        foreach (JsonNode? row in viewItemContext)
        {
            if (row?["submenu"]?.GetValue<string>()
                == pluginVerilog.CoreBridge.HierarchyMenuManifest.SubmenuId("add")) opensAdd = true;
        }
        Assert.True(opensAdd, "view/item/context must open the Add submenu");
    }

    /// <summary>
    /// "command|submenu|when|group" so the comparison ignores property order
    /// (VS Code's schema also accepts other keys, which are not compared).
    /// </summary>
    /// <param name="generatedOnly">
    /// When true, rows that are not produced by the catalog (the extension's own
    /// commands) are skipped, so a hand-written row does not fail the sync.
    /// </param>
    private static List<string> Signatures(JsonArray rows, bool generatedOnly)
    {
        List<string> result = new();
        foreach (JsonNode? row in rows)
        {
            if (row is not JsonObject obj) continue;

            if (generatedOnly)
            {
                string? command = obj["command"]?.GetValue<string>();
                string? submenu = obj["submenu"]?.GetValue<string>();
                bool isGenerated =
                    (command != null && command.StartsWith(
                        pluginVerilog.CoreBridge.HierarchyMenuManifest.CommandPrefix,
                        StringComparison.Ordinal))
                    || (submenu != null && submenu.StartsWith(
                        pluginVerilog.CoreBridge.HierarchyMenuManifest.SubmenuPrefix,
                        StringComparison.Ordinal));
                if (!isGenerated) continue;
            }

            result.Add(
                (obj["command"]?.GetValue<string>() ?? "")
                + "|" + (obj["submenu"]?.GetValue<string>() ?? "")
                + "|" + (obj["when"]?.GetValue<string>() ?? "")
                + "|" + (obj["group"]?.GetValue<string>() ?? ""));
        }
        return result;
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
