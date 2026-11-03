'use strict';

const vscode = require('vscode');
const { spawn } = require('child_process');
const { registerHierarchy } = require('./hierarchy');

let log = null;
let logFilePath = null;
function initLog(context) {
    if (!log) {
        log = vscode.window.createOutputChannel('SystemVerilog LSP');
        context.subscriptions.push(log);
        // Mirror every line to a file as well. The RTL Hierarchy view renders
        // silently empty when a request fails, and the OutputChannel is easy to
        // miss, so a plain file is the reliable way to diagnose it. Prefer the
        // extension's own log folder (context.logUri); fall back to the temp
        // directory when it is unavailable.
        try {
            const fs = require('fs');
            const path = require('path');
            let dir = null;
            if (context && context.logUri) {
                dir = context.logUri.fsPath;
            } else if (context && context.globalStorageUri) {
                dir = context.globalStorageUri.fsPath;
            }
            if (dir) {
                fs.mkdirSync(dir, { recursive: true });
                logFilePath = path.join(dir, 'svlsp-extension.log');
            } else {
                logFilePath = path.join(require('os').tmpdir(), 'svlsp-extension.log');
            }
            fs.writeFileSync(logFilePath, '');
        } catch (e) {
            logFilePath = null;
        }
    }
    return log;
}
function logLine(msg) {
    const line = '[' + new Date().toISOString() + '] ' + msg;
    if (log) log.appendLine(line);
    if (logFilePath) {
        try { require('fs').appendFileSync(logFilePath, line + '\n'); } catch (e) { /* ignore */ }
    }
}
/** Reveals the OutputChannel (and reports the log file path). */
function showLog() {
    if (log) log.show(true);
    if (logFilePath) {
        vscode.window.setStatusBarMessage('SystemVerilog LSP log: ' + logFilePath, 8000);
    }
}

// ---- raw JSON-RPC (LSP over stdio) client, no external dependencies ----

class RawLspClient {
    constructor(process_) {
        this.proc = process_;
        this.nextId = 1;
        this.pending = new Map(); // id -> {resolve, reject}
        this.notifications = new Map(); // method -> handler
        this.buffer = Buffer.alloc(0);
        this.closed = false;

        this.proc.stdout.on('data', (chunk) => this.onData(chunk));
        this.proc.stderr.on('data', (chunk) => {
            // Route the server's stderr (workspace scan progress, parse
            // failures, full stack traces) into the extension log file so the
            // user can see exactly what the server did.
            const text = chunk.toString();
            console.error('[svlsp server stderr]', text);
            for (const line of text.split(/\r?\n/)) {
                if (line.trim().length > 0) logLine('[server] ' + line);
            }
        });
        this.proc.on('exit', (code) => {
            this.closed = true;
            for (const p of this.pending.values()) {
                p.reject(new Error('server exited (code ' + code + ')'));
            }
            this.pending.clear();
        });
    }

    onData(chunk) {
        this.buffer = Buffer.concat([this.buffer, chunk]);
        // LSP framing: headers terminated by \r\n\r\n, then Content-Length body
        while (true) {
            const sep = this.buffer.indexOf('\r\n\r\n');
            if (sep < 0) return;
            const header = this.buffer.slice(0, sep).toString('ascii');
            const m = /Content-Length:\s*(\d+)/i.exec(header);
            if (!m) {
                this.buffer = this.buffer.slice(sep + 4);
                continue;
            }
            const length = parseInt(m[1], 10);
            if (this.buffer.length < sep + 4 + length) return;
            const body = this.buffer.slice(sep + 4, sep + 4 + length).toString('utf8');
            this.buffer = this.buffer.slice(sep + 4 + length);
            this.dispatch(JSON.parse(body));
        }
    }

    dispatch(msg) {
        if (msg.id !== undefined && (msg.method === undefined || msg.result !== undefined || msg.error !== undefined)) {
            // response to a request
            const p = this.pending.get(msg.id);
            if (p) {
                this.pending.delete(msg.id);
                if (msg.error) p.reject(new Error(msg.error.message || 'server error'));
                else p.resolve(msg.result);
            }
        } else if (msg.method) {
            const h = this.notifications.get(msg.method);
            if (h) h(msg.params);
        }
    }

