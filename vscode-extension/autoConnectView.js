'use strict';

// Auto-connect form (RtlEditor2's AutoConnectWindow equivalent) shown as a
// webview panel.
//
// The desktop editor opens an Avalonia window listing every port of the
// instantiated module, with the signal the editor would connect, and lets the
// user accept / reject each row (Space / Left = accept, Right = reject,
// Up / Down = move). A headless LSP server cannot open that window, so the
// form is split in two:
//
//   - `rtl/autoConnect` returns the rows as data (port name, direction, port
//     group, current connection, candidate signals). The decision of *which
//     signal matches a port* lives in the server's UI-free
//     `CoreBridge.AutoConnectAdapter`, so the desktop editor and this view
//     share one implementation.
//   - `rtl/autoConnectApply` takes the chosen connections and returns the
//     replacement text for the instantiation as a TextEdit.
//
// This file is the renderer: it draws the whole port list at once (like the
// desktop window) and sends the user's choices back. Per
// memory/vscode_extension_architecture.md the extension stays a pipe: it does
// not decide which signal matches a port, it only renders and forwards.

const vscode = require('vscode');

/**
 * One row of the form, as returned by `rtl/autoConnect`.
 * @typedef {{
 *   name: string, direction: string, portGroup: string|null,
 *   currentConnection: string|null, candidates: string[],
 *   isSettled: boolean
 * }} AutoConnectPort
 */

/**
 * The whole form, as returned by `rtl/autoConnect`.
 * @typedef {{
 *   moduleName: string, instanceName: string,
 *   ports: AutoConnectPort[], message: string|null
 * }} AutoConnectModel
 */

/**
 * Opens the auto-connect form for the instantiation at the caret.
 *
 * @param {object} deps
 * @param {() => Promise<any>} deps.ensureClient resolves the LSP client
 * @param {(msg: string) => void} deps.log log sink (OutputChannel)
 */
async function showAutoConnect(deps) {
    const editor = vscode.window.activeTextEditor;
    if (!editor) {
        vscode.window.showWarningMessage('SystemVerilog LSP: open a SystemVerilog file first.');
        return;
    }
    const doc = editor.document;
    if (doc.languageId !== 'systemverilog' && doc.languageId !== 'verilog') {
        vscode.window.showWarningMessage('SystemVerilog LSP: the active file is not SystemVerilog / Verilog.');
        return;
    }

    const position = { line: editor.selection.active.line, character: editor.selection.active.character };
    const params = { textDocument: { uri: doc.uri.toString() }, position };

    let model;
    try {
        const client = await deps.ensureClient();
        model = await client.request('rtl/autoConnect', params);
    } catch (e) {
        deps.log('rtl/autoConnect ERROR: ' + (e && e.stack ? e.stack : e));
        vscode.window.showErrorMessage('SystemVerilog LSP: auto-connect request failed (see the log).');
        return;
    }

    if (!model || !model.ports || model.ports.length === 0) {
        const message = (model && model.message) || 'no module instantiation at the caret';
        vscode.window.showInformationMessage('SystemVerilog LSP: ' + message);
        return;
    }

    const panel = vscode.window.createWebviewPanel(
        'systemverilogLsp.autoConnect',
        `Auto Connect: ${model.instanceName || model.moduleName}`,
        vscode.ViewColumn.Beside,
        { enableScripts: true, retainContextWhenHidden: true }
    );

    const nonce = createNonce();
    panel.webview.html = buildHtml(model, nonce);

    panel.webview.onDidReceiveMessage(async (message) => {
        if (!message || message.type !== 'apply') return;

        const selections = message.selections || {};
        try {
            const client = await deps.ensureClient();
            const edits = await client.request('rtl/autoConnectApply', {
                textDocument: { uri: doc.uri.toString() },
                position,
                selections,
            });
            if (!edits || edits.length === 0) {
                vscode.window.showInformationMessage('SystemVerilog LSP: nothing to apply.');
                return;
            }
            const workspaceEdit = new vscode.WorkspaceEdit();
            for (const edit of edits) {
                workspaceEdit.replace(doc.uri, toRange(edit.range), edit.newText);
            }
            await vscode.workspace.applyEdit(workspaceEdit);
            deps.log('auto-connect applied: ' + Object.keys(selections).length + ' port(s)');
            panel.dispose();
        } catch (e) {
            deps.log('rtl/autoConnectApply ERROR: ' + (e && e.stack ? e.stack : e));
            vscode.window.showErrorMessage('SystemVerilog LSP: applying the connections failed (see the log).');
        }
    });
}

/** Converts an LSP range to a vscode.Range. */
function toRange(range) {
    return new vscode.Range(
        range.start.line, range.start.character,
        range.end.line, range.end.character
    );
}

