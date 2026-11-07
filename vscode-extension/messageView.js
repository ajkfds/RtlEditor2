'use strict';

// Message view (RtlEditor2's MessageView equivalent) shown in the Auxiliary
// Bar, i.e. a pane on the right-hand side of the editor.
//
// The editor's MessageView lists the parse messages of the current document
// (Error / Warning / Notice / Hint) with a severity icon and the line number,
// and clicking an entry jumps to it. VS Code already receives exactly that
// data through the standard `textDocument/publishDiagnostics` notification, so
// this view is a *renderer* for diagnostics the extension already holds: no
// extra server protocol is involved and the LSP standard is left untouched.
//
// Responsibilities follow memory/vscode_extension_architecture.md:
//   - "what to show" (severity, message, position) comes from the server's
//     diagnostics; this file does not invent or re-derive it.
//   - "how to show it" (HTML/CSS) and "VS Code API calls" (reveal a range) are
//     the client's job, which is what lives here.

const vscode = require('vscode');

// Severity order used for sorting: errors first, like the editor's list.
const SEVERITY_ORDER = { 1: 0, 2: 1, 3: 2, 4: 3 };

/**
 * Stable key for a document URI.
 *
 * The server canonicalises URIs before publishing diagnostics
 * (`LspHandler.NormalizeUri` -> `file:///C:/dir/x.sv`), while VS Code hands us
 * the form it uses on the wire (`file:///c%3A/dir/x.sv`: lower-case drive
 * letter, colon percent-encoded). Keying the diagnostics map on the raw string
 * therefore produced two entries for one file, so the "current file" scope
 * (which looks the active document up by key) never matched and stayed empty
 * while "whole workspace" (which lists every key) showed the messages.
 *
 * Keying on the normalised filesystem path makes both forms the same entry.
 * Non-file schemes (untitled:, ...) keep their raw string.
 * @param {string} uriString
 * @returns {string}
 */
function canonicalKey(uriString) {
    if (!uriString) return '';
    try {
        const uri = vscode.Uri.parse(uriString);
        if (uri.scheme !== 'file') return uriString;
        const fsPath = uri.fsPath;
        // Windows paths are case-insensitive, so the drive letter / path case
        // must not split the entry.
        return process.platform === 'win32' ? fsPath.toLowerCase() : fsPath;
    } catch (e) {
        return uriString;
    }
}

/**
 * One entry of the message list, flattened from a vscode.Diagnostic.
 * @typedef {{
 *   uri: string, fileName: string, line: number, character: number,
 *   endLine: number, endCharacter: number,
 *   severity: number, message: string, code: string
 * }} MessageEntry
 */

/**
 * Renders the diagnostics of the workspace (or of the active file) in a
 * webview placed in the Auxiliary Bar.
 */
class MessageViewProvider {
    /**
     * @param {vscode.Uri} extensionUri root of the extension (for local resources)
     * @param {(msg: string) => void} [log] optional logger
     */
    constructor(extensionUri, log) {
        this.extensionUri = extensionUri;
        this.log = log || (() => {});
        this.view = null;
        // canonicalKey(uri) -> { uri, diagnostics } as last published by the
        // server. The key is the normalised filesystem path so the server's
        // canonical form and VS Code's wire form map to one entry (see
        // canonicalKey); `uri` keeps the form the server sent, which is what
        // the reveal request has to echo back.
        this.diagnosticsByUri = new Map();
        // 'active' (the file in the editor) or 'workspace' (every parsed file)
        this.scope = 'active';
        // the file the view currently lists (only meaningful for scope=active):
        // `activeUri` is the raw URI (for display) and `activeKey` the
        // canonicalKey used to look the diagnostics up.
        this.activeUri = null;
        this.activeKey = null;
    }

    /** Called by VS Code when the view is created (and again on restore). */
    resolveWebviewView(webviewView) {
        this.view = webviewView;
        webviewView.webview.options = {
            enableScripts: true,
            localResourceRoots: [this.extensionUri],
        };
        webviewView.webview.html = this._buildHtml();

        // Messages from the webview: a click on an entry, or a scope change.
        webviewView.webview.onDidReceiveMessage((message) => {
            if (!message || typeof message.type !== 'string') return;
            if (message.type === 'reveal') {
                this._reveal(message.uri, message.line, message.character,
                    message.endLine, message.endCharacter);
            } else if (message.type === 'setScope') {
                this.scope = message.scope === 'workspace' ? 'workspace' : 'active';
                this._postState();
            } else if (message.type === 'ready') {
                // the webview finished loading: send the current state
                this._postState();
            }
        });

        // Keep the list in sync with the editor: the "active file" scope has to
        // follow the focused document.
        webviewView.onDidChangeVisibility(() => {
            if (webviewView.visible) this._postState();
        });

        this._postState();
    }

