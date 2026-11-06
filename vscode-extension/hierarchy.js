'use strict';

// RTL Hierarchy tree view (NavigatePanel equivalent).
//
// VS Code's built-in File Explorer cannot be extended with extra child nodes
// (there is no provider API for it), so the instance hierarchy is rendered in
// its own Custom Tree View placed inside the Explorer container.
//
// The top level is the WHOLE workspace (every .sv/.svh/.v/.vh file found under
// the workspace roots by the server), grouped into folders, not just the
// documents that happen to be open. The tree is lazy: one `rtl/hierarchy`
// request per expanded node, carrying the opaque `expandToken` the server
// issued for that node (no token = the workspace root), so the server never
// has to serialise the whole workspace hierarchy at once and the client never
// builds a path or an index itself.

const vscode = require('vscode');

/** One node of the RTL hierarchy tree. */
class HierarchyItem {
    constructor(options) {
        this.label = options.label;
        // 'folder' (workspace folder) | 'file' (workspace file)
        // | 'instance' (building block instance)
        this.kind = options.kind;
        this.uri = options.uri;
        this.detail = options.detail;
        this.startLine = options.startLine;
        this.startCharacter = options.startCharacter;
        this.hasChildren = options.hasChildren;
        // Opaque token the server issued for this node. The client stores it
        // and echoes it back verbatim to expand the node; it never parses it.
        this.expandToken = options.expandToken;
        // readable instance path, used by "Copy Instance Path"
        this.instancePath = options.instancePath;
        // workspace-relative folder path (informational; expansion uses the token)
        this.folderPath = options.folderPath;
        // ---- presentation metadata, decided by the server ----
        // The client only maps these onto the VS Code TreeItem; it never
        // derives them from `kind` / `hasChildren` itself.
        this.icon = options.icon;
        // optional VS Code theme colour id ("errorForeground" /
        // "editorWarning.foreground") used to flag files with errors/warnings
        this.iconColor = options.iconColor;
        this.contextValue = options.contextValue;
        this.collapsible = options.collapsible;
        this.tooltip = options.tooltip;
        // absolute paths for the file operations (Add / Delete / Open in
        // Explorer), resolved by the server
        this.fsPath = options.fsPath;
        this.dirPath = options.dirPath;
    }
}

class HierarchyProvider {
    /**
     * @param {() => Promise<any>} ensureClient resolves the LSP client
     * @param {(msg: string) => void} [log] optional logger (OutputChannel)
     * @param {() => void} [revealLog] optional: reveal the log output channel
     */
    constructor(ensureClient, log, revealLog) {
        this.ensureClient = ensureClient;
        this.log = log || (() => {});
        this.revealLog = revealLog || (() => {});
        // set once we auto-revealed the log for an empty top level, so we do
        // not steal focus on every redraw
        this.revealedLogOnce = false;
        this._onDidChangeTreeData = new vscode.EventEmitter();
        this.onDidChangeTreeData = this._onDidChangeTreeData.event;
        this.lastError = null;
        // set by refresh(): the next top-level fetch asks the server to
        // re-scan the workspace roots from disk (picking up files created or
        // deleted since initialize). Plain expands skip the rescan so the tree
        // does not enumerate the whole workspace on every render.
        this.pendingRescan = false;
        // The server pushes rtl/hierarchyInvalidated when the workspace root
        // becomes known (or files change), so the client no longer needs the
        // "retry after N seconds" / "latch once non-empty" timers that used to
        // live here: the redraw is driven by the server.
    }

    /**
     * Re-reads the whole workspace from disk (files created / deleted since the
     * last scan) and redraws the tree. Bound to the view's refresh button.
     */
    refresh() {
        this.lastError = null;
        this.pendingRescan = true;
        this._onDidChangeTreeData.fire(undefined);
    }

    /**
     * Redraws the tree without touching the disk, for events that only changed
     * the parse result of files already known to the server (e.g. didSave).
     */
    refreshTree() {
        this.lastError = null;
        this._onDidChangeTreeData.fire(undefined);
    }

    /**
     * Asks the server to re-parse the given file and then redraws the tree, so
     * clicking a hierarchy node updates the instance list even before the
     * client sends a didChange (an already-open, unedited file otherwise keeps
     * the parse result from when it was first opened).
     */
    async reparseAndRefresh(uri) {
        try {
            const client = await this.ensureClient();
            if (client && uri) {
                await client.request('rtl/reparse', { uri });
            }
        } catch (e) {
            this.log('reparse ERROR: ' + (e && e.stack ? e.stack : e));
        }
        this.refreshTree();
    }

