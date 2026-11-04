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
// request per expanded node — `folderPath` for folders, `uri` + `nodePath`
// for file / instance nodes — so the server never has to serialise the whole
// workspace hierarchy at once.

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
        // sequence of child indexes from the workspace root
        this.nodePath = options.nodePath;
        // readable instance path, used by "Copy Instance Path"
        this.instancePath = options.instancePath;
        // workspace-relative folder path, used to expand a 'folder' node
        this.folderPath = options.folderPath;
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
        // set once the first top-level fetch has returned a non-empty tree, so
        // a folder-less window does not re-scan the disk on every redraw
        this.initialRescanDone = false;
        // guards the one-shot retry scheduled when the first render came back
        // empty because the server had no workspace root yet
        this.retryScheduled = false;
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
     * Re-fetches the tree only while it has never been populated. Used when a
     * SystemVerilog document is opened: the server may have had no workspace
     * root when the view first rendered, so the first fetch came back empty.
     * Once the tree has content this is a no-op (a full rescan on every
     * document open would be wasteful on a large workspace).
     */
    refreshIfEmpty() {
        if (this.initialRescanDone) return;
        // Defer: this is called from inside the LSP initialize promise chain,
        // and getChildren awaits ensureClient() (the same promise). Firing the
        // tree change synchronously would make the re-fetch wait on a promise
        // that only resolves after the current callback returns.
        setTimeout(() => {
            if (!this.initialRescanDone) this.refresh();
        }, 0);
    }

    getTreeItem(element) {
        if (element.kind === 'info') {
            // Diagnostic node shown when the tree would otherwise be empty, so
            // the user (and the log) can see WHY nothing was listed instead of
            // a silent blank view.
            const item = new vscode.TreeItem(
                element.label,
                vscode.TreeItemCollapsibleState.None
            );
            item.iconPath = new vscode.ThemeIcon('warning');
            item.contextValue = 'rtlInfo';
            item.tooltip = element.detail || element.label;
            return item;
        }

        if (element.kind === 'folder') {
            const item = new vscode.TreeItem(
                element.label,
                element.hasChildren
                    ? vscode.TreeItemCollapsibleState.Collapsed
                    : vscode.TreeItemCollapsibleState.None
            );
            item.iconPath = vscode.ThemeIcon.Folder;
            item.contextValue = 'rtlFolder';
            item.tooltip = element.folderPath;
            return item;
        }

        if (element.kind === 'file') {
            const item = new vscode.TreeItem(
                element.label,
                element.hasChildren
                    ? vscode.TreeItemCollapsibleState.Collapsed
                    : vscode.TreeItemCollapsibleState.None
            );
            // resourceUri drives the real file icon; unknown schemes (a file the
            // server could not turn into a URI) fall back to the generic one
            try {
                item.resourceUri = vscode.Uri.parse(element.uri);
            } catch (e) {
                item.iconPath = vscode.ThemeIcon.File;
            }
            item.description = element.detail || undefined;
            item.contextValue = 'rtlFile';
            item.tooltip = element.uri;
            // opening the file itself is the default action for leaf files
            item.command = {
                command: 'systemverilogLsp.openHierarchyNode',
                title: 'Open',
                arguments: [element],
            };
            return item;
        }

        const item = new vscode.TreeItem(
            element.label,
            element.hasChildren
                ? vscode.TreeItemCollapsibleState.Collapsed
                : vscode.TreeItemCollapsibleState.None
        );
        item.iconPath = new vscode.ThemeIcon('symbol-class');
        item.description = element.detail || undefined;
        item.contextValue = 'rtlInstance';
        item.tooltip = element.detail
            ? `${element.instancePath}  (${element.detail})`
            : element.instancePath;
        item.command = {
            command: 'systemverilogLsp.revealInEditor',
            title: 'Reveal in Editor',
            arguments: [element],
        };
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
                // top level: the whole workspace (folder tree + files)
                const params = { nodePath: [] };
                // A window that has no folder open ("File > Open File") sends no
                // workspace root at initialize, so the server cannot scan the
                // project. Ask for a rescan on the first render: the server then
                // infers the root from the opened document instead of showing
                // only that one file.
                const noFolderOpen = !(vscode.workspace.workspaceFolders || []).length;
                if (this.pendingRescan || (noFolderOpen && !this.initialRescanDone)) {
                    params.rescan = true;
                    this.pendingRescan = false;
                }
                const result = await client.request('rtl/hierarchy', params);
                const items = this._toItems(result, [], '');
                // Only latch the "initial rescan done" flag once the server
                // actually returned something. The view is created (and
                // getChildren runs) as soon as the extension activates, which
                // can be before any SystemVerilog document has been opened: the
                // server then has no workspace root yet and answers with an
                // empty list. Latching on that empty answer left the tree blank
                // forever, because nothing re-triggered a rescan afterwards.
                if (items.length > 0) {
                    this.initialRescanDone = true;
                } else if (noFolderOpen && !this.retryScheduled) {
                    // The server had no workspace root yet (no folder open and
                    // no SystemVerilog document opened so far). Retry once the
                    // client has had a chance to open a document, so the tree
                    // fills in without the user pressing refresh.
                    this.retryScheduled = true;
                    setTimeout(() => {
                        this.retryScheduled = false;
                        this.refresh();
                    }, 2000);
                }
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
                        nodePath: [],
                    })];
                }
                return items;
            }

            if (!element.hasChildren) return [];

            if (element.kind === 'folder') {
                const result = await client.request('rtl/hierarchy', {
                    folderPath: element.folderPath,
                });
                return this._toItems(result, [], '');
            }

            if (element.kind === 'file') {
                const result = await client.request('rtl/hierarchy', {
                    uri: element.uri,
                    nodePath: [],
                });
                return this._toItems(result, [], '');
            }

            const result = await client.request('rtl/hierarchy', {
                uri: element.uri,
                nodePath: element.nodePath,
            });
            return this._toItems(result, element.nodePath, element.instancePath);
        } catch (err) {
            this.lastError = err;
            this.log('getChildren ERROR: ' + (err && err.stack ? err.stack : err));
            throw err;
        }
    }

    _toItems(result, parentPath, parentInstancePath) {
        const nodes = (result && result.nodes) || [];
        return nodes.map((node, index) => {
            const nodePath = parentPath.concat([index]);
            const kind = node.kind || 'instance';
            const label = node.label || '';
            const instancePath = kind === 'instance'
                ? (parentInstancePath ? parentInstancePath + '.' + label : label)
                : '';
            return new HierarchyItem({
                label,
                kind,
                uri: node.uri,
                detail: node.detail,
                startLine: node.startLine,
                startCharacter: node.startCharacter,
                hasChildren: !!node.hasChildren,
                nodePath,
                instancePath,
                folderPath: node.folderPath,
            });
        });
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
