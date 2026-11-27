using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// <c>package.json</c> must contain exactly the rows
/// <see cref="SystemVerilogCore.Menus.HierarchyMenuManifest"/> generates, in
/// the same menus. A drift here is invisible in C# and only shows up as a wrong
/// VS Code menu, so it is checked mechanically.
/// </summary>
public class HierarchyMenuManifestSyncTests
{
    /// <summary>
    /// The committed file must be what the generator produces. This is the
    /// check that replaces "remember to paste the JSON in by hand".
    /// </summary>
    [Fact]
    public void CommittedPackageJson_IsUpToDate()
    {
        string path = Path.Combine(FindExtensionRoot(), "package.json");

        SystemVerilogCore.Menus.PackageJsonWriteResult result =
            SystemVerilogCore.Menus.HierarchyMenuManifest.WriteToPackageJson(path, write: false);

        Assert.False(result.Failed, result.Message);
        Assert.False(result.Changed,
            "package.json is out of date. Run: dotnet SystemVerilogLanguageServer.dll " +
            "--write-hierarchy-menu-manifest " + path);
    }

    /// <summary>
    /// Writing an up-to-date file must be a no-op, so the generator can run on
    /// every build without touching the working tree.
    /// </summary>
    [Fact]
    public void Write_IsIdempotent()
    {
        string original = File.ReadAllText(Path.Combine(FindExtensionRoot(), "package.json"));
        string temp = Path.Combine(Path.GetTempPath(), "pkg_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(temp, original);
        try
        {
            SystemVerilogCore.Menus.PackageJsonWriteResult first =
                SystemVerilogCore.Menus.HierarchyMenuManifest.WriteToPackageJson(temp, write: true);
            Assert.False(first.Failed, first.Message);

            string afterFirst = File.ReadAllText(temp);
            SystemVerilogCore.Menus.PackageJsonWriteResult second =
                SystemVerilogCore.Menus.HierarchyMenuManifest.WriteToPackageJson(temp, write: true);
            Assert.False(second.Failed, second.Message);
            Assert.False(second.Changed, "the second write must not change anything");
            Assert.Equal(afterFirst, File.ReadAllText(temp));
        }
        finally
        {
            try { File.Delete(temp); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// A stale file (a generated row removed by hand) must be detected and
    /// repaired, and the extension's own rows must survive the repair.
    /// </summary>
    [Fact]
    public void Write_RepairsAStaleFileAndKeepsHandWrittenRows()
    {
        string original = File.ReadAllText(Path.Combine(FindExtensionRoot(), "package.json"));
        string temp = Path.Combine(Path.GetTempPath(), "pkg_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(temp, original);
        try
        {
            // Drop one generated row and one generated command, keeping the
            // hand-written ones ("showNodeMenu" etc.) untouched.
            JsonNode root = JsonNode.Parse(original)!;
            JsonArray commands = root["contributes"]!["commands"]!.AsArray();
            for (int i = commands.Count - 1; i >= 0; i--)
            {
                if (commands[i]?["command"]?.GetValue<string>()
                    == SystemVerilogCore.Menus.HierarchyMenuManifest.CommandId("copyPath"))
                {
                    commands.RemoveAt(i);
                }
            }
            JsonObject menus = root["contributes"]!["menus"]!.AsObject();
            menus.Remove(SystemVerilogCore.Menus.HierarchyMenuManifest.SubmenuId("add"));

            File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            SystemVerilogCore.Menus.PackageJsonWriteResult check =
                SystemVerilogCore.Menus.HierarchyMenuManifest.WriteToPackageJson(temp, write: false);
            Assert.True(check.Changed, "a stale package.json must be reported as out of date");

            SystemVerilogCore.Menus.PackageJsonWriteResult repaired =
                SystemVerilogCore.Menus.HierarchyMenuManifest.WriteToPackageJson(temp, write: true);
            Assert.False(repaired.Failed, repaired.Message);
            Assert.True(repaired.Changed);

            // The repair settles: a second write changes nothing. (The first
            // write only replaces the generated spans, so the surrounding
            // formatting is whatever the caller had - which is why this checks
            // for stability rather than for byte equality with the original.)
            string afterRepair = File.ReadAllText(temp);
            SystemVerilogCore.Menus.PackageJsonWriteResult recheck =
                SystemVerilogCore.Menus.HierarchyMenuManifest.WriteToPackageJson(temp, write: false);
            Assert.False(recheck.Changed, recheck.Message);
            Assert.Equal(afterRepair, File.ReadAllText(temp));

            // the repaired blocks equal the catalog's rows
            JsonNode repairedRoot = JsonNode.Parse(afterRepair)!;
            List<string> repairedCommandIds = new();
            foreach (JsonNode? command in repairedRoot["contributes"]!["commands"]!.AsArray())
            {
                string? id = command?["command"]?.GetValue<string>();
                if (id != null) repairedCommandIds.Add(id);
            }
            Assert.Contains(
                SystemVerilogCore.Menus.HierarchyMenuManifest.CommandId("copyPath"),
                repairedCommandIds);
            Assert.True(
                repairedRoot["contributes"]!["menus"]!.AsObject()
                    .ContainsKey(SystemVerilogCore.Menus.HierarchyMenuManifest.SubmenuId("add")),
                "the dropped submenu must be restored");

            // and the extension's own rows are still there
            JsonNode after = JsonNode.Parse(File.ReadAllText(temp))!;
            List<string> ids = new();
            foreach (JsonNode? command in after["contributes"]!["commands"]!.AsArray())
            {
                string? id = command?["command"]?.GetValue<string>();
                if (id != null) ids.Add(id);
            }
            Assert.Contains("systemverilogLsp.showNodeMenu", ids);
            Assert.Contains("systemverilogLsp.revealInEditor", ids);
        }
        finally
        {
            try { File.Delete(temp); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void PackageJson_MatchesTheGeneratedManifest()
    {
        string root = FindExtensionRoot();
        JsonNode generated = JsonNode.Parse(
            SystemVerilogCore.Menus.HierarchyMenuManifest.ToJson())!;
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
            SystemVerilogCore.Menus.HierarchyMenuManifest.ToJson())!;
        JsonObject menus = generated["menus"]!.AsObject();

        foreach (KeyValuePair<string, JsonNode?> menu in menus)
        {
            if (menu.Key == SystemVerilogCore.Menus.HierarchyMenuManifest.ViewItemContextMenu) continue;
            // a submenu id is not a command target of the tree's menu
            Assert.DoesNotContain(
                SystemVerilogCore.Menus.HierarchyMenuManifest.CommandPrefix,
                menu.Key);
        }

        // the tree's own menu opens the flyouts
        JsonArray viewItemContext = menus[
            SystemVerilogCore.Menus.HierarchyMenuManifest.ViewItemContextMenu]!.AsArray();
        bool opensAdd = false;
        foreach (JsonNode? row in viewItemContext)
        {
            if (row?["submenu"]?.GetValue<string>()
                == SystemVerilogCore.Menus.HierarchyMenuManifest.SubmenuId("add")) opensAdd = true;
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
                        SystemVerilogCore.Menus.HierarchyMenuManifest.CommandPrefix,
                        StringComparison.Ordinal))
                    || (submenu != null && submenu.StartsWith(
                        SystemVerilogCore.Menus.HierarchyMenuManifest.SubmenuPrefix,
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