    /**
     * Redraws the tree once the LSP session is ready.
     *
     * <p>
     * The view is created as soon as the extension activates, which can be
     * before the client has started: the first fetch then comes back empty.
     * The server also pushes <c>rtl/hierarchyInvalidated</c> when it infers a
     * workspace root, so this is only the "session just became ready" nudge.
     * </p>
     *
     * <p>
     * Deferred with setTimeout(0): this is called from inside the LSP
     * initialize promise chain, and getChildren awaits ensureClient() (the same
     * promise). Firing the tree change synchronously would make the re-fetch
     * wait on a promise that only resolves after the current callback returns.
     * </p>
     */
    refreshIfEmpty() {
        setTimeout(() => this.refresh(), 0);
    }

    /**
     * Maps a server descriptor onto a VS Code TreeItem.
     *
     * The server decides the icon, contextValue, collapsible state, tooltip and
     * the absolute paths; this method only translates them into the VS Code
     * types. The only client-side special case is the file icon: a real
     * `resourceUri` gives the proper file-type icon, which the server cannot
     * express as a ThemeIcon id.
     */
    getTreeItem(element) {
        const collapsible = element.collapsible === 'collapsed'
            ? vscode.TreeItemCollapsibleState.Collapsed
            : vscode.TreeItemCollapsibleState.None;
        const item = new vscode.TreeItem(element.label, collapsible);

        if (element.contextValue) item.contextValue = element.contextValue;
        if (element.tooltip) item.tooltip = element.tooltip;
        if (element.detail) item.description = element.detail;

        // The server decides the codicon id and (optionally) a theme colour id
        // used to flag files with errors / warnings. Codicons are used instead
        // of the plugin's own SVG assets because those are filled with a fixed
        // dark grey and would be invisible on a dark theme.
        const iconColor = element.iconColor
            ? new vscode.ThemeColor(element.iconColor)
            : undefined;

        if (element.kind === 'file' && element.uri) {
            // resourceUri drives the real file-type icon. A file with errors /
            // warnings keeps that icon but is tinted with the theme colour, so
            // the tree flags broken files like the editor's NavigatePanel.
            try {
                item.resourceUri = vscode.Uri.parse(element.uri);
                if (iconColor) {
                    item.iconPath = new vscode.ThemeIcon('file', iconColor);
                }
            } catch (e) {
                item.iconPath = new vscode.ThemeIcon(element.icon || 'file', iconColor);
            }
        } else if (element.icon) {
            item.iconPath = new vscode.ThemeIcon(element.icon, iconColor);
        }

        // default click action: open the file / reveal the instance
        if (element.kind === 'file') {
            item.command = {
                command: 'systemverilogLsp.openHierarchyNode',
                title: 'Open',
                arguments: [element],
            };
        } else if (element.kind === 'instance') {
            item.command = {
                command: 'systemverilogLsp.revealInEditor',
                title: 'Reveal in Editor',
                arguments: [element],
            };
        }
        return item;
    }

    async getChildren(element) {
        try {
            this.log('getChildren: ' + (element === undefined ? '<root>' : element.kind + ':' + element.label));
            const client = await this.ensureClient();
            if (!client) {
                this.log('getChildren: no client');
                return [];
            }

            if (element === undefined) {
                // top level: the whole workspace (folder tree + files). No
                // expandToken means "the workspace root".
                const params = {};
                // A window that has no folder open ("File > Open File") sends no
                // workspace root at initialize, so the server cannot scan the
                // project. Ask for a rescan on the first render: the server then
                // infers the root from the opened document instead of showing
                // only that one file. Once the root is known the server pushes
                // rtl/hierarchyInvalidated, so no client-side retry timer is
                // needed here.
                const noFolderOpen = !(vscode.workspace.workspaceFolders || []).length;
                if (this.pendingRescan || noFolderOpen) {
                    params.rescan = true;
                    this.pendingRescan = false;
                }
                const result = await client.request('rtl/hierarchy', params);
                const items = this._toItems(result);
                this.log('hierarchy top level: ' + items.length + ' node(s)'
                    + (params.rescan ? ' (rescan)' : ''));
                if (items.length === 0) {
                    // Never leave the view silently blank: surface the reason
                    // as a single diagnostic node, and reveal the log once so
                    // the user can see what the server actually did.
                    if (!this.revealedLogOnce) {
                        this.revealedLogOnce = true;
                        this.revealLog();
                    }
                    return [new HierarchyItem({
                        label: 'No SystemVerilog files found (click refresh)',
                        kind: 'info',
                        detail: 'The server returned an empty workspace tree. '
                            + 'Open a folder containing .sv/.svh/.v/.vh files, '
                            + 'or press the refresh button in the view title.',
                        hasChildren: false,
                        icon: 'warning',
                        contextValue: 'rtlInfo',
                        collapsible: 'none',
                        tooltip: 'The server returned an empty workspace tree.',
                    })];
                }
                return items;
            }

            if (!element.hasChildren) return [];
            if (!element.expandToken) {
                // a leaf, or a node the server did not mark expandable
                return [];
            }

            // The server issued an opaque token for this node: echo it back
            // verbatim. The client never builds a path or an index itself.
            const result = await client.request('rtl/hierarchy', {
                expandToken: element.expandToken,
            });
            return this._toItems(result);
        } catch (err) {
            this.lastError = err;
            this.log('getChildren ERROR: ' + (err && err.stack ? err.stack : err));
            throw err;
        }
    }

