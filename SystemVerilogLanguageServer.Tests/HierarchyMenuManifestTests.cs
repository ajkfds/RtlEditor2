using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// The RTL Hierarchy context menu must show the language server's entries as
/// ordinary VS Code menu rows, nested into submenus, indistinguishable from the
/// rows the extension declares itself.
/// </summary>
/// <remarks>
/// <para>
/// VS Code reads <c>contributes.commands</c> / <c>contributes.submenus</c> /
/// <c>contributes.menus."view/item/context"</c> from <c>package.json</c> when the
/// extension loads, and offers no API to add a row at run time. The catalog is
/// therefore the source of truth and <c>HierarchyMenuManifest</c> renders it
/// into package.json; these tests fail when the committed file no longer
/// matches the catalog, which is the only way the two can drift.
/// </para>
/// </remarks>
public class HierarchyMenuManifestTests
{
    /// <summary>
    /// Every catalog entry that is not a submenu has a command, and every
    /// submenu entry has a submenu, in package.json.
    /// </summary>
    [Fact]
    public void PackageJson_DeclaresAContributionForEveryCatalogEntry()
    {
        JsonObject contributes = ReadContributes();

        List<string> commands = CommandIds(contributes);
        List<string> submenus = SubmenuIds(contributes);

        foreach (pluginVerilog.CoreBridge.HierarchyMenuCatalog.Entry entry
                 in pluginVerilog.CoreBridge.HierarchyMenuCatalog.All)
        {
            if (entry.IsSubmenu)
            {
                string expected = pluginVerilog.CoreBridge.HierarchyMenuManifest.SubmenuId(entry.Id);
                Assert.True(submenus.Contains(expected),
                    "package.json has no submenu contribution for '" + entry.Id + "' (" + expected + ")");
                continue;
            }

            string commandId = pluginVerilog.CoreBridge.HierarchyMenuManifest.CommandId(entry.Id);
            Assert.True(commands.Contains(commandId),
                "package.json has no command contribution for '" + entry.Id + "' (" + commandId + ")");
        }
    }

    /// <summary>
    /// The generated rows must be in <c>view/item/context</c> for every node
    /// kind the entry applies to, so a C#-defined entry appears in exactly the
    /// places a hand-written one would.
    /// </summary>
    [Fact]
    public void PackageJson_ShowsEveryEntryForTheNodeKindsItAppliesTo()
    {
        JsonObject contributes = ReadContributes();
        JsonArray rows = contributes["menus"]?["view/item/context"]?.AsArray()
            ?? throw new Xunit.Sdk.XunitException("package.json has no menus.view/item/context");

        foreach (pluginVerilog.CoreBridge.HierarchyMenuCatalog.Entry entry
                 in pluginVerilog.CoreBridge.HierarchyMenuCatalog.All)
        {
            string target = entry.IsSubmenu
                ? pluginVerilog.CoreBridge.HierarchyMenuManifest.SubmenuId(entry.Id)
                : pluginVerilog.CoreBridge.HierarchyMenuManifest.CommandId(entry.Id);
            string property = entry.IsSubmenu ? "submenu" : "command";

            foreach ((string contextValue, pluginVerilog.CoreBridge.HierarchyMenuCatalog.AppliesTo applies) in NodeKinds)
            {
                bool shouldAppear = (entry.Targets & applies) != 0;
                bool appears = false;
                foreach (JsonNode? row in rows)
                {
                    if (row is not JsonObject obj) continue;
                    if (obj[property]?.GetValue<string>() != target) continue;
                    string when = obj["when"]?.GetValue<string>() ?? "";
                    if (when.Contains("viewItem == " + contextValue, StringComparison.Ordinal)) appears = true;
                }
                Assert.True(shouldAppear == appears,
                    $"'{entry.Id}' ({(shouldAppear ? "should" : "should not")} appear for {contextValue})");
            }
        }
    }