    send(method, params) {
        this.write(JSON.stringify({ jsonrpc: '2.0', method, params }));
    }

    request(method, params, timeoutMs) {
        const id = this.nextId++;
        return new Promise((resolve, reject) => {
            this.pending.set(id, { resolve, reject });
            if (timeoutMs && timeoutMs > 0) {
                // A request that never gets an answer would otherwise leave the
                // caller (e.g. the RTL Hierarchy getChildren) awaiting forever,
                // which renders as a silently empty tree view.
                setTimeout(() => {
                    if (this.pending.has(id)) {
                        this.pending.delete(id);
                        reject(new Error('request timed out: ' + method));
                    }
                }, timeoutMs);
            }
            this.write(JSON.stringify({ jsonrpc: '2.0', id, method, params }));
        });
    }

    write(text) {
        if (this.closed || !this.proc.stdin.writable) return;
        const body = Buffer.from(text, 'utf8');
        const header = Buffer.from('Content-Length: ' + body.length + '\r\n\r\n', 'ascii');
        this.proc.stdin.write(Buffer.concat([header, body]));
    }

    onNotification(method, handler) {
        this.notifications.set(method, handler);
    }

    dispose() {
        this.closed = true;
        try { this.proc.kill(); } catch (e) { /* ignore */ }
    }
}

// --- workspace root reporting -------------------------------------------

// The RTL Hierarchy view lists the WHOLE workspace, so the server has to know
// every workspace root - not just the first one. Without this the tree only
// ever contains the documents the client opened.
function workspaceFoldersList() {
    return vscode.workspace.workspaceFolders || [];
}

function workspaceFolderParams() {
    const folders = workspaceFoldersList();
    if (folders.length > 0) {
        return folders.map((folder) => ({
            uri: folder.uri.toString(),
            name: folder.name,
        }));
    }
    // No folder open: report the inferred root so the server can scan the
    // project (see workspaceRootUri).
    const inferred = inferRootFromOpenDocuments();
    return inferred ? [{ uri: inferred.toString(), name: 'workspace' }] : [];
}

function workspaceRootUri() {
    const folders = workspaceFoldersList();
    if (folders.length > 0) return folders[0].uri.toString();
    // No folder open ("File > Open File"): report the directory of an open
    // SystemVerilog document as the root. Without this the server has no
    // workspace root at initialize, cannot scan the project, and the RTL
    // Hierarchy view renders empty until a document is opened (and even then
    // only if the tree is re-fetched).
    const inferred = inferRootFromOpenDocuments();
    return inferred ? inferred.uri.toString() : null;
}

/**
 * Directory of the first open SystemVerilog / Verilog document, used as a
 * workspace root when the window has no folder open.
 */
function inferRootFromOpenDocuments() {
    for (const doc of vscode.workspace.textDocuments) {
        if (doc.uri.scheme !== 'file') continue;
        const lang = doc.languageId;
        if (lang !== 'systemverilog' && lang !== 'verilog') continue;
        // Uri.joinPath(uri, '..') is not normalised, so build the parent
        // directory explicitly from the filesystem path.
        const dir = require('path').dirname(doc.uri.fsPath);
        return vscode.Uri.file(dir);
    }
    return null;
}

function resolveServerCommand(context) {
    const config = vscode.workspace.getConfiguration('systemverilogLsp');
    let serverPath = config.get('serverPath', '');

    if (!serverPath) {
        // Prefer the published binary. The extension lives inside the
        // repository (vscode-extension/), so the server is a sibling folder;
        // falling back to `dotnet run` would execute bin/Debug, which is often
        // stale (an already-running server keeps the DLL locked, so the rebuild
        // silently fails and the old binary keeps serving old behaviour).
        const candidates = [
            vscode.Uri.joinPath(context.extensionUri, 'SystemVerilogLanguageServer', 'publish', 'SystemVerilogLanguageServer.exe').fsPath,
            vscode.Uri.joinPath(context.extensionUri, 'SystemVerilogLanguageServer', 'publish', 'SystemVerilogLanguageServer').fsPath,
            vscode.Uri.joinPath(context.extensionUri, '..', 'SystemVerilogLanguageServer', 'publish', 'SystemVerilogLanguageServer.exe').fsPath,
            vscode.Uri.joinPath(context.extensionUri, '..', 'SystemVerilogLanguageServer', 'publish', 'SystemVerilogLanguageServer').fsPath,
        ];
        for (const candidate of candidates) {
            if (require('fs').existsSync(candidate)) {
                serverPath = candidate;
                break;
            }
        }
    }

    if (serverPath) {
        return { command: serverPath, args: [] };
    }

    const projectCandidates = [
        vscode.Uri.joinPath(context.extensionUri, 'SystemVerilogLanguageServer', 'SystemVerilogLanguageServer.csproj').fsPath,
        vscode.Uri.joinPath(context.extensionUri, '..', 'SystemVerilogLanguageServer', 'SystemVerilogLanguageServer.csproj').fsPath,
    ];
    for (const candidate of projectCandidates) {
        if (require('fs').existsSync(candidate)) {
            return { command: 'dotnet', args: ['run', '--project', candidate] };
        }
    }

    return null;
}

