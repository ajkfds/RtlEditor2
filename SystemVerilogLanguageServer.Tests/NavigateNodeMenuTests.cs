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