    /// <summary>
    /// Nesting is expressed by a row whose <c>submenu</c> property names a
    /// declared submenu, and the children of a submenu are plain commands. This
    /// is the shape VS Code renders as a flyout.
    /// </summary>
    [Fact]
    public void PackageJson_UsesSubmenusForNestedEntries()
    {
        JsonObject contributes = ReadContributes();
        List<string> submenus = SubmenuIds(contributes);

        // a submenu contribution exists for the catalog's grouping entry
        Assert.True(submenus.Contains(
            pluginVerilog.CoreBridge.HierarchyMenuManifest.SubmenuId(
                pluginVerilog.CoreBridge.HierarchyMenuCatalog.MenuId.Simulation)));

        // the child is a command, not another submenu row
        List<string> commands = CommandIds(contributes);
        Assert.True(commands.Contains(
            pluginVerilog.CoreBridge.HierarchyMenuManifest.CommandId(
                pluginVerilog.CoreBridge.HierarchyMenuCatalog.MenuId.SimulationSetup)));
    }

    /// <summary>
    /// The extension registers an implementation for each generated command id;
    /// if the list in hierarchy.js and the catalog disagree, VS Code shows
    /// "command not found" (or a C# entry silently does nothing).
    /// </summary>
    [Fact]
    public void HierarchyJs_RegistersEveryGeneratedCommandId()
    {
        string[] ids = ReadHierarchyJsIds();

        List<string> expected = new();
        foreach (pluginVerilog.CoreBridge.HierarchyMenuCatalog.Entry entry
                 in pluginVerilog.CoreBridge.HierarchyMenuCatalog.All)
        {
            if (entry.IsSubmenu) continue;
            expected.Add(entry.Id);
        }
        expected.Sort(StringComparer.Ordinal);

        string[] actual = ids;
        Array.Sort(actual, StringComparer.Ordinal);
        Assert.Equal(expected.ToArray(), actual);
    }

    private static readonly (string ContextValue, pluginVerilog.CoreBridge.HierarchyMenuCatalog.AppliesTo Applies)[] NodeKinds =
    {
        ("rtlFolder", pluginVerilog.CoreBridge.HierarchyMenuCatalog.AppliesTo.Folder),
        ("rtlFile", pluginVerilog.CoreBridge.HierarchyMenuCatalog.AppliesTo.File),
        ("rtlInstance", pluginVerilog.CoreBridge.HierarchyMenuCatalog.AppliesTo.Instance),
    };

    private static JsonObject ReadContributes()
    {
        string path = Path.Combine(FindExtensionRoot(), "package.json");
        JsonNode? root = JsonNode.Parse(File.ReadAllText(path));
        JsonObject? contributes = root?["contributes"]?.AsObject();
        Assert.True(contributes != null, "package.json has no contributes object");
        return contributes!;
    }

    private static List<string> CommandIds(JsonObject contributes)
    {
        List<string> result = new();
        JsonArray? commands = contributes["commands"]?.AsArray();
        if (commands == null) return result;
        foreach (JsonNode? command in commands)
        {
            string? id = command?["command"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(id)) result.Add(id!);
        }
        return result;
    }

    private static List<string> SubmenuIds(JsonObject contributes)
    {
        List<string> result = new();
        JsonArray? submenus = contributes["submenus"]?.AsArray();
        if (submenus == null) return result;
        foreach (JsonNode? submenu in submenus)
        {
            string? id = submenu?["id"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(id)) result.Add(id!);
        }
        return result;
    }

    /// <summary>Reads GENERATED_HIERARCHY_MENU_IDS out of hierarchy.js.</summary>
    private static string[] ReadHierarchyJsIds()
    {
        string path = Path.Combine(FindExtensionRoot(), "hierarchy.js");
        string text = File.ReadAllText(path);

        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"const\s+GENERATED_HIERARCHY_MENU_IDS\s*=\s*\[(?<items>[^\]]*)\]",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(match.Success, "could not find GENERATED_HIERARCHY_MENU_IDS in hierarchy.js");

        List<string> ids = new();
        foreach (System.Text.RegularExpressions.Match item in System.Text.RegularExpressions.Regex.Matches(
                     match.Groups["items"].Value, @"'([^']*)'"))
        {
            ids.Add(item.Groups[1].Value);
        }
        return ids.ToArray();
    }

    private static string FindExtensionRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "RtlEditorVscodeExtension");
            if (File.Exists(Path.Combine(candidate, "package.json"))
                && File.Exists(Path.Combine(candidate, "hierarchy.js")))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("RtlEditorVscodeExtension folder not found from " + AppContext.BaseDirectory);
    }
}
