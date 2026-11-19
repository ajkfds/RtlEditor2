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
            // desktop-only rows are not part of the extension manifest
            if ((entry.Hosts & pluginVerilog.CoreBridge.HierarchyMenuCatalog.Hosts.VsCode) == 0) continue;

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
    /// Every row must be declared in the menu VS Code actually reads it from,
    /// for every node kind the entry applies to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The menu depends on nesting: a top-level row belongs to
    /// <c>view/item/context</c>, but the child of a submenu belongs to
    /// <c>menus["&lt;submenu id&gt;"]</c> - that is where VS Code takes the
    /// flyout's contents from. Putting a submenu's children in
    /// <c>view/item/context</c> (what this file used to do) renders them as
    /// ordinary rows of the tree's context menu, i.e. the Add rows appeared
    /// flat next to the "Add" flyout instead of inside it.
    /// </para>
    /// <para>
    /// A submenu also does not inherit its opening row's context, so each child
    /// re-states the <c>viewItem</c> check; that is asserted here too.
    /// </para>
    /// </remarks>
    [Fact]
    public void PackageJson_ShowsEveryEntryForTheNodeKindsItAppliesTo()
    {
        JsonObject contributes = ReadContributes();
        JsonObject menus = contributes["menus"]?.AsObject()
            ?? throw new Xunit.Sdk.XunitException("package.json has no menus object");

        foreach (pluginVerilog.CoreBridge.HierarchyMenuCatalog.Entry entry
                 in pluginVerilog.CoreBridge.HierarchyMenuCatalog.All)
        {
            // A row declared for the desktop editor only is deliberately absent
            // from package.json (the extension cannot run it), so it must not be
            // expected here either.
            if ((entry.Hosts & pluginVerilog.CoreBridge.HierarchyMenuCatalog.Hosts.VsCode) == 0) continue;

            string target = entry.IsSubmenu
                ? pluginVerilog.CoreBridge.HierarchyMenuManifest.SubmenuId(entry.Id)
                : pluginVerilog.CoreBridge.HierarchyMenuManifest.CommandId(entry.Id);
            string property = entry.IsSubmenu ? "submenu" : "command";

            // A submenu's children live in the menu named after the submenu;
            // everything else lives in the tree's context menu.
            string? parentId = pluginVerilog.CoreBridge.HierarchyMenuCatalog.ParentOf(entry.Id)?.Id;
            string menuName = string.IsNullOrEmpty(parentId)
                ? pluginVerilog.CoreBridge.HierarchyMenuManifest.ViewItemContextMenu
                : pluginVerilog.CoreBridge.HierarchyMenuManifest.SubmenuId(parentId!);

            JsonArray? rows = menus[menuName]?.AsArray();
            Assert.True(rows != null,
                $"package.json has no menus[\"{menuName}\"] for '{entry.Id}'");

            foreach ((string contextValue, pluginVerilog.CoreBridge.HierarchyMenuCatalog.AppliesTo applies) in NodeKinds)
            {
                bool shouldAppear = (entry.Targets & applies) != 0;
                bool appears = false;
                foreach (JsonNode? row in rows!)
                {
                    if (row is not JsonObject obj) continue;
                    if (obj[property]?.GetValue<string>() != target) continue;
                    string when = obj["when"]?.GetValue<string>() ?? "";
                    if (when.Contains("viewItem == " + contextValue, StringComparison.Ordinal)) appears = true;
                }
                Assert.True(shouldAppear == appears,
                    $"'{entry.Id}' ({(shouldAppear ? "should" : "should not")} appear for {contextValue} in '{menuName}')");
            }
        }
    }

    /// <summary>
    /// A submenu's children must NOT also be listed in <c>view/item/context</c>:
    /// VS Code would then show them twice - once inside the flyout and once as
    /// a flat row of the tree's context menu, which is the reported symptom.
    /// </summary>
    [Fact]
    public void PackageJson_DoesNotFlattenSubmenuChildren()
    {
        JsonObject contributes = ReadContributes();
        JsonArray rows = contributes["menus"]?[pluginVerilog.CoreBridge.HierarchyMenuManifest.ViewItemContextMenu]?.AsArray()
            ?? throw new Xunit.Sdk.XunitException("package.json has no menus.view/item/context");

        foreach (pluginVerilog.CoreBridge.HierarchyMenuCatalog.Entry entry
                 in pluginVerilog.CoreBridge.HierarchyMenuCatalog.All)
        {
            if (pluginVerilog.CoreBridge.HierarchyMenuCatalog.ParentOf(entry.Id) == null) continue;
            if ((entry.Hosts & pluginVerilog.CoreBridge.HierarchyMenuCatalog.Hosts.VsCode) == 0) continue;
            // a nested submenu opens its own flyout, so it is a row of the
            // parent menu - but it must still be a `submenu`, never a command
            string target = entry.IsSubmenu
                ? pluginVerilog.CoreBridge.HierarchyMenuManifest.SubmenuId(entry.Id)
                : pluginVerilog.CoreBridge.HierarchyMenuManifest.CommandId(entry.Id);

            foreach (JsonNode? row in rows)
            {
                if (row is not JsonObject obj) continue;
                Assert.NotEqual(target, obj["command"]?.GetValue<string>());
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
            // only the rows VS Code actually offers get a generated command
            if ((entry.Hosts & pluginVerilog.CoreBridge.HierarchyMenuCatalog.Hosts.VsCode) == 0) continue;
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
