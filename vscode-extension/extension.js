'use strict';

const vscode = require('vscode');
const { spawn } = require('child_process');

let log = null;
function initLog(context) {
    if (!log) {
        log = vscode.window.createOutputChannel('SystemVerilog LSP');
        context.subscriptions.push(log);
    }
    return log;
}
function logLine(msg) {
    if (log) log.appendLine('[' + new Date().toISOString() + '] ' + msg);
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
            console.error('[svlsp server stderr]', chunk.toString());
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

    request(method, params) {
        const id = this.nextId++;
        return new Promise((resolve, reject) => {
            this.pending.set(id, { resolve, reject });
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

function resolveServerCommand(context) {
    const config = vscode.workspace.getConfiguration('systemverilogLsp');
    let serverPath = config.get('serverPath', '');

    if (!serverPath) {
        const candidates = [
            vscode.Uri.joinPath(context.extensionUri, 'SystemVerilogLanguageServer', 'publish', 'SystemVerilogLanguageServer.exe').fsPath,
            vscode.Uri.joinPath(context.extensionUri, 'SystemVerilogLanguageServer', 'publish', 'SystemVerilogLanguageServer').fsPath,
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
const openedDocs = new Map(); // uri -> open count

function withClient(fn) {
    return ensureClient().then(fn, (err) => {
        logLine('ERROR: ' + (err && err.stack ? err.stack : err.message));
        vscode.window.showErrorMessage('SystemVerilog LSP: ' + err.message);
        return undefined;
    });
}

function ensureClient() {
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

    return client.request('initialize', {
        processId: process.pid,
        rootUri: vscode.workspace.workspaceFolders && vscode.workspace.workspaceFolders[0]
            ? vscode.workspace.workspaceFolders[0].uri.toString() : null,
        capabilities: {},
    }).then((result) => {
        logLine('initialize succeeded');
        client.send('initialized', {});
        // sync already-open documents
        for (const doc of vscode.workspace.textDocuments) {
            maybeOpenDocument(doc);
        }
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
        throw err;
    });
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
    return client.request('textDocument/documentSymbol', positionParams(doc)).then((result) => {
        logLine('documentSymbol result count: ' + (result ? result.length : 'null'));
        if (!result) return [];
        return result.map(toSymbol).filter((s) => s);
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
    return client.request('textDocument/semanticTokens/full', positionParams(doc)).then((result) => {
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
                const start = new vscode.Position(line, char);
                const end = new vscode.Position(line, char + length);
                builder.push(start, end, type);
                count++;
            }
        }
        logLine('semanticTokens: ' + count + ' tokens');
        return builder.build();
    }, (err) => {
        logLine('semanticTokens ERROR: ' + (err && err.message));
        throw err;
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

    // configurationDefaults does not reliably apply to already-open
    // workspaces; force-enable semantic highlighting programmatically
    // (only when the current value is the default 'configuredByTheme').
    const editorConfig = vscode.workspace.getConfiguration('editor');
    const current = editorConfig.inspect('semanticHighlighting.enabled');
    if (current.globalValue === undefined && current.workspaceValue === undefined) {
        editorConfig.update('semanticHighlighting.enabled', true, vscode.ConfigurationTarget.Global)
            .then(() => logLine('semanticHighlighting.enabled set to true (user settings)'));
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
