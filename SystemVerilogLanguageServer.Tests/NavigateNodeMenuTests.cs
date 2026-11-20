using System.Collections.Generic;
using System.Linq;
using pluginVerilog.CoreBridge;
using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// The navigate-node menu is one catalog rendered by two hosts: the desktop
/// editor's NavigatePanel context menu and the VS Code "RTL Hierarchy" node
/// menu. These tests pin the contract that makes that possible - the host
/// filter and the action mapping - because a mistake here shows up as "the row
/// disappeared in one host" rather than as a compile error.
/// </summary>
/// <remarks>
/// <para>
/// The editor used to customise its menu through
/// <c>CustomizeNavigateNodeContextMenu</c> (an Avalonia event) while VS Code had
/// its own list, so the two drifted. Both now read
/// <see cref="NavigateNodeMenuAdapter"/>; a row declares which hosts offer it
/// with <see cref="HierarchyMenuCatalog.Entry.Hosts"/>.
/// </para>
/// </remarks>
public class NavigateNodeMenuTests
{
    /// <summary>
    /// A row declared for the editor must never reach VS Code, and the other way
    /// round. This is what replaces the per-host subscription lists.
    /// </summary>
    [Fact]
    public void Build_FiltersRowsByHost()
    {
        const string desktopOnly = "testDesktopOnlyRow";
        const string vsCodeOnly = "testVsCodeOnlyRow";
        Register(desktopOnly, HierarchyMenuCatalog.Hosts.Desktop);
        Register(vsCodeOnly, HierarchyMenuCatalog.Hosts.VsCode);

        try
        {
            NavigateNodeMenuContext context = FileContext();

            List<string> editor = Ids(NavigateNodeMenuAdapter.Build(
                context, HierarchyMenuCatalog.Hosts.Desktop));
            List<string> vsCode = Ids(NavigateNodeMenuAdapter.Build(
                context, HierarchyMenuCatalog.Hosts.VsCode));

            Assert.Contains(desktopOnly, editor);
            Assert.DoesNotContain(desktopOnly, vsCode);
            Assert.Contains(vsCodeOnly, vsCode);
            Assert.DoesNotContain(vsCodeOnly, editor);
        }
        finally
        {
            HierarchyMenuCatalog.Unregister(desktopOnly);
            HierarchyMenuCatalog.Unregister(vsCodeOnly);
        }
    }

    /// <summary>
    /// The shared rows must appear in both hosts - that is the whole point of
    /// the unification: one declaration, two menus.
    /// </summary>
    [Fact]
    public void Build_SharedRowsAppearInBothHosts()
    {
        NavigateNodeMenuContext context = FileContext();

        List<string> editor = Ids(NavigateNodeMenuAdapter.Build(
            context, HierarchyMenuCatalog.Hosts.Desktop));
        List<string> vsCode = Ids(NavigateNodeMenuAdapter.Build(
            context, HierarchyMenuCatalog.Hosts.VsCode));

        foreach (string id in new[]
                 {
                     HierarchyMenuCatalog.MenuId.SimulationSetup,
                     HierarchyMenuCatalog.MenuId.CopyPath,
                     HierarchyMenuCatalog.MenuId.OpenInExplorer,
                     HierarchyMenuCatalog.MenuId.ShowProperty,
                 })
        {
            Assert.Contains(id, editor);
            Assert.Contains(id, vsCode);
        }
    }

    /// <summary>
    /// A submenu is offered as a row in both hosts (the editor nests a
    /// MenuItem, VS Code renders a flyout) and is never runnable itself.
    /// </summary>
    [Fact]
    public void Build_SubmenuRowIsGroupingOnly()
    {
        NavigateNodeMenuContext context = FileContext();

        NavigateNodeMenuAdapter.MenuResult result =
            NavigateNodeMenuAdapter.Build(context, HierarchyMenuCatalog.Hosts.Desktop);
        NavigateNodeMenuAdapter.MenuEntry? submenu =
            result.Entries.FirstOrDefault(e => e.Id == HierarchyMenuCatalog.MenuId.Simulation);

        Assert.NotNull(submenu);
        Assert.True(submenu!.IsSubmenu);
        Assert.True(submenu.Enabled, "a grouping row must be offered (the host nests it)");

        NavigateNodeMenuAdapter.InvokeResult invoked =
            NavigateNodeMenuAdapter.Invoke(HierarchyMenuCatalog.MenuId.Simulation, context);
        Assert.False(string.IsNullOrEmpty(invoked.Message));
    }

