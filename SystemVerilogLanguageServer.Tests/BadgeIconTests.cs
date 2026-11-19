using System;
using System.IO;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// The tree icons carry the error / warning / dirty overlays as separate SVG
/// assets, because VS Code's <c>TreeItem.iconPath</c> cannot draw an overlay.
/// They are generated from the base icons by
/// <see cref="pluginVerilog.CoreBridge.BadgeIconGenerator"/>, which mirrors
/// <c>RtlEditorVscodeExtension/scripts/generate-badge-icons.js</c>; these tests
/// pin that the committed assets are what either generator produces, so a new
/// base icon or a changed badge cannot ship without its variants.
/// </summary>
public class BadgeIconTests
{
    /// <summary>
    /// Every base icon the server can return has a variant for each badge, in
    /// both themes. A missing file makes VS Code draw nothing at all for that
    /// node (a broken icon, not a missing badge).
    /// </summary>
    [Fact]
    public void EveryBadgeableIconHasAVariantForEveryBadge()
    {
        string icons = IconsDirectory();

        foreach (string baseName in pluginVerilog.CoreBridge.BadgeIconGenerator.BadgeableIcons)
        {
            foreach (string variant in new[] { "", "-dark" })
            {
                Assert.True(
                    File.Exists(Path.Combine(icons, baseName + variant + ".svg")),
                    "missing base icon: " + baseName + variant + ".svg");

                foreach (string badge in pluginVerilog.CoreBridge.BadgeIconGenerator.BadgeNames)
                {
                    string file = Path.Combine(icons, baseName + "-" + badge + variant + ".svg");
                    Assert.True(File.Exists(file), "missing badge icon: " + Path.GetFileName(file));
                }
            }
        }
    }

    /// <summary>
    /// The committed assets must match the generator. This is the check that
    /// replaces "remember to run generate-badge-icons.js".
    /// </summary>
    [Fact]
    public void CommittedIconsAreUpToDate()
    {
        pluginVerilog.CoreBridge.BadgeIconResult result =
            pluginVerilog.CoreBridge.BadgeIconGenerator.Generate(IconsDirectory(), write: false);

        Assert.False(result.Failed, result.Message);
        Assert.True(result.Changed.Count == 0,
            "badge icons are out of date: " + string.Join(", ", result.Changed) +
            " - run: dotnet SystemVerilogLanguageServer.dll " +
            "--write-hierarchy-menu-manifest <path to package.json>");
    }

    /// <summary>
    /// Generating over an up-to-date folder must not touch anything, so the
    /// command can run on every build without dirtying the working tree.
    /// </summary>
    [Fact]
    public void Generate_IsIdempotent()
    {
        string temp = Path.Combine(Path.GetTempPath(), "icons_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            // copy the base icons only, then generate twice
            foreach (string baseName in pluginVerilog.CoreBridge.BadgeIconGenerator.BadgeableIcons)
            {
                foreach (string variant in new[] { "", "-dark" })
                {
                    string source = Path.Combine(IconsDirectory(), baseName + variant + ".svg");
                    if (File.Exists(source)) File.Copy(source, Path.Combine(temp, Path.GetFileName(source)));
                }
            }

            pluginVerilog.CoreBridge.BadgeIconResult first =
                pluginVerilog.CoreBridge.BadgeIconGenerator.Generate(temp, write: true);
            Assert.False(first.Failed, first.Message);
            Assert.True(first.Written > 0, "the first run must produce the variants");

            pluginVerilog.CoreBridge.BadgeIconResult second =
                pluginVerilog.CoreBridge.BadgeIconGenerator.Generate(temp, write: true);
            Assert.False(second.Failed, second.Message);
            Assert.Equal(0, second.Written);
            Assert.Empty(second.Changed);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { /* best effort */ }
        }
    }

    private static string IconsDirectory()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "RtlEditorVscodeExtension", "icons");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "RtlEditorVscodeExtension/icons not found from " + AppContext.BaseDirectory);
    }
}