let client = null;
let initPromise = null;
const openedDocs = new Map(); // uri -> open count
// set by activate(); used to re-fetch the RTL Hierarchy tree once the LSP
// session is ready (the view can render before the client has started, in
// which case the first fetch comes back empty).
let hierarchyProviderRef = null;

function withClient(fn) {
    return ensureClient().then(fn, (err) => {
        logLine('ERROR: ' + (err && err.stack ? err.stack : err.message));
        vscode.window.showErrorMessage('SystemVerilog LSP: ' + err.message);
        return undefined;
    });
}

function ensureClient() {
    if (initPromise) return initPromise;
    if (client) return Promise.resolve(client);

    const server = resolveServerCommand(getContext());
    if (!server) {
        return Promise.reject(new Error(
            'server executable not found. Set "systemverilogLsp.serverPath" to the published SystemVerilogLanguageServer binary, ' +
            'or build it with: dotnet publish SystemVerilogLanguageServer/SystemVerilogLanguageServer.csproj -c Release'
        ));
    }

    logLine('starting server: ' + server.command + ' ' + server.args.join(' '));
    const proc = spawn(server.command, server.args, { stdio: ['pipe', 'pipe', 'pipe'] });
    proc.on('error', (err) => logLine('server spawn error: ' + err.message));
    proc.on('exit', (code) => logLine('server exited with code ' + code));
    client = new RawLspClient(proc);
    // A spawn failure (missing binary, permission) must reject the pending
    // initialize instead of leaving the promise pending forever.
    const spawnFailed = new Promise((_, reject) => {
        proc.on('error', (err) => reject(new Error('server spawn failed: ' + err.message)));
    });
    spawnFailed.catch(() => { /* handled below via Promise.race */ });
    client.onNotification('textDocument/publishDiagnostics', (params) => {
        const uri = vscode.Uri.parse(params.uri);
        const diags = (params.diagnostics || []).map((d) => {
            const range = new vscode.Range(
                d.range.start.line, d.range.start.character,
                d.range.end.line, d.range.end.character
            );
            const sev = (d.severity === 1) ? vscode.DiagnosticSeverity.Error :
                        (d.severity === 2) ? vscode.DiagnosticSeverity.Warning :
                        (d.severity === 3) ? vscode.DiagnosticSeverity.Information :
                        vscode.DiagnosticSeverity.Hint;
            const diag = new vscode.Diagnostic(range, d.message, sev);
            if (d.code !== undefined) diag.code = d.code;
            return diag;
        });
        const collection = getDiagnosticCollection();
        collection.set(uri, diags);
    });

    initPromise = Promise.race([
        client.request('initialize', {
            processId: process.pid,
            rootUri: workspaceRootUri(),
            workspaceFolders: workspaceFolderParams(),
            capabilities: {},
        }, 120000),
        spawnFailed,
    ]).then((result) => {
        logLine('initialize succeeded');
        client.send('initialized', {});
        // sync already-open documents
        for (const doc of vscode.workspace.textDocuments) {
            maybeOpenDocument(doc);
        }
        // The RTL Hierarchy view may have rendered before the session existed
        // (getChildren awaits ensureClient, but the very first render can race
        // the workspace scan). Re-fetch now that the server is ready; this is a
        // no-op once the tree has content.
        if (hierarchyProviderRef) hierarchyProviderRef.refreshIfEmpty();
        const openDisposable = vscode.workspace.onDidOpenTextDocument((doc) => { logLine('didOpen: ' + doc.uri.toString()); maybeOpenDocument(doc); });
        const changeDisposable = vscode.workspace.onDidChangeTextDocument((e) => {
            if (e.document && openedDocs.has(e.document.uri.toString())) {
                client.send('textDocument/didChange', {
                    textDocument: { uri: e.document.uri.toString(), version: e.document.version },
                    contentChanges: [{ text: e.document.getText() }],
                });
            }
        });
        const closeDisposable = vscode.workspace.onDidCloseTextDocument((doc) => {
            const key = doc.uri.toString();
            if (openedDocs.has(key)) {
                openedDocs.delete(key);
                client.send('textDocument/didClose', { textDocument: { uri: key } });
                getDiagnosticCollection().delete(doc.uri);
            }
        });
        client.disposables = [openDisposable, changeDisposable, closeDisposable];
        return client;
    }, (err) => {
        client.dispose();
        client = null;
        initPromise = null;
        throw err;
    });
    return initPromise;
}