    /**
     * Records the diagnostics the server published for one document and
     * refreshes the view.
     * @param {string} uriString
     * @param {vscode.Diagnostic[]} diagnostics
     */
    update(uriString, diagnostics) {
        if (!uriString) return;
        const key = canonicalKey(uriString);
        if (!diagnostics || diagnostics.length === 0) {
            this.diagnosticsByUri.delete(key);
        } else {
            this.diagnosticsByUri.set(key, { uri: uriString, diagnostics });
        }
        this._postState();
    }

    /** Drops the diagnostics of a document that was closed. */
    remove(uriString) {
        if (uriString && this.diagnosticsByUri.delete(canonicalKey(uriString))) {
            this._postState();
        }
    }

    /** Follows the focused editor (used by the "active file" scope). */
    setActiveUri(uriString) {
        const key = uriString ? canonicalKey(uriString) : null;
        if (this.activeKey === key) return;
        this.activeKey = key;
        this.activeUri = uriString || null;
        if (this.scope === 'active') this._postState();
    }

    /**
     * Reveals the view (used by the "Show Messages" command).
     *
     * VS Code generates `<viewId>.focus` for every contributed view, so the
     * command below both opens the Auxiliary Bar and focuses this view. The
     * fallback only covers a host that does not know the view.
     */
    async show() {
        try {
            await vscode.commands.executeCommand('systemverilogLsp.messageView.focus');
        } catch (e) {
            try { await vscode.commands.executeCommand('workbench.action.toggleAuxiliaryBar'); } catch (_) { /* ignore */ }
        }
    }

    // ---- internals ------------------------------------------------------

    /**
     * Builds the flat list of entries for the current scope, sorted by file
     * (workspace scope) and then by position, with errors first inside a file.
     * @returns {MessageEntry[]}
     */
    _collectEntries() {
        const entries = [];
        const keys = this.scope === 'active'
            ? (this.activeKey ? [this.activeKey] : [])
            : Array.from(this.diagnosticsByUri.keys());

        for (const key of keys) {
            const record = this.diagnosticsByUri.get(key);
            if (!record) continue;
            const uriString = record.uri;
            const fileName = this._fileName(uriString);
            for (const d of record.diagnostics) {
                entries.push({
                    uri: uriString,
                    fileName,
                    line: d.range.start.line,
                    character: d.range.start.character,
                    endLine: d.range.end.line,
                    endCharacter: d.range.end.character,
                    severity: d.severity === undefined ? 1 : d.severity,
                    message: d.message || '',
                    code: d.code === undefined || d.code === null ? '' : String(d.code),
                });
            }
        }

        entries.sort((a, b) => {
            if (a.uri !== b.uri) return a.fileName.localeCompare(b.fileName);
            if (a.line !== b.line) return a.line - b.line;
            const sa = SEVERITY_ORDER[a.severity] === undefined ? 9 : SEVERITY_ORDER[a.severity];
            const sb = SEVERITY_ORDER[b.severity] === undefined ? 9 : SEVERITY_ORDER[b.severity];
            return sa - sb;
        });
        return entries;
    }

    /** File name (with the workspace-relative folder when available). */
    _fileName(uriString) {
        try {
            const uri = vscode.Uri.parse(uriString);
            const folder = vscode.workspace.getWorkspaceFolder(uri);
            if (folder) {
                const relative = vscode.workspace.asRelativePath(uri, false);
                return relative || uri.path.split('/').pop() || uriString;
            }
            return uri.path.split('/').pop() || uriString;
        } catch (e) {
            return uriString;
        }
    }