/** A random nonce for the webview CSP. */
function createNonce() {
    const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
    let text = '';
    for (let i = 0; i < 32; i++) text += chars.charAt(Math.floor(Math.random() * chars.length));
    return text;
}

/** Escapes text for safe insertion into HTML. */
function escapeHtml(text) {
    return String(text == null ? '' : text)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

/**
 * Builds the webview HTML: the whole port list at once, with per-row
 * accept / reject, mirroring the desktop window's layout.
 *
 * @param {AutoConnectModel} model
 * @param {string} nonce
 */
function buildHtml(model, nonce) {
    const rows = [];
    let lastGroup = null;
    for (let i = 0; i < model.ports.length; i++) {
        const port = model.ports[i];
        if (port.portGroup && port.portGroup !== lastGroup) {
            lastGroup = port.portGroup;
            rows.push(`<tr class="group"><td colspan="3">// ${escapeHtml(port.portGroup)}</td></tr>`);
        }
        const candidates = port.candidates || [];
        const options = ['<option value="">(not connected)</option>'];
        for (const candidate of candidates) {
            const selected = candidate === port.currentConnection ? ' selected' : '';
            options.push(`<option value="${escapeHtml(candidate)}"${selected}>${escapeHtml(candidate)}</option>`);
        }
        // A port with no candidate and no current connection still needs a
        // row so the user can see it is unconnected (the desktop window does
        // the same).
        const current = port.currentConnection || '';
        const hasCurrentOption = candidates.indexOf(current) >= 0;
        if (current && !hasCurrentOption) {
            options.push(`<option value="${escapeHtml(current)}" selected>${escapeHtml(current)}</option>`);
        }
        rows.push(
            `<tr data-port="${escapeHtml(port.name)}">` +
            `<td class="name">.${escapeHtml(port.name)}</td>` +
            `<td class="dir">${escapeHtml(port.direction)}</td>` +
            `<td class="conn"><select data-port="${escapeHtml(port.name)}">${options.join('')}</select></td>` +
            `</tr>`
        );
    }

    return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta http-equiv="Content-Security-Policy"
      content="default-src 'none'; style-src 'unsafe-inline'; script-src 'nonce-${nonce}';">
<style>
  body { font-family: var(--vscode-font-family); font-size: var(--vscode-font-size); color: var(--vscode-foreground); padding: 8px; }
  h2 { font-size: 1.1em; margin: 0 0 4px 0; }
  .sub { opacity: 0.7; margin-bottom: 12px; }
  table { border-collapse: collapse; width: 100%; }
  th, td { text-align: left; padding: 3px 8px; }
  th { border-bottom: 1px solid var(--vscode-panel-border); font-weight: 600; }
  tr.group td { opacity: 0.7; font-style: italic; padding-top: 10px; }
  td.name { font-family: var(--vscode-editor-font-family); white-space: nowrap; }
  td.dir { opacity: 0.7; white-space: nowrap; }
  select { width: 100%; background: var(--vscode-input-background); color: var(--vscode-input-foreground);
           border: 1px solid var(--vscode-input-border, transparent); padding: 2px 4px; }
  .buttons { margin-top: 16px; display: flex; gap: 8px; }
  button { background: var(--vscode-button-background); color: var(--vscode-button-foreground);
           border: none; padding: 6px 14px; cursor: pointer; }
  button.secondary { background: var(--vscode-button-secondaryBackground); color: var(--vscode-button-secondaryForeground); }
  button:hover { opacity: 0.9; }
</style>
</head>
<body>
  <h2>${escapeHtml(model.moduleName)} ${escapeHtml(model.instanceName)}</h2>
  <div class="sub">Choose the signal for each port, then Apply.</div>
  <table>
    <thead><tr><th>Port</th><th>Direction</th><th>Connection</th></tr></thead>
    <tbody>
${rows.join('\n')}
    </tbody>
  </table>
  <div class="buttons">
    <button id="apply">Apply</button>
    <button id="cancel" class="secondary">Cancel</button>
  </div>
<script nonce="${nonce}">
  const vscode = acquireVsCodeApi();
  document.getElementById('apply').addEventListener('click', () => {
    const selections = {};
    for (const select of document.querySelectorAll('select[data-port]')) {
      selections[select.getAttribute('data-port')] = select.value || null;
    }
    vscode.postMessage({ type: 'apply', selections });
  });
  document.getElementById('cancel').addEventListener('click', () => {
    vscode.postMessage({ type: 'cancel' });
  });
</script>
</body>
</html>`;
}

module.exports = { showAutoConnect };