    /**
     * Maps the server's node descriptors onto HierarchyItems.
     *
     * Everything the tree needs (label, icon, contextValue, collapsible state,
     * tooltip, instance path, absolute paths, expand token) comes from the
     * server, so this is a straight field copy: no path building, no index
     * bookkeeping.
     */
    _toItems(result) {
        const nodes = (result && result.nodes) || [];
        return nodes.map((node) => new HierarchyItem({
            label: node.label || '',
            kind: node.kind || 'instance',
            uri: node.uri,
            detail: node.detail,
            startLine: node.startLine,
            startCharacter: node.startCharacter,
            hasChildren: !!node.hasChildren,
            expandToken: node.expandToken,
            instancePath: node.instancePath,
            folderPath: node.folderPath,
            icon: node.icon,
            iconColor: node.iconColor,
            contextValue: node.contextValue,
            collapsible: node.collapsible,
            tooltip: node.tooltip,
            fsPath: node.fsPath,
            dirPath: node.dirPath,
        }));
    }
}

/**
 * Registers the tree view plus its commands.
 * @param {vscode.ExtensionContext} context
 * @param {() => Promise<any>} ensureClient
 * @param {(msg: string) => void} [log] optional logger (OutputChannel)
 */
function registerHierarchy(context, ensureClient, log, revealLog) {
    const provider = new HierarchyProvider(ensureClient, log, revealLog);
    const view = vscode.window.createTreeView('systemverilogLsp.hierarchy', {
        treeDataProvider: provider,
        showCollapseAll: true,
    });
    context.subscriptions.push(view);
    context.subscriptions.push(provider._onDidChangeTreeData);

    // Clicking a node must re-parse its file and refresh the tree. Relying on
    // TreeItem.command alone does not work for expandable nodes: VS Code toggles
    // their expansion on click and does NOT run the command, so the hierarchy
    // (and the errors under it) stayed stale until an edit happened to re-parse
    // the file. Selection changes on every click, so drive the re-parse here.
    context.subscriptions.push(view.onDidChangeSelection(async (e) => {
        const node = e.selection && e.selection.length > 0 ? e.selection[0] : null;
        if (!node || !node.uri) return;
        try {
            await provider.reparseAndRefresh(node.uri);
        } catch (err) {
            provider.log('selection reparse ERROR: ' + (err && err.message));
        }
    }));
    // The view title shows "No ... files" (viewsWelcome) only when the provider
    // returns nothing; keep the message visible by revealing the view itself.
    try { view.badge = undefined; } catch (e) { /* ignore */ }

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.refreshHierarchy', () => provider.refresh())
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.openHierarchyNode', async (item) => {
            const node = item || view.selection[0];
            if (!node || !node.uri) return;
            await openAt(node.uri, node.startLine, node.startCharacter);
            await provider.reparseAndRefresh(node.uri);
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.revealInEditor', async (item) => {
            const node = item || view.selection[0];
            if (!node || !node.uri) return;
            await openAt(node.uri, node.startLine, node.startCharacter);
            await provider.reparseAndRefresh(node.uri);
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.copyHierarchyPath', async (item) => {
            const node = item || view.selection[0];
            if (!node || !node.instancePath) return;
            await vscode.env.clipboard.writeText(node.instancePath);
            vscode.window.setStatusBarMessage('Copied: ' + node.instancePath, 3000);
        })
    );

    // ---- file system operations (mirror the editor's NavigatePanel menu) ----
    // These act on the on-disk file / folder the node represents, then ask the
    // server to re-scan so the tree (and parse results) reflect the change.

    // The server resolves the absolute paths (fsPath / dirPath) for every node,
    // so the client never joins paths itself. The uri fallback only covers an
    // older server that does not send them yet.
    const resolveFsPath = (node) => {
        if (!node) return undefined;
        if (node.fsPath) return node.fsPath;
        if (node.uri) {
            try { return vscode.Uri.parse(node.uri).fsPath; } catch (e) { /* ignore */ }
        }
        return undefined;
    };
    // folder the node lives in (for Add): the server sends it as dirPath
    const resolveDirPath = (node) => {
        if (!node) return undefined;
        if (node.dirPath) return node.dirPath;
        const fsPath = resolveFsPath(node);
        if (fsPath) return require('path').dirname(fsPath);
        return undefined;
    };

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.addFolder', async (item) => {
            const node = item || view.selection[0];
            const dir = resolveDirPath(node);
            if (!dir) return;
            const name = await vscode.window.showInputBox({ prompt: 'New folder name' });
            if (!name) return;
            try {
                await vscode.workspace.fs.createDirectory(vscode.Uri.file(require('path').join(dir, name)));
                provider.refresh();
            } catch (e) {
                vscode.window.showErrorMessage('Add Folder failed: ' + e.message);
            }
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.addBlankFile', async (item) => {
            const node = item || view.selection[0];
            const dir = resolveDirPath(node);
            if (!dir) return;
            const name = await vscode.window.showInputBox({ prompt: 'New file name (e.g. top.sv)' });
            if (!name) return;
            try {
                const uri = vscode.Uri.file(require('path').join(dir, name));
                await vscode.workspace.fs.writeFile(uri, new Uint8Array());
                await openAt(uri.toString(), 0, 0);
                provider.refresh();
            } catch (e) {
                vscode.window.showErrorMessage('Add File failed: ' + e.message);
            }
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.deleteNode', async (item) => {
            const node = item || view.selection[0];
            const fsPath = resolveFsPath(node);
            if (!fsPath) return;
            const ok = await vscode.window.showWarningMessage(
                'Delete ' + require('path').basename(fsPath) + '?', { modal: true }, 'Delete');
            if (ok !== 'Delete') return;
            try {
                await vscode.workspace.fs.delete(vscode.Uri.file(fsPath), { recursive: true, useTrash: true });
                provider.refresh();
            } catch (e) {
                vscode.window.showErrorMessage('Delete failed: ' + e.message);
            }
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.copyPath', async (item) => {
            const node = item || view.selection[0];
            // prefer the workspace-relative path (detail) like the editor does,
            // fall back to the absolute fs path
            const text = (node && node.detail) ? node.detail : resolveFsPath(node);
            if (!text) return;
            await vscode.env.clipboard.writeText(text);
            vscode.window.setStatusBarMessage('Copied: ' + text, 3000);
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.openInExplorer', async (item) => {
            const node = item || view.selection[0];
            const fsPath = resolveFsPath(node);
            if (!fsPath) return;
            try {
                await vscode.commands.executeCommand('revealFileInOS', vscode.Uri.file(fsPath));
            } catch (e) {
                vscode.window.showErrorMessage('Open in Explorer failed: ' + e.message);
            }
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.showProperty', async (item) => {
            const node = item || view.selection[0];
            if (!node) return;
            const lines = [
                'label: ' + (node.label || ''),
                'kind: ' + (node.kind || ''),
                'uri: ' + (node.uri || ''),
                'detail: ' + (node.detail || ''),
                'instancePath: ' + (node.instancePath || ''),
                'startLine: ' + (node.startLine == null ? '' : node.startLine),
                'startCharacter: ' + (node.startCharacter == null ? '' : node.startCharacter),
            ];
            await vscode.window.showInformationMessage(lines.join('\n'), { modal: true });
        })
    );

    return provider;
}

/**
 * Opens the given file and reveals the given 0-based line/character.
 * Falls back gracefully when the position is unknown.
 */
async function openAt(uriString, line, character) {
    const uri = vscode.Uri.parse(uriString);
    try {
        const doc = await vscode.workspace.openTextDocument(uri);
        const editor = await vscode.window.showTextDocument(doc, { preview: false });
        if (typeof line === 'number' && line >= 0) {
            const pos = new vscode.Position(line, typeof character === 'number' ? character : 0);
            const range = new vscode.Range(pos, pos);
            editor.selection = new vscode.Selection(pos, pos);
            editor.revealRange(range, vscode.TextEditorRevealType.InCenterIfOutsideViewport);
        }
    } catch (err) {
        vscode.window.showErrorMessage('Could not open ' + uriString + ': ' + err.message);
    }
}

module.exports = { registerHierarchy, HierarchyProvider, HierarchyItem };