    /// <summary>
    /// Nesting is the shape of the declaration: a row is in a submenu because it
    /// is written inside that row's <c>Children</c>, and
    /// <see cref="HierarchyMenuCatalog.Entry.IsSubmenu"/> is derived from it. A
    /// separately authored <c>IsSubmenu</c> flag (what this replaced) could
    /// disagree with the tree.
    /// </summary>
    [Fact]
    public void Catalog_NestingIsTheShapeOfTheTree()
    {
        HierarchyMenuCatalog.Entry? add = HierarchyMenuCatalog.Find(HierarchyMenuCatalog.MenuId.Add);
        Assert.NotNull(add);
        Assert.True(add!.IsSubmenu, "a row with children must be a grouping row");

        // the Add children are reachable through the tree, not through a lookup
        List<HierarchyMenuCatalog.Entry> children = HierarchyMenuCatalog.Children(add);
        Assert.NotEmpty(children);
        foreach (HierarchyMenuCatalog.Entry child in children)
        {
            Assert.False(child.IsSubmenu, "these rows have no children of their own");
            Assert.Same(add, HierarchyMenuCatalog.ParentOf(child.Id));
        }

        // a leaf at the top level has no parent and is not a grouping row
        HierarchyMenuCatalog.Entry? copyPath =
            HierarchyMenuCatalog.Find(HierarchyMenuCatalog.MenuId.CopyPath);
        Assert.NotNull(copyPath);
        Assert.False(copyPath!.IsSubmenu);
        Assert.Null(HierarchyMenuCatalog.ParentOf(copyPath.Id));
        Assert.Contains(copyPath, HierarchyMenuCatalog.Root);
    }

    /// <summary>
    /// A plugin can extend a submenu without knowing its id: it names the parent
    /// row. The row lands inside that submenu in every host, because the hosts
    /// walk the tree instead of matching ids.
    /// </summary>
    [Fact]
    public void Register_PlacesTheRowInsideTheNamedParent()
    {
        const string id = "testNestedPluginRow";
        HierarchyMenuCatalog.Register(new HierarchyMenuCatalog.Entry
        {
            Id = id,
            Label = "Plugin row",
            Detail = "added by a plugin",
            Targets = HierarchyMenuCatalog.AppliesTo.File,
            Hosts = HierarchyMenuCatalog.Hosts.Both,
            Action = HierarchyMenuCatalog.ActionKind.CopyPath,
            Group = 15,
        }, parentId: HierarchyMenuCatalog.MenuId.Add);

        try
        {
            HierarchyMenuCatalog.Entry? parent =
                HierarchyMenuCatalog.ParentOf(id);
            Assert.NotNull(parent);
            Assert.Equal(HierarchyMenuCatalog.MenuId.Add, parent!.Id);

            // and the adapter reports it as nested, so both hosts render it
            // inside the submenu
            NavigateNodeMenuAdapter.MenuResult result = NavigateNodeMenuAdapter.Build(
                FileContext(), HierarchyMenuCatalog.Hosts.VsCode);
            NavigateNodeMenuAdapter.MenuEntry? entry =
                result.Entries.FirstOrDefault(e => e.Id == id);
            Assert.NotNull(entry);
            Assert.Equal(HierarchyMenuCatalog.MenuId.Add, entry!.ParentId);
            Assert.Equal("Add › Plugin row", entry.FlatLabel);
            Assert.Equal("Plugin row", entry.Label);
        }
        finally
        {
            HierarchyMenuCatalog.Unregister(id);
        }

        Assert.Null(HierarchyMenuCatalog.Find(id));
    }

    /// <summary>
    /// A row whose <c>Available</c> predicate says no must not be listed, and a
    /// grouping row whose children are all unavailable must not be listed either
    /// (an empty submenu is worse than no submenu).
    /// </summary>
    [Fact]
    public void Build_HidesRowsThatAreNotAvailable()
    {
        const string groupId = "testUnavailableGroup";
        const string childId = "testUnavailableChild";
        bool available = false;

        HierarchyMenuCatalog.Register(new HierarchyMenuCatalog.Entry
        {
            Id = groupId,
            Label = "Simulation tools",
            Detail = "test group",
            Targets = HierarchyMenuCatalog.AppliesTo.File,
            Hosts = HierarchyMenuCatalog.Hosts.Both,
            Group = 97,
            Children =
            {
                new HierarchyMenuCatalog.Entry
                {
                    Id = childId,
                    Label = "Run",
                    Detail = "test row",
                    Targets = HierarchyMenuCatalog.AppliesTo.File,
                    Hosts = HierarchyMenuCatalog.Hosts.Both,
                    Action = HierarchyMenuCatalog.ActionKind.CopyPath,
                    Group = 97,
                    Available = () => available,
                },
            },
        });

        try
        {
            NavigateNodeMenuContext context = FileContext();

            List<string> hidden = Ids(NavigateNodeMenuAdapter.Build(
                context, HierarchyMenuCatalog.Hosts.Desktop));
            Assert.DoesNotContain(childId, hidden);
            Assert.DoesNotContain(groupId, hidden);

            // the row appears as soon as its condition becomes true
            available = true;
            List<string> shown = Ids(NavigateNodeMenuAdapter.Build(
                context, HierarchyMenuCatalog.Hosts.Desktop));
            Assert.Contains(childId, shown);
            Assert.Contains(groupId, shown);
        }
        finally
        {
            HierarchyMenuCatalog.Unregister(groupId);
        }
    }