let context_ = null;
function getContext() { return context_; }

let diagnosticCollection = null;
function getDiagnosticCollection() {
    if (!diagnosticCollection) {
        diagnosticCollection = vscode.languages.createDiagnosticCollection('systemverilogLsp');
    }
    return diagnosticCollection;
}

function maybeOpenDocument(doc) {
    if (!doc || !client) return;
    const lang = doc.languageId;
    if (lang !== 'systemverilog' && lang !== 'verilog') return;
    const key = doc.uri.toString();
    if (openedDocs.has(key)) return;
    openedDocs.set(key, true);
    client.send('textDocument/didOpen', {
        textDocument: {
            uri: key,
            languageId: lang,
            version: doc.version,
            text: doc.getText(),
        },
    });
}

function positionParams(doc, pos, extra) {
    return Object.assign({
        textDocument: { uri: doc.uri.toString() },
        position: { line: pos.line, character: pos.character },
    }, extra || {});
}

function provideHover(doc, pos) {
    logLine('provideHover @ ' + pos.line + ':' + pos.character);
    return client.request('textDocument/hover', positionParams(doc, pos)).then((result) => {
        logLine('hover result: ' + JSON.stringify(result));
        if (!result || !result.contents) return null;
        const contents = typeof result.contents === 'string' ? [result.contents] :
            Array.isArray(result.contents) ? result.contents :
            (result.contents.value !== undefined ? [result.contents.value] : []);
        const md = new vscode.MarkdownString(contents.join('\n\n'));
        const range = result.range ? new vscode.Range(
            result.range.start.line, result.range.start.character,
            result.range.end.line, result.range.end.character) : undefined;
        return new vscode.Hover(md, range);
    });
}

function provideDefinition(doc, pos) {
    logLine('provideDefinition @ ' + pos.line + ':' + pos.character);
    return client.request('textDocument/definition', positionParams(doc, pos)).then((result) => {
        logLine('definition result: ' + JSON.stringify(result));
        if (!result) return [];
        return toLocations(result);
    });
}

function provideReferences(doc, pos, opts) {
    logLine('provideReferences @ ' + pos.line + ':' + pos.character);
    return client.request('textDocument/references', positionParams(doc, pos, { context: { includeDeclaration: opts.includeDeclaration } })).then((result) => {
        logLine('references result count: ' + (Array.isArray(result) ? result.length : 'null'));
        if (!result) return [];
        return toLocations(result);
    });
}

function provideDocumentSymbols(doc) {
    logLine('provideDocumentSymbols');
    // documentSymbol takes only the textDocument (no position). Using
    // positionParams() here would dereference an undefined position and throw
    // "Cannot read properties of undefined (reading 'line')".
    return client.request('textDocument/documentSymbol', {
        textDocument: { uri: doc.uri.toString() },
    }).then((result) => {
        logLine('documentSymbol result count: ' + (result ? result.length : 'null'));
        if (!result) return [];
        return result.map(toSymbol).filter((s) => s);
    });
}