    /** Sends the current entries + scope to the webview. */
    _postState() {
        if (!this.view) return;
        const entries = this._collectEntries();
        const counts = { error: 0, warning: 0, info: 0, hint: 0 };
        for (const e of entries) {
            if (e.severity === 1) counts.error++;
            else if (e.severity === 2) counts.warning++;
            else if (e.severity === 3) counts.info++;
            else counts.hint++;
        }
        this.view.webview.postMessage({
            type: 'state',
            scope: this.scope,
            activeFileName: this.activeUri ? this._fileName(this.activeUri) : '',
            entries,
            counts,
        });
        // The view title shows the error / warning counts, like the editor's
        // badge. WebviewView has no `badge` (that is a TreeView property), so
        // the counts go into `description` instead.
        try {
            this.view.description = entries.length > 0
                ? (counts.error + 'E ' + counts.warning + 'W')
                : '';
        } catch (e) { /* description is cosmetic */ }
    }

    /** Opens the document and selects the diagnostic range. */
    async _reveal(uriString, line, character, endLine, endCharacter) {
        try {
            const uri = vscode.Uri.parse(uriString);
            const doc = await vscode.workspace.openTextDocument(uri);
            const editor = await vscode.window.showTextDocument(doc, { preview: false });
            const start = new vscode.Position(line || 0, character || 0);
            const end = new vscode.Position(
                typeof endLine === 'number' ? endLine : (line || 0),
                typeof endCharacter === 'number' ? endCharacter : (character || 0));
            const range = new vscode.Range(start, end);
            editor.selection = new vscode.Selection(start, end);
            editor.revealRange(range, vscode.TextEditorRevealType.InCenterIfOutsideViewport);
        } catch (e) {
            this.log('messageView reveal ERROR: ' + (e && e.message));
        }
    }

