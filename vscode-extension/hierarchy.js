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
const path = require('path');

// ---- icons -------------------------------------------------------------
//
// The tree uses the plugin's own SVG assets (copied into icons/) so the view
// matches the desktop editor's NavigatePanel. Each icon ships in a light and a
// dark variant: the plugin SVGs are filled with a fixed dark grey (#4b4b4b),
// which is invisible on a dark theme, so the dark variant uses a light grey.
//
// The server sends a stable icon *name* ("module" / "interface" / "package" /
// "header" / "systemverilog-document" / "verilog-document" /
// "verilog-header-document"); the client maps it onto the two files. An
// unknown name falls back to a codicon so a new server-side kind still renders
// something.
const ICON_NAMES = new Set([
    'module',
    'interface',
    'package',
    'header',
    'systemverilog-document',
    'verilog-document',
    'verilog-header-document',
]);

// codicon fallbacks for names the client does not ship an SVG for
const ICON_FALLBACKS = {
    folder: 'folder',
    file: 'file',
    module: 'symbol-module',
    interface: 'symbol-interface',
    package: 'symbol-package',
    header: 'file-code',
    'systemverilog-document': 'file-code',
    'verilog-document': 'file-code',
    'verilog-header-document': 'file-code',
};

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
     * @param {vscode.Uri} [extensionUri] root of the extension (for icons/)
     */
    constructor(ensureClient, log, revealLog, extensionUri) {
        this.ensureClient = ensureClient;
        this.extensionUri = extensionUri;
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

        // The server decides the icon name and (optionally) a theme colour id
        // used to flag files with errors / warnings.
        const iconColor = element.iconColor
            ? new vscode.ThemeColor(element.iconColor)
            : undefined;

        const iconPath = this._resolveIconPath(element.icon, iconColor);
        if (iconPath) item.iconPath = iconPath;

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

    /**
     * Maps the server's icon name onto a VS Code icon.
     *
     * The plugin's own SVGs are shipped in icons/ (light + dark variants) so
     * the tree matches the desktop editor. When the name has no shipped SVG
     * (a folder, or a kind added on the server later) a codicon is used, which
     * also carries the error / warning theme colour.
     */
    _resolveIconPath(iconName, iconColor) {
        if (iconName && ICON_NAMES.has(iconName) && this.extensionUri) {
            return {
                light: vscode.Uri.joinPath(this.extensionUri, 'icons', iconName + '.svg'),
                dark: vscode.Uri.joinPath(this.extensionUri, 'icons', iconName + '-dark.svg'),
            };
        }
        const codicon = ICON_FALLBACKS[iconName] || iconName || 'file';
        return new vscode.ThemeIcon(codicon, iconColor);
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
    const provider = new HierarchyProvider(ensureClient, log, revealLog, context.extensionUri);
    const view = vscode.window.createTreeView('systemverilogLsp.hierarchy', {
        treeDataProvider: provider,
        showCollapseAll: true,
    });
    context.subscriptions.push(view);
    context.subscriptions.push(provider._onDidChangeTreeData);

    // Clicking a node must BOTH switch the editor to the node's declaration and
    // re-parse its file (so the hierarchy and its errors are fresh).
    //
    // Relying on TreeItem.command alone does not work for expandable nodes:
    // VS Code toggles their expansion on click and does NOT run the command.
    // Module instances are exactly that case (they have children), so clicking
    // one only expanded it and the editor never switched. Selection changes on
    // every click, so drive both actions here.
    //
    // `revealing` guards against re-entrancy: reparseAndRefresh() ends with
    // refreshTree(), which can change the selection again and would otherwise
    // bounce between "open" and "refresh" forever.
    let revealing = false;
    context.subscriptions.push(view.onDidChangeSelection(async (e) => {
        const node = e.selection && e.selection.length > 0 ? e.selection[0] : null;
        if (!node || !node.uri) return;
        if (revealing) return;
        revealing = true;
        try {
            // Log the exact target: the server sends the file the instantiation
            // is WRITTEN IN (uri) plus the instantiated module's own source
            // (fsPath). If the editor opens the wrong file, this line shows
            // which uri the client actually used.
            provider.log('selection: ' + node.kind + ' ' + node.label
                + ' -> uri=' + node.uri
                + ' line=' + node.startLine + ' char=' + node.startCharacter
                + ' (fsPath=' + node.fsPath + ')');
            await openAt(node.uri, node.startLine, node.startCharacter, provider.log);
            await provider.reparseAndRefresh(node.uri);
        } catch (err) {
            provider.log('selection reveal/reparse ERROR: ' + (err && err.message));
        } finally {
            revealing = false;
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
            await openAt(node.uri, node.startLine, node.startCharacter, provider.log);
            await provider.reparseAndRefresh(node.uri);
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.revealInEditor', async (item) => {
            const node = item || view.selection[0];
            if (!node || !node.uri) return;
            await openAt(node.uri, node.startLine, node.startCharacter, provider.log);
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
async function openAt(uriString, line, character, log) {
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
        if (log) log('openAt: opened ' + doc.uri.toString() + ' at ' + line + ':' + character);
    } catch (err) {
        vscode.window.showErrorMessage('Could not open ' + uriString + ': ' + err.message);
    }
}

module.exports = { registerHierarchy, HierarchyProvider, HierarchyItem };