// CompletionItemKind values used by the server
const completionKinds = {
    3: vscode.CompletionItemKind.Function,
    6: vscode.CompletionItemKind.Variable,
    9: vscode.CompletionItemKind.Module,
    14: vscode.CompletionItemKind.Keyword,
};

function provideCompletion(doc, pos) {
    logLine('provideCompletion @ ' + pos.line + ':' + pos.character);
    return client.request('textDocument/completion', positionParams(doc, pos)).then((result) => {
        if (!result || !result.items) return [];
        logLine('completion result count: ' + result.items.length);
        return result.items.map((item) => {
            const ci = new vscode.CompletionItem(
                item.label,
                completionKinds[item.kind] !== undefined ? completionKinds[item.kind] : vscode.CompletionItemKind.Text
            );
            if (item.detail) ci.detail = item.detail;
            return ci;
        });
    });
}

function toLocations(items) {
    const list = Array.isArray(items) ? items : [items];
    return list.map((loc) => {
        if (!loc || !loc.range) return null;
        const range = new vscode.Range(
            loc.range.start.line, loc.range.start.character,
            loc.range.end.line, loc.range.end.character
        );
        return new vscode.Location(vscode.Uri.parse(loc.uri), range);
    }).filter((l) => l);
}

// semantic tokens: build a vscode.SemanticTokens from the server response
const tokenTypesByIndex = ['keyword', 'comment', 'string', 'number', 'macro',
    'function', 'type', 'variable', 'property', 'parameter', 'register', 'identifier'];

function provideDocumentSemanticTokens(doc) {
    logLine('provideDocumentSemanticTokens called: ' + doc.uri.toString());
    if (!client) {
        logLine('semanticTokens: client is null');
        return new vscode.SemanticTokens(new Uint32Array(0));
    }
    return client.request('textDocument/semanticTokens/full', { textDocument: { uri: doc.uri.toString() } }).then((result) => {
        logLine('semanticTokens raw result: ' + JSON.stringify(result).substring(0, 200));
        const builder = new vscode.SemanticTokensBuilder(
            new vscode.SemanticTokensLegend(tokenTypesByIndex, []));
        let count = 0;
        if (result && Array.isArray(result.data)) {
            const d = result.data;
            let line = 0, char = 0;
            for (let i = 0; i + 4 < d.length; i += 5) {
                line += d[i];
                if (d[i] > 0) char = d[i + 1]; else char += d[i + 1];
                const length = d[i + 2];
                const type = tokenTypesByIndex[d[i + 3]] || 'identifier';
                const range = new vscode.Range(line, char, line, char + length);
                builder.push(range, type, []);
                count++;
            }
        }
        logLine('semanticTokens: ' + count + ' tokens');
        return builder.build();
    }, (err) => {
        logLine('semanticTokens request ERROR: ' + (err && err.message));
        return new vscode.SemanticTokens(new Uint32Array(0));
    });
}

// DocumentSymbol -> vscode.DocumentSymbol (recursive, SymbolKind passthrough numeric)
function toSymbol(sym) {
    if (!sym || !sym.range || !sym.selectionRange) return null;
    const range = new vscode.Range(
        sym.range.start.line, sym.range.start.character,
        sym.range.end.line, sym.range.end.character
    );
    const sel = new vscode.Range(
        sym.selectionRange.start.line, sym.selectionRange.start.character,
        sym.selectionRange.end.line, sym.selectionRange.end.character
    );
    const vs = new vscode.DocumentSymbol(sym.name || '', sym.detail || '',
        sym.kind !== undefined ? sym.kind : vscode.SymbolKind.Struct,
        range, sel);
    if (sym.children && sym.children.length) {
        vs.children = sym.children.map(toSymbol).filter((s) => s);
    }
    return vs;
}