    /**
     * The webview document. Kept as a single string: the view is small and a
     * separate HTML file would have to be shipped and resolved through
     * asWebviewUri for no benefit.
     */
    _buildHtml() {
        const nonce = createNonce();
        const csp = [
            "default-src 'none'",
            "style-src 'unsafe-inline'",
            "script-src 'nonce-" + nonce + "'",
        ].join('; ');

        return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta http-equiv="Content-Security-Policy" content="${csp}">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Messages</title>
<style>
  :root {
    --sev-error: var(--vscode-errorForeground, #f14c4c);
    --sev-warning: var(--vscode-editorWarning-foreground, #cca700);
    --sev-info: var(--vscode-editorInfo-foreground, #3794ff);
    --sev-hint: var(--vscode-editorHint-foreground, #6c6c6c);
  }
  body {
    margin: 0;
    padding: 0;
    font-family: var(--vscode-font-family);
    font-size: var(--vscode-font-size);
    color: var(--vscode-foreground);
    background: transparent;
  }
  .toolbar {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 4px 8px;
    position: sticky;
    top: 0;
    background: var(--vscode-sideBar-background, transparent);
    border-bottom: 1px solid var(--vscode-panel-border, rgba(128,128,128,0.35));
    z-index: 1;
  }
  .toolbar select {
    flex: 1;
    background: var(--vscode-dropdown-background);
    color: var(--vscode-dropdown-foreground);
    border: 1px solid var(--vscode-dropdown-border, transparent);
    padding: 2px 4px;
    font-family: inherit;
    font-size: inherit;
  }
  .counts { white-space: nowrap; opacity: 0.85; }
  .counts .e { color: var(--sev-error); }
  .counts .w { color: var(--sev-warning); }
  .list { padding: 2px 0; }
  .entry {
    display: flex;
    align-items: flex-start;
    gap: 6px;
    padding: 3px 8px;
    cursor: pointer;
    line-height: 1.35;
    border: none;
    background: none;
    color: inherit;
    font: inherit;
    text-align: left;
    width: 100%;
    box-sizing: border-box;
  }
  .entry:hover { background: var(--vscode-list-hoverBackground); }
  .entry:focus { outline: 1px solid var(--vscode-focusBorder); outline-offset: -1px; }
  .icon { flex: 0 0 auto; width: 1em; text-align: center; }
  .icon.error { color: var(--sev-error); }
  .icon.warning { color: var(--sev-warning); }
  .icon.info { color: var(--sev-info); }
  .icon.hint { color: var(--sev-hint); }
  .body { flex: 1 1 auto; min-width: 0; }
  .loc { opacity: 0.8; }
  .file { opacity: 0.7; font-size: 0.92em; }
  .msg { word-break: break-word; }
  .empty { padding: 12px 10px; opacity: 0.75; }
  .code { opacity: 0.6; font-size: 0.9em; }
</style>
</head>
<body>
  <div class="toolbar">
    <select id="scope" title="Which files to list">
      <option value="active">Current file</option>
      <option value="workspace">Whole workspace</option>
    </select>
    <span class="counts" id="counts"></span>
  </div>
  <div class="list" id="list"></div>
  <script nonce="${nonce}">
    const vscode = acquireVsCodeApi();
    const listEl = document.getElementById('list');
    const scopeEl = document.getElementById('scope');
    const countsEl = document.getElementById('counts');

    const ICON = { 1: '\\u25CF', 2: '\\u25B2', 3: '\\u25A0', 4: '\\u25CB' };
    const CLASS = { 1: 'error', 2: 'warning', 3: 'info', 4: 'hint' };

    scopeEl.addEventListener('change', () => {
      vscode.postMessage({ type: 'setScope', scope: scopeEl.value });
    });

    function render(state) {
      scopeEl.value = state.scope;
      const c = state.counts || {};
      countsEl.innerHTML = '';
      if (c.error) countsEl.appendChild(span('e', c.error + 'E '));
      if (c.warning) countsEl.appendChild(span('w', c.warning + 'W'));
      if (!c.error && !c.warning) countsEl.textContent = '';

      listEl.innerHTML = '';
      const entries = state.entries || [];
      if (entries.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'empty';
        empty.textContent = state.scope === 'active'
          ? (state.activeFileName
              ? 'No messages in ' + state.activeFileName
              : 'No active SystemVerilog / Verilog file')
          : 'No messages in the workspace';
        listEl.appendChild(empty);
        return;
      }

      let lastUri = null;
      for (const e of entries) {
        const row = document.createElement('button');
        row.className = 'entry';
        row.type = 'button';

        const icon = document.createElement('span');
        icon.className = 'icon ' + (CLASS[e.severity] || 'error');
        icon.textContent = ICON[e.severity] || ICON[1];
        row.appendChild(icon);

        const body = document.createElement('span');
        body.className = 'body';

        if (state.scope === 'workspace' && e.uri !== lastUri) {
          lastUri = e.uri;
          const file = document.createElement('div');
          file.className = 'file';
          file.textContent = e.fileName;
          body.appendChild(file);
        }

        const loc = document.createElement('span');
        loc.className = 'loc';
        loc.textContent = '[' + (e.line + 1) + '] ';
        body.appendChild(loc);

        const msg = document.createElement('span');
        msg.className = 'msg';
        msg.textContent = e.message;
        body.appendChild(msg);

        if (e.code) {
          const code = document.createElement('span');
          code.className = 'code';
          code.textContent = ' (' + e.code + ')';
          body.appendChild(code);
        }

        row.appendChild(body);
        row.title = e.fileName + ':' + (e.line + 1) + ':' + (e.character + 1) + '\\n' + e.message;
        row.addEventListener('click', () => {
          vscode.postMessage({
            type: 'reveal',
            uri: e.uri,
            line: e.line,
            character: e.character,
            endLine: e.endLine,
            endCharacter: e.endCharacter,
          });
        });
        listEl.appendChild(row);
      }
    }

    function span(cls, text) {
      const s = document.createElement('span');
      s.className = cls;
      s.textContent = text;
      return s;
    }

    window.addEventListener('message', (event) => {
      const message = event.data;
      if (message && message.type === 'state') render(message);
    });

    vscode.postMessage({ type: 'ready' });
  </script>
</body>
</html>`;
    }
}

/** Random nonce for the webview CSP. */
function createNonce() {
    const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
    let text = '';
    for (let i = 0; i < 32; i++) {
        text += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    return text;
}

/**
 * Registers the message view and the commands that drive it.
 * @param {vscode.ExtensionContext} context
 * @param {(msg: string) => void} [log]
 * @returns {MessageViewProvider}
 */
function registerMessageView(context, log) {
    const provider = new MessageViewProvider(context.extensionUri, log);

    context.subscriptions.push(
        vscode.window.registerWebviewViewProvider(
            'systemverilogLsp.messageView',
            provider,
            { webviewOptions: { retainContextWhenHidden: true } }
        )
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.showMessages', () => provider.show())
    );

    // The "current file" scope follows the focused editor.
    context.subscriptions.push(
        vscode.window.onDidChangeActiveTextEditor((editor) => {
            provider.setActiveUri(editor && editor.document ? editor.document.uri.toString() : null);
        })
    );
    const active = vscode.window.activeTextEditor;
    provider.setActiveUri(active && active.document ? active.document.uri.toString() : null);

    return provider;
}

module.exports = { registerMessageView, MessageViewProvider };