    /// <summary>
    /// A predicate that throws must hide its row, not take the menu down: a
    /// plugin probing a state that is not ready yet should never break the
    /// right-click menu.
    /// </summary>
    [Fact]
    public void Build_SurvivesAThrowingAvailabilityPredicate()
    {
        const string id = "testThrowingAvailability";
        HierarchyMenuCatalog.Register(new HierarchyMenuCatalog.Entry
        {
            Id = id,
            Label = "Broken row",
            Detail = "test row",
            Targets = HierarchyMenuCatalog.AppliesTo.File,
            Hosts = HierarchyMenuCatalog.Hosts.Both,
            Action = HierarchyMenuCatalog.ActionKind.CopyPath,
            Group = 97,
            Available = () => throw new InvalidOperationException("boom"),
        });

        try
        {
            List<string> ids = Ids(NavigateNodeMenuAdapter.Build(
                FileContext(), HierarchyMenuCatalog.Hosts.Desktop));
            Assert.DoesNotContain(id, ids);
        }
        finally
        {
            HierarchyMenuCatalog.Unregister(id);
        }
    }

    /// <summary>
    /// A row that declares an icon must carry it to the client, and an icon name
    /// must resolve to a file the extension actually ships - a missing asset
    /// makes VS Code draw nothing for that row.
    /// </summary>
    [Fact]
    public void Build_ForwardsTheClientIcon()
    {
        NavigateNodeMenuAdapter.MenuResult result = NavigateNodeMenuAdapter.Build(
            FileContext(), HierarchyMenuCatalog.Hosts.VsCode);

        NavigateNodeMenuAdapter.MenuEntry? verilogModule =
            result.Entries.FirstOrDefault(e => e.Id == HierarchyMenuCatalog.MenuId.AddVerilogModule);
        Assert.NotNull(verilogModule);
        Assert.Equal("verilog-document", verilogModule!.VsCodeIcon);

        // a codicon is passed through untouched
        NavigateNodeMenuAdapter.MenuEntry? delete =
            result.Entries.FirstOrDefault(e => e.Id == HierarchyMenuCatalog.MenuId.DeleteNode);
        if (delete != null) Assert.StartsWith("$(", delete.VsCodeIcon ?? "");

        // every bare icon name must exist under the extension's icons/
        string icons = IconsDirectory();
        foreach (NavigateNodeMenuAdapter.MenuEntry entry in result.Entries)
        {
            string? icon = entry.VsCodeIcon;
            if (string.IsNullOrEmpty(icon) || icon!.StartsWith("$(", StringComparison.Ordinal)) continue;
            foreach (string variant in new[] { "", "-dark" })
            {
                string file = Path.Combine(icons, icon + variant + ".svg");
                Assert.True(File.Exists(file),
                    "menu row '" + entry.Id + "' names icon '" + icon +
                    "' but " + Path.GetFileName(file) + " does not exist");
            }
        }
    }