function activate(context) {
    context_ = context;
    initLog(context);
    logLine('extension activated');
    logLine('log file: ' + (logFilePath || '<unavailable>'));
    logLine('workspaceFolders: ' + JSON.stringify(workspaceFolderParams()));
    logLine('rootUri: ' + workspaceRootUri());

    // Let the user open the log without hunting through the Output dropdown.
    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.showLog', () => showLog())
    );
    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.showLogFilePath', async () => {
            if (!logFilePath) {
                vscode.window.showWarningMessage('SystemVerilog LSP: log file unavailable');
                return;
            }
            await vscode.env.clipboard.writeText(logFilePath);
            vscode.window.showInformationMessage('Log file path copied: ' + logFilePath);
        })
    );

    // RTL Hierarchy tree view (NavigatePanel equivalent). The standard File
    // Explorer cannot be extended with instance children, so the hierarchy is
    // rendered in a Custom Tree View inside the Explorer container.
    const hierarchyProvider = registerHierarchy(context, () => ensureClient(), logLine, showLog);
    hierarchyProviderRef = hierarchyProvider;
    context.subscriptions.push(
        vscode.workspace.onDidSaveTextDocument(async (doc) => {
            const lang = doc && doc.languageId;
            if (lang !== 'systemverilog' && lang !== 'verilog') return;
            // the server re-parses on didChange; refresh the hierarchy so the
            // instance tree reflects the saved file
            try {
                const c = await ensureClient();
                if (c) {
                    c.send('textDocument/didSave', {
                        textDocument: { uri: doc.uri.toString() },
                        text: doc.getText(),
                    });
                }
            } catch (e) {
                logLine('didSave send ERROR: ' + (e && e.message));
            }
            // the save only changed the parse result of a file the server
            // already knows: redraw without re-scanning the workspace roots
            hierarchyProvider.refreshTree();
        })
    );

    // A folder added to / removed from the window changes the workspace tree.
    context.subscriptions.push(
        vscode.workspace.onDidChangeWorkspaceFolders((e) => {
            if (!client) return;
            client.send('workspace/didChangeWorkspaceFolders', {
                event: {
                    added: (e.added || []).map((f) => ({ uri: f.uri.toString(), name: f.name })),
                    removed: (e.removed || []).map((f) => ({ uri: f.uri.toString(), name: f.name })),
                },
            });
            hierarchyProvider.refresh();
        })
    );

    // Files created / deleted / renamed on disk (outside the editor) change the
    // workspace tree, so re-scan the workspace roots when a .sv/.svh/.v/.vh is
    // added or removed.
    const sourceWatcher = vscode.workspace.createFileSystemWatcher(
        '**/*.{sv,svh,v,vh}'
    );
    context.subscriptions.push(sourceWatcher);
    context.subscriptions.push(
        sourceWatcher.onDidCreate(() => hierarchyProvider.refresh())
    );
    context.subscriptions.push(
        sourceWatcher.onDidDelete(() => hierarchyProvider.refresh())
    );
    context.subscriptions.push(
        vscode.workspace.onDidRenameFiles(() => hierarchyProvider.refresh())
    );

    // configurationDefaults does not reliably apply to already-open
    // workspaces; force-enable semantic highlighting programmatically
    // (only when the current value is the default 'configuredByTheme').
    const editorConfig = vscode.workspace.getConfiguration('editor');
    const current = editorConfig.inspect('semanticHighlighting.enabled');
    if (current.globalValue === undefined && current.workspaceValue === undefined) {
        editorConfig.update('semanticHighlighting.enabled', true, vscode.ConfigurationTarget.Global)
            .then(() => logLine('semanticHighlighting.enabled set to true (user settings)'));
    }

    // configurationDefaults does not reliably apply semanticTokenColorCustomizations;
    // write the pluginVerilog CodeDrawStyle colors into user settings when absent.
    const existingColors = editorConfig.inspect('semanticTokenColorCustomizations');
    // overwrite when the previous extension-written default (identifier #D4D4D4) is present
    const prevDefault = existingColors.globalValue &&
        existingColors.globalValue.rules && existingColors.globalValue.rules.identifier === '#D4D4D4';
    if (!existingColors.globalValue && !existingColors.workspaceValue || prevDefault) {
        // pluginVerilog CodeDrawStyle hues, adjusted for VS Code dark themes
        // (the original colors are tuned for the CodeEditor2 light background
        // and read as white on dark backgrounds).
        const colors = {
            rules: {
                keyword: '#569CD6',
                comment: '#6A9955',
                type: '#4EC9B0',
                function: '#DCDCAA',
                parameter: '#FF5EC2',
                macro: '#FF5EC2',
                property: '#E86FB8',
                variable: '#F48FB1',
                number: '#CE9178',
                string: '#CE9178',
                register: '#FF6B6B',
                identifier: '#9CDCFE',
            }
        };
        editorConfig.update('semanticTokenColorCustomizations', colors, vscode.ConfigurationTarget.Global)
            .then(() => logLine('semanticTokenColorCustomizations written (user settings)'));
    }
    context.subscriptions.push(
        vscode.languages.registerHoverProvider(
            [{ scheme: 'file', language: 'systemverilog' }, { scheme: 'file', language: 'verilog' }],
            { provideHover: (doc, pos) => withClient(() => provideHover(doc, pos)) }
        )
    );
    context.subscriptions.push(
        vscode.languages.registerDefinitionProvider(
            [{ scheme: 'file', language: 'systemverilog' }, { scheme: 'file', language: 'verilog' }],
            { provideDefinition: (doc, pos) => withClient(() => provideDefinition(doc, pos)) }
        )
    );
    context.subscriptions.push(
        vscode.languages.registerDocumentSymbolProvider(
            [{ scheme: 'file', language: 'systemverilog' }, { scheme: 'file', language: 'verilog' }],
            { provideDocumentSymbols: (doc) => withClient(() => provideDocumentSymbols(doc)) }
        )
    );
    context.subscriptions.push(
        vscode.languages.registerReferenceProvider(
            [{ scheme: 'file', language: 'systemverilog' }, { scheme: 'file', language: 'verilog' }],
            { provideReferences: (doc, pos, opts) => withClient(() => provideReferences(doc, pos, opts)) }
        )
    );
    context.subscriptions.push(
        vscode.languages.registerCompletionItemProvider(
            [{ scheme: 'file', language: 'systemverilog' }, { scheme: 'file', language: 'verilog' }],
            { provideCompletionItems: (doc, pos) => withClient(() => provideCompletion(doc, pos)) },
            '.', '`', '$'
        )
    );
    try {
        const legend = new vscode.SemanticTokensLegend(
            ['keyword', 'comment', 'string', 'number', 'macro',
             'function', 'type', 'variable', 'property', 'parameter', 'register', 'identifier'],
            []
        );
        context.subscriptions.push(
            vscode.languages.registerDocumentSemanticTokensProvider(
                { scheme: 'file', language: 'systemverilog' },
                { provideDocumentSemanticTokens: (doc) => withClient(() => provideDocumentSemanticTokens(doc)) },
                legend
            )
        );
        context.subscriptions.push(
            vscode.languages.registerDocumentSemanticTokensProvider(
                { scheme: 'file', language: 'verilog' },
                { provideDocumentSemanticTokens: (doc) => withClient(() => provideDocumentSemanticTokens(doc)) },
                legend
            )
        );
        logLine('semantic tokens provider registered');
    } catch (e) {
        logLine('semantic tokens registration ERROR: ' + (e && e.message));
    }

    // Eagerly start the LSP session as soon as a matching document is
    // (or becomes) open, so didOpen + publishDiagnostics work without
    // requiring the user to hover first.
    const isMatchingDoc = (doc) => doc &&
        (doc.languageId === 'systemverilog' || doc.languageId === 'verilog');
    if (vscode.workspace.textDocuments.some(isMatchingDoc)) {
        ensureClient().catch((err) => logLine('ERROR: ' + (err && err.stack ? err.stack : err.message)));
    }
    context.subscriptions.push(vscode.workspace.onDidOpenTextDocument((doc) => {
        if (isMatchingDoc(doc)) {
            ensureClient().catch((err) => logLine('ERROR: ' + (err && err.stack ? err.stack : err.message)));
            // The server infers a workspace root from the first opened
            // SystemVerilog document when the window has no folder open. The
            // hierarchy view is rendered as soon as the extension activates,
            // which can be before that didOpen has been processed, so the first
            // fetch may come back empty. Re-fetch once the document is open so
            // the tree fills in without the user pressing refresh (no-op once
            // the tree has content).
            hierarchyProvider.refreshIfEmpty();
        }
    }));
}

function deactivate() {
    if (client) {
        if (client.disposables) {
            for (const d of client.disposables) d.dispose();
        }
        client.dispose();
        client = null;
    }
    openedDocs.clear();
    if (diagnosticCollection) diagnosticCollection.clear();
}

module.exports = { activate, deactivate };
