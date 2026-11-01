'use strict';

// RTL Hierarchy tree view (NavigatePanel equivalent).
//
// VS Code's built-in File Explorer cannot be extended with extra child nodes
// (there is no provider API for it), so the instance hierarchy is rendered in
// its own Custom Tree View placed inside the Explorer container. The tree is
// lazy: one `rtl/hierarchy` request per expanded node, addressed by a
// "nodePath" (the sequence of child indexes from the workspace root).

const vscode = require('vscode');
const path = require('path');

/** One node of the RTL hierarchy tree. */
class HierarchyItem {
    constructor(options) {
        this.label = options.label;
        // 'file' (workspace file) | 'instance' (building block instance)
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
    }
}

class HierarchyProvider {
    /**
     * @param {() => Promise<any>} ensureClient resolves the LSP client
     */
    constructor(ensureClient) {
        this.ensureClient = ensureClient;
        this._onDidChangeTreeData = new vscode.EventEmitter();
        this.onDidChangeTreeData = this._onDidChangeTreeData.event;
        this.lastError = null;
    }

    refresh() {
        this.lastError = null;
        this._onDidChangeTreeData.fire(undefined);
    }

    getTreeItem(element) {
        if (element.kind === 'file') {
            const item = new vscode.TreeItem(
                element.label,
                element.hasChildren
                    ? vscode.TreeItemCollapsibleState.Collapsed
                    : vscode.TreeItemCollapsibleState.None
            );
            item.iconPath = vscode.ThemeIcon.File;
            item.description = element.detail || undefined;
            item.resourceUri = vscode.Uri.parse(element.uri);
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
            const client = await this.ensureClient();
            if (!client) return [];

            if (element === undefined) {
                // top level: workspace file list
                const result = await client.request('rtl/hierarchy', { nodePath: [] });
                return this._toItems(result, [], '');
            }

            if (!element.hasChildren) return [];

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
            });
        });
    }
}

/**
 * Registers the tree view plus its commands.
 * @param {vscode.ExtensionContext} context
 * @param {() => Promise<any>} ensureClient
 */
function registerHierarchy(context, ensureClient) {
    const provider = new HierarchyProvider(ensureClient);
    const view = vscode.window.createTreeView('systemverilogLsp.hierarchy', {
        treeDataProvider: provider,
        showCollapseAll: true,
    });
    context.subscriptions.push(view);
    context.subscriptions.push(provider._onDidChangeTreeData);

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.refreshHierarchy', () => provider.refresh())
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.openHierarchyNode', async (item) => {
            const node = item || view.selection[0];
            if (!node || !node.uri) return;
            await openAt(node.uri, node.startLine, node.startCharacter);
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.revealInEditor', async (item) => {
            const node = item || view.selection[0];
            if (!node || !node.uri) return;
            await openAt(node.uri, node.startLine, node.startCharacter);
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