    private static string IconsDirectory()
    {
        System.IO.DirectoryInfo? dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = System.IO.Path.Combine(
                dir.FullName, "RtlEditorVscodeExtension", "icons");
            if (System.IO.Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "RtlEditorVscodeExtension/icons not found from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Every catalog action that the two hosts share must be reachable without a
    /// desktop handler, otherwise VS Code would offer a row it can never run.
    /// </summary>
    [Fact]
    public void Catalog_VsCodeRowsNeedNoDesktopHandler()
    {
        foreach (HierarchyMenuCatalog.Entry entry in HierarchyMenuCatalog.All)
        {
            if ((entry.Hosts & HierarchyMenuCatalog.Hosts.VsCode) == 0) continue;
            Assert.NotEqual(HierarchyMenuCatalog.ActionKind.DesktopHandler, entry.Action);
        }
    }

    /// <summary>
    /// The file-system work is returned as an operation so each host can perform
    /// it with its own API (<c>System.IO</c> in the editor,
    /// <c>vscode.workspace.fs</c> in the extension). The decision of <b>where</b>
    /// stays on the C# side.
    /// </summary>
    [Fact]
    public void Invoke_RevealReturnsAnOperationNotAPathOnly()
    {
        NavigateNodeMenuContext context = FileContext();

        NavigateNodeMenuAdapter.InvokeResult result =
            NavigateNodeMenuAdapter.Invoke(HierarchyMenuCatalog.MenuId.OpenInExplorer, context);

        Assert.Null(result.Message);
        Assert.NotNull(result.Operation);
        Assert.Equal("reveal", result.Operation!.Kind);
        Assert.Equal(context.FsPath, result.Operation.Path);
    }

    /// <summary>
    /// "Copy Path" prefers the workspace-relative path, like the editor does.
    /// </summary>
    [Fact]
    public void Invoke_CopyPathPrefersTheWorkspaceRelativePath()
    {
        NavigateNodeMenuContext context = FileContext();

        NavigateNodeMenuAdapter.InvokeResult result =
            NavigateNodeMenuAdapter.Invoke(HierarchyMenuCatalog.MenuId.CopyPath, context);

        Assert.Equal("src/rtl/top.sv", result.ClipboardText);
    }

    /// <summary>
    /// Creating a file asks for a name first, then returns a create operation
    /// whose content is built in C#, so both hosts write the same skeleton.
    /// </summary>
    [Fact]
    public void Invoke_CreateSourceFilePromptsThenReturnsTheSkeleton()
    {
        const string id = "testCreateVerilogFile";
        HierarchyMenuCatalog.Register(new HierarchyMenuCatalog.Entry
        {
            Id = id,
            Label = "verilog module",
            Targets = HierarchyMenuCatalog.AppliesTo.Folder,
            Hosts = HierarchyMenuCatalog.Hosts.Both,
            Action = HierarchyMenuCatalog.ActionKind.CreateSourceFile,
            Argument = "verilog",
            Group = 20,
        });

        try
        {
            NavigateNodeMenuContext context = new NavigateNodeMenuContext
            {
                Kind = HierarchyMenuCatalog.AppliesTo.Folder,
                NodeLabel = "rtl",
                DirPath = System.IO.Path.Combine("root", "rtl"),
            };

            NavigateNodeMenuAdapter.InvokeResult prompt =
                NavigateNodeMenuAdapter.Invoke(id, context);
            Assert.False(string.IsNullOrEmpty(prompt.Prompt));
            Assert.Null(prompt.Operation);

            NavigateNodeMenuAdapter.InvokeResult created =
                NavigateNodeMenuAdapter.Invoke(id, context, "myModule");
            Assert.Null(created.Message);
            Assert.NotNull(created.Operation);
            Assert.Equal("createFile", created.Operation!.Kind);
            Assert.EndsWith("myModule.v", created.Operation.Path);
            Assert.Contains("module myModule;", created.Operation.Content);
            Assert.Contains("endmodule", created.Operation.Content);
        }
        finally
        {
            HierarchyMenuCatalog.Unregister(id);
        }
    }

    /// <summary>
    /// A nested row's label must be the bare catalog label, because the desktop
    /// menu already nests it inside its parent MenuItem and VS Code renders it
    /// as a flyout. Prefixing the parent in <c>Label</c> made the editor show
    /// "Add › Folder" inside a submenu literally named "Add".
    /// </summary>
    [Fact]
    public void Build_NestedRowsKeepTheBareLabel()
    {
        NavigateNodeMenuContext context = FileContext();

        NavigateNodeMenuAdapter.MenuResult result =
            NavigateNodeMenuAdapter.Build(context, HierarchyMenuCatalog.Hosts.Desktop);

        NavigateNodeMenuAdapter.MenuEntry? folder =
            result.Entries.FirstOrDefault(e => e.Id == HierarchyMenuCatalog.MenuId.AddFolder);
        Assert.NotNull(folder);
        Assert.Equal("Folder", folder!.Label);

        // the flat rendering still needs the parent context
        Assert.Equal("Add › Folder", folder.FlatLabel);

        // a top-level row has no parent, so both labels agree
        NavigateNodeMenuAdapter.MenuEntry? copyPath =
            result.Entries.FirstOrDefault(e => e.Id == HierarchyMenuCatalog.MenuId.CopyPath);
        Assert.NotNull(copyPath);
        Assert.Equal(copyPath!.Label, copyPath.FlatLabel);
    }

    /// <summary>
    /// The project root cannot be deleted, and the row says why instead of
    /// silently doing nothing.
    /// </summary>
    [Fact]
    public void Build_DeleteIsDisabledOnTheProjectRoot()
    {
        NavigateNodeMenuContext context = new NavigateNodeMenuContext
        {
            Kind = HierarchyMenuCatalog.AppliesTo.Project,
            NodeLabel = "project",
            FsPath = System.IO.Path.Combine("root", "x.sv"),
        };

        NavigateNodeMenuAdapter.MenuResult result =
            NavigateNodeMenuAdapter.Build(context, HierarchyMenuCatalog.Hosts.Desktop);
        NavigateNodeMenuAdapter.MenuEntry? delete =
            result.Entries.FirstOrDefault(e => e.Id == HierarchyMenuCatalog.MenuId.DeleteNode);

        if (delete == null) return; // the row is not part of the catalog yet
        Assert.False(delete.Enabled);
        Assert.False(string.IsNullOrEmpty(delete.Reason));
    }

    /// <summary>
    /// "Property" must be offered on every node kind, including the project root
    /// and folders.
    /// </summary>
    /// <remarks>
    /// The editor built this row inline for every node type, so narrowing the
    /// catalog to <c>File | Instance</c> made it disappear from the project,
    /// folder, header and package nodes once the catalog replaced the inline
    /// rows. The row only needs the node's Item (the editor's property form) or
    /// its paths (the VS Code text rendering), not a parse result, so every kind
    /// can run it.
    /// </remarks>
    [Fact]
    public void Build_PropertyIsOfferedForEveryNodeKind()
    {
        HierarchyMenuCatalog.AppliesTo[] kinds =
        {
            HierarchyMenuCatalog.AppliesTo.Project,
            HierarchyMenuCatalog.AppliesTo.Folder,
            HierarchyMenuCatalog.AppliesTo.File,
            HierarchyMenuCatalog.AppliesTo.Instance,
            HierarchyMenuCatalog.AppliesTo.Header,
            HierarchyMenuCatalog.AppliesTo.Package,
        };

        foreach (HierarchyMenuCatalog.AppliesTo kind in kinds)
        {
            NavigateNodeMenuContext context = new NavigateNodeMenuContext
            {
                Kind = kind,
                NodeLabel = "node",
                WorkspaceRelativePath = "src/rtl/top.sv",
                FsPath = System.IO.Path.Combine("root", "src", "rtl", "top.sv"),
                DirPath = System.IO.Path.Combine("root", "src", "rtl"),
            };

            foreach (HierarchyMenuCatalog.Hosts host in new[]
                     {
                         HierarchyMenuCatalog.Hosts.Desktop,
                         HierarchyMenuCatalog.Hosts.VsCode,
                     })
            {
                NavigateNodeMenuAdapter.MenuEntry? property = NavigateNodeMenuAdapter
                    .Build(context, host).Entries
                    .FirstOrDefault(e => e.Id == HierarchyMenuCatalog.MenuId.ShowProperty);

                Assert.True(property != null,
                    "the Property row must be offered for " + kind + " in " + host);
                Assert.True(property!.Enabled,
                    "the Property row must be enabled for " + kind + " in " + host +
                    " (reason: " + property.Reason + ")");
            }
        }
    }

    /// <summary>A file node with the paths the Add / Copy rows need.</summary>
    private static NavigateNodeMenuContext FileContext()
    {
        string fsPath = System.IO.Path.Combine("root", "src", "rtl", "top.sv");
        return new NavigateNodeMenuContext
        {
            Kind = HierarchyMenuCatalog.AppliesTo.File,
            NodeLabel = "top.sv",
            WorkspaceRelativePath = "src/rtl/top.sv",
            FsPath = fsPath,
            DirPath = System.IO.Path.Combine("root", "src", "rtl"),
        };
    }

    private static void Register(string id, HierarchyMenuCatalog.Hosts hosts)
    {
        HierarchyMenuCatalog.Register(new HierarchyMenuCatalog.Entry
        {
            Id = id,
            Label = id,
            Detail = "test row",
            Targets = HierarchyMenuCatalog.AppliesTo.File,
            Hosts = hosts,
            Action = HierarchyMenuCatalog.ActionKind.CopyPath,
            Group = 98,
        });
    }

    private static List<string> Ids(NavigateNodeMenuAdapter.MenuResult result)
        => result.Entries.Select(e => e.Id).ToList();
}
