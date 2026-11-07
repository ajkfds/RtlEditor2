'use strict';

const vscode = require('vscode');
const { spawn } = require('child_process');

// Same reasoning as the guarded require of ./messageView below: a throw while
// loading a sibling module would abort the whole extension. The hierarchy is
// required (the view is core), but the failure is recorded so it is
// diagnosable instead of a bare "extension failed to activate".
let registerHierarchy = null;
let hierarchyLoadError = null;
try {
    registerHierarchy = require('./hierarchy').registerHierarchy;
} catch (err) {
    hierarchyLoadError = err;
}

// The Message view lives in its own module. Requiring it at the top level
// means a syntax error / bad export there throws while the extension module is
// being loaded, which VS Code reports as "the extension failed to activate"
// and then disables the whole extension - the RTL Hierarchy, hover, completion
// and everything else stop working because of one optional pane.
//
// Load it defensively instead: a broken messageView.js then only disables that
// pane, and the reason is recorded in the crash log.
let registerMessageView = null;
let messageViewLoadError = null;
try {
    registerMessageView = require('./messageView').registerMessageView;
} catch (err) {
    messageViewLoadError = err;
}

// Auto-connect form (webview). Loaded defensively for the same reason as the
// message view: a broken module must not disable the whole extension.
let autoConnectView = null;
let autoConnectLoadError = null;
try {
    autoConnectView = require('./autoConnectView');
} catch (err) {
    autoConnectLoadError = err;
}

let log = null;
let logFilePath = null;
// Directory of the extension's own log file, recorded by initLog(). The crash
// log lives next to it (see getCrashLogPath). Declared here, before initLog()
// runs, so the assignment below is not a temporal-dead-zone reference.
let crashLogDir = null;
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
            // Keep the crash log next to the normal log (see getCrashLogPath).
            crashLogDir = path.dirname(logFilePath);
            fs.writeFileSync(logFilePath, '');
        } catch (e) {
            logFilePath = null;
        }
    }
    return log;
}
function logLine(msg) {
    const line = '[' + new Date().toISOString() + '] ' + msg;
    // appendLine() throws "Channel has been closed" once the extension host is
    // being torn down (window close / reload). logLine() is called from
    // ChildProcess event handlers (e.g. the server's 'exit'), so an unguarded
    // throw there becomes an uncaughtException, and the uncaughtException
    // handler then fails the same way -> Node aborts the host with SIGABRT
    // (exit code 134). Guard it exactly like crashLog() does.
    try { if (log) log.appendLine(line); } catch (e) { /* channel closed */ }
    if (logFilePath) {
        try { require('fs').appendFileSync(logFilePath, line + '\n'); } catch (e) { /* ignore */ }
    }
}

// ---- crash log -------------------------------------------------------
//
// An exception thrown from activate() (or an unhandled rejection anywhere in
// the extension) makes VS Code show a one-line notification and then disables
// the extension: the Extension Development Host window closes and nothing is
// written to the OutputChannel, because initLog() may not have run yet and the
// channel dies with the host. That made "the extension no longer starts"
// impossible to diagnose.
//
// crashLog() therefore writes to a plain file that does not depend on the
// extension host staying alive, and is also called from the process-level
// handlers installed by installCrashHandlers().
let crashLogPath = null;
function getCrashLogPath() {
    if (crashLogPath) return crashLogPath;
    try {
        const path = require('path');
        const os = require('os');
        crashLogPath = path.join(crashLogDir || os.tmpdir(), 'svlsp-extension-crash.log');
    } catch (e) {
        crashLogPath = null;
    }
    return crashLogPath;
}

/** Appends a crash record (message + stack) to the crash log and the channel. */
function crashLog(where, err) {
    const detail = err && err.stack ? err.stack : String(err);
    const line = '[' + new Date().toISOString() + '] ' + where + ': ' + detail;
    // OutputChannel first (visible while the host is alive), then the file.
    try { if (log) log.appendLine(line); } catch (e) { /* ignore */ }
    try { if (logFilePath) require('fs').appendFileSync(logFilePath, line + '\n'); } catch (e) { /* ignore */ }
    const file = getCrashLogPath();
    if (file) {
        try { require('fs').appendFileSync(file, line + '\n'); } catch (e) { /* ignore */ }
    }
    return file;
}

/**
 * Installs process-level handlers so an exception that escapes activate() or
 * any unhandled promise rejection is written to the crash log instead of
 * silently killing the extension host.
 */
// Guards against registering the process-level handlers more than once.
//
// activate() runs again on every "Reload Window" / extension host restart in
// the same process, and process.on() appends a listener each time. After ten
// activations Node prints
//   MaxListenersExceededWarning: Possible EventEmitter memory leak detected.
//   11 uncaughtException listeners added to [process].
// and every crash is then reported N times. Worse, the handlers are what keep
// the host alive on an uncaught exception, so a pile of them makes the failure
// path itself unreliable. Register exactly once per process.
let crashHandlersInstalled = false;
function installCrashHandlers() {
    if (crashHandlersInstalled) return;
    crashHandlersInstalled = true;
    process.on('uncaughtException', (err) => {
        // This handler must never throw. If it does, Node aborts the whole
        // extension host with SIGABRT (exit code 134) instead of just logging
        // the original error. showErrorMessage() rejects/throws once the host
        // is shutting down ("Channel has been closed"), so it is wrapped and
        // its promise is explicitly caught.
        const file = crashLog('uncaughtException', err);
        try {
            const shown = vscode.window.showErrorMessage(
                'SystemVerilog LSP crashed: ' + (err && err.message) +
                (file ? ' (see ' + file + ')' : ''));
            if (shown && typeof shown.catch === 'function') shown.catch(() => { /* host closing */ });
        } catch (e) { /* ignore */ }
    });
    process.on('unhandledRejection', (reason) => {
        // "Canceled" rejections are normal when the host is torn down; do not
        // spam the crash log with them.
        const message = reason && reason.message ? String(reason.message) : String(reason);
        if (message === 'Canceled' || message === 'Canceled: Canceled') return;
        crashLog('unhandledRejection', reason);
    });
}
/** Reveals the OutputChannel (and reports the log file path). */
function showLog() {
    // Same reasoning as logLine(): the channel may already be closed.
    try { if (log) log.show(true); } catch (e) { /* channel closed */ }
    if (logFilePath) {
        try { vscode.window.setStatusBarMessage('SystemVerilog LSP log: ' + logFilePath, 8000); } catch (e) { /* ignore */ }
    }
}

// ---- raw JSON-RPC (LSP over stdio) client, no external dependencies ----

/**
 * Transport abstraction so the client can talk to either a spawned server
 * process (stdio, the default) or a server that is already listening on a TCP
 * port (debugging: the C# debugger launches the server, see
 * SVLSP_SERVER_PORT below).
 *
 * A transport exposes:
 *   onData(cb)    - raw bytes from the server
 *   onStderr(cb)  - server diagnostics (process transport only)
 *   onExit(cb)    - the connection ended
 *   write(buffer) - send raw bytes to the server
 *   kill()        - tear the connection down
 */
function createProcessTransport(proc) {
    return {
        onData: (cb) => proc.stdout.on('data', cb),
        onStderr: (cb) => proc.stderr.on('data', cb),
        onExit: (cb) => proc.on('exit', (code) => cb(code)),
        write: (buffer) => {
            if (proc.stdin.writable) proc.stdin.write(buffer);
        },
        kill: () => { try { proc.kill(); } catch (e) { /* ignore */ } },
    };
}

class RawLspClient {
    constructor(transport) {
        this.transport = transport;
        this.nextId = 1;
        this.pending = new Map(); // id -> {resolve, reject}
        this.notifications = new Map(); // method -> handler
        this.buffer = Buffer.alloc(0);
        this.closed = false;

        this.transport.onData((chunk) => this.onData(chunk));
        this.transport.onStderr((chunk) => {
            // Route the server's stderr (workspace scan progress, parse
            // failures, full stack traces) into the extension log file so the
            // user can see exactly what the server did.
            const text = chunk.toString();
            console.error('[svlsp server stderr]', text);
            for (const line of text.split(/\r?\n/)) {
                if (line.trim().length > 0) logLine('[server] ' + line);
            }
        });
        this.transport.onExit((code) => {
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
        if (this.closed) return;
        const body = Buffer.from(text, 'utf8');
        const header = Buffer.from('Content-Length: ' + body.length + '\r\n\r\n', 'ascii');
        this.transport.write(Buffer.concat([header, body]));
    }

    onNotification(method, handler) {
        this.notifications.set(method, handler);
    }

    dispose() {
        this.closed = true;
        this.transport.kill();
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
        // Prefer a bundled server. The packaged .vsix ships the published
        // binaries under <extension>/server/ so the extension is self-contained
        // (no separate build / serverPath needed). Development layouts
        // (vscode-extension/ inside the repo, or a sibling publish folder) are
        // also tried so F5 debugging keeps working. Falling back to
        // `dotnet run` would execute bin/Debug, which is often stale (a running
        // server keeps the DLL locked, so the rebuild silently fails).
        const exe = 'SystemVerilogLanguageServer.exe';
        const noExt = 'SystemVerilogLanguageServer';
        const candidates = [
            // bundled server (packaged .vsix)
            vscode.Uri.joinPath(context.extensionUri, 'server', exe).fsPath,
            vscode.Uri.joinPath(context.extensionUri, 'server', noExt).fsPath,
            // development: publish folder inside / next to the repo
            vscode.Uri.joinPath(context.extensionUri, 'SystemVerilogLanguageServer', 'publish', exe).fsPath,
            vscode.Uri.joinPath(context.extensionUri, 'SystemVerilogLanguageServer', 'publish', noExt).fsPath,
            vscode.Uri.joinPath(context.extensionUri, '..', 'SystemVerilogLanguageServer', 'publish', exe).fsPath,
            vscode.Uri.joinPath(context.extensionUri, '..', 'SystemVerilogLanguageServer', 'publish', noExt).fsPath,
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
// set by activate(); the Message view (Auxiliary Bar) mirrors the diagnostics
// the server publishes, so it is fed from the publishDiagnostics handler.
let messageViewProviderRef = null;

function withClient(fn) {
    return ensureClient().then(fn, (err) => {
        logLine('ERROR: ' + (err && err.stack ? err.stack : err.message));
        // Never let the error report itself throw: this runs inside a promise
        // rejection handler, and an unguarded throw here would surface as an
        // unhandledRejection (and, while the host is closing, as a crash).
        try {
            const shown = vscode.window.showErrorMessage('SystemVerilog LSP: ' + err.message);
            if (shown && typeof shown.catch === 'function') shown.catch(() => { /* host closing */ });
        } catch (e) { /* ignore */ }
        return undefined;
    });
}

/**
 * Debug transport: when SVLSP_SERVER_PORT is set, the server is NOT spawned.
 * Instead the extension connects to a server that is already listening on that
 * loopback port. This is what makes "F5 starts both the extension host and the
 * C# server" work: the C# debugger launches the server (so breakpoints in the
 * server are hit) and the extension host connects to it.
 *
 * The port is read from the environment of the extension host, which the
 * launch configuration in RtlEditor2.code-workspace sets via "env".
 */
function debugServerPort() {
    const raw = process.env.SVLSP_SERVER_PORT;
    if (!raw) return 0;
    const port = parseInt(raw, 10);
    return Number.isFinite(port) && port > 0 ? port : 0;
}

/**
 * Connects to a server already listening on 127.0.0.1:<port>. Retries for a
 * short while because the extension host and the server are launched in
 * parallel by the compound launch: the server may still be starting up.
 */
function connectToDebugServer(port) {
    const net = require('net');
    // Keep this short: while the connection is being retried, getChildren()
    // (and therefore the RTL Hierarchy view) is still waiting, so a long
    // timeout looks like "the tree never loads". The compound launch starts
    // the server first, so a few seconds is plenty; after that the caller
    // falls back to spawning the server.
    const deadline = Date.now() + 10000;
    let attemptCount = 0;
    return new Promise((resolve, reject) => {
        const attempt = () => {
            attemptCount++;
            const socket = net.connect({ host: '127.0.0.1', port });
            // `settled` guards the retry path: once the socket has connected
            // (or the deadline passed) a later 'error' must not start another
            // connection attempt.
            let settled = false;
            // A PERMANENT error listener (not `once`): socket.destroy() can
            // emit a second error (ECONNRESET / ERR_STREAM_DESTROYED) and an
            // unhandled 'error' event would crash the extension host. This was
            // the source of the "unable to ... at Socket.<anonymous>" crash.
            socket.on('error', (err) => {
                if (settled) {
                    logLine('debug server socket error (ignored): ' + err.message);
                    return;
                }
                settled = true;
                socket.destroy();
                if (Date.now() > deadline) {
                    reject(new Error(
                        'could not connect to the debug server on 127.0.0.1:' + port +
                        ' after ' + attemptCount + ' attempt(s): ' + err.message +
                        '. Start "Launch: SystemVerilog Language Server (C#)" (or the ' +
                        '"Extension + Language Server" compound) first, or unset ' +
                        'SVLSP_SERVER_PORT to let the extension spawn the server itself.'));
                    return;
                }
                // Log the first attempt and then every 8th (~2s) so a wait is
                // visible in the log instead of looking like a hang.
                if (attemptCount === 1 || attemptCount % 8 === 0) {
                    logLine('waiting for the debug server on 127.0.0.1:' + port +
                        ' (attempt ' + attemptCount + '): ' + err.message);
                }
                setTimeout(attempt, 250);
            });
            socket.once('connect', () => {
                settled = true;
                logLine('connected to debug server on 127.0.0.1:' + port);
                resolve(socket);
            });
        };
        attempt();
    });
}

/**
 * Spawns the server and wraps it in a client (the normal, packaged path).
 *
 * Returns { client, spawnFailed }:
 *   - client:      the RawLspClient (already wired to its transport)
 *   - spawnFailed: a promise that rejects if the process could not be started
 *                  (missing binary, permission), so the pending initialize is
 *                  rejected instead of hanging forever.
 */
function createSpawnedClient() {
    const server = resolveServerCommand(getContext());
    if (!server) {
        return {
            client: null,
            spawnFailed: Promise.reject(new Error(
                'server executable not found. The extension expects a bundled server under <extension>/server/ ' +
                '(or set "systemverilogLsp.serverPath" to a published SystemVerilogLanguageServer binary). ' +
                'To build it: dotnet publish SystemVerilogLanguageServer/SystemVerilogLanguageServer.csproj -c Release'
            )),
        };
    }

    logLine('starting server: ' + server.command + ' ' + server.args.join(' '));
    const proc = spawn(server.command, server.args, { stdio: ['pipe', 'pipe', 'pipe'] });
    // These run from ChildProcess event handlers. logLine() is guarded, but the
    // handlers are also wrapped so a throw can never escape into
    // uncaughtException while the host is shutting down.
    proc.on('error', (err) => { try { logLine('server spawn error: ' + err.message); } catch (e) { /* ignore */ } });
    proc.on('exit', (code) => { try { logLine('server exited with code ' + code); } catch (e) { /* ignore */ } });
    // A spawn failure (missing binary, permission) must reject the pending
    // initialize instead of leaving the promise pending forever.
    const spawnFailed = new Promise((_, reject) => {
        proc.on('error', (err) => reject(new Error('server spawn failed: ' + err.message)));
    });
    return { client: new RawLspClient(createProcessTransport(proc)), spawnFailed };
}

/**
 * Creates the LSP client for the current transport.
 *
 * When SVLSP_SERVER_PORT is set the extension first tries to CONNECT to a
 * server that is already listening (the C# debugger launched it, so
 * breakpoints in the server work). If that connection cannot be established
 * the extension falls back to spawning the server, so "Run Extension" alone
 * still works even though the launch configuration sets the variable.
 */
async function createClient() {
    const debugPort = debugServerPort();
    if (debugPort > 0) {
        logLine('debug transport: connecting to 127.0.0.1:' + debugPort + ' (server is not spawned)');
        let socket;
        try {
            socket = await connectToDebugServer(debugPort);
        } catch (err) {
            // Not fatal: fall back to spawning the server so a plain
            // "Run Extension" (without the C# launch) keeps working.
            logLine('debug transport unavailable, falling back to spawning the server: ' + err.message);
            return createSpawnedClient();
        }
        const transport = {
            onData: (cb) => socket.on('data', cb),
            onStderr: () => { /* no separate stderr over TCP */ },
            onExit: (cb) => {
                socket.on('close', () => cb('socket closed'));
                socket.on('error', (err) => cb('socket error: ' + err.message));
            },
            write: (buffer) => { if (socket.writable) socket.write(buffer); },
            kill: () => { try { socket.destroy(); } catch (e) { /* ignore */ } },
        };
        return {
            client: new RawLspClient(transport),
            // The connection is already up, so nothing can fail here.
            spawnFailed: new Promise(() => { /* never rejects */ }),
        };
    }

    return createSpawnedClient();
}

function ensureClient() {
    if (initPromise) return initPromise;
    if (client) return Promise.resolve(client);

    // createClient() may await the TCP connection, so the whole setup runs
    // inside initPromise: concurrent callers (getChildren, hover, ...) all
    // await the same promise instead of racing to create a second client.
    initPromise = createClient().then((created) => {
        if (!created.client) {
            // No client could be created: surface the reason to the caller.
            return created.spawnFailed;
        }
        client = created.client;
        return wireClient(client, created.spawnFailed);
    }).catch((err) => {
        // Allow a later retry (e.g. the user starts the server and reloads).
        client = null;
        initPromise = null;
        throw err;
    });
    return initPromise;
}

/**
 * Registers the server notifications and performs the LSP handshake.
 * Split out of ensureClient so the transport setup (which may be async) stays
 * readable.
 *
 * @param {RawLspClient} client
 * @param {Promise<never>} spawnFailed rejects if the transport died before the
 *        handshake finished (spawn error). It never resolves, so it is only
 *        used to fail fast via Promise.race.
 */
function wireClient(client, spawnFailed) {
    // Server-initiated "the hierarchy changed" push (non-standard notification,
    // vendor prefix rtl/). The server sends it on didSave / watched-file
    // changes / workspace-folder changes / workspace-root inference, so the
    // tree redraws without the user pressing refresh. The tree is fetched
    // lazily per expanded node, so a plain redraw is enough.
    client.onNotification('rtl/hierarchyInvalidated', (params) => {
        const reason = params && params.reason ? params.reason : '';
        logLine('hierarchy invalidated by server: ' + reason);
        if (hierarchyProviderRef) hierarchyProviderRef.refreshTree();
    });

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
        // Mirror the same diagnostics into the Message view (the editor's
        // MessageView equivalent). The view is a renderer for data the
        // extension already holds, so no extra server protocol is involved.
        if (messageViewProviderRef) messageViewProviderRef.update(params.uri, diags);
    });

    return Promise.race([
        client.request('initialize', {
            processId: process.pid,
            rootUri: workspaceRootUri(),
            workspaceFolders: workspaceFolderParams(),
            capabilities: {},
        }, 120000),
        spawnFailed,
    ]).then((result) => {
        logLine('initialize succeeded');
        // Adopt the server's semantic-token legend instead of keeping a copy
        // here: the token type order is defined once, on the C# side.
        try {
            const legend = result && result.capabilities &&
                result.capabilities.semanticTokensProvider &&
                result.capabilities.semanticTokensProvider.legend;
            if (legend && Array.isArray(legend.tokenTypes) && legend.tokenTypes.length > 0) {
                tokenTypesByIndex = legend.tokenTypes.slice();
                logLine('semantic token legend from server: ' + tokenTypesByIndex.join(','));
            }
        } catch (e) {
            logLine('could not read the server legend: ' + (e && e.message));
        }
        client.send('initialized', {});
        // sync already-open documents
        for (const doc of vscode.workspace.textDocuments) {
            maybeOpenDocument(doc);
        }
        // The RTL Hierarchy view may have rendered before the session existed
        // (getChildren awaits ensureClient, but the very first render can race
        // the workspace scan). Redraw now that the server is ready; the server
        // also pushes rtl/hierarchyInvalidated once it infers a workspace root.
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
                if (messageViewProviderRef) messageViewProviderRef.remove(key);
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
            // The server may return a snippet body (e.g. "begin ... end") so that
            // block keywords expand like the desktop editor's autocomplete.
            if (item.insertText) {
                if (item.insertTextFormat === 2) {
                    ci.insertText = new vscode.SnippetString(item.insertText);
                } else {
                    ci.insertText = item.insertText;
                }
            }
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

// semantic tokens: build a vscode.SemanticTokens from the server response.
//
// The legend is NOT hard-coded here: it is read from the server's initialize
// result (semanticTokensProvider.legend), so the token type order can only be
// defined in one place (the C# side). This fallback is used only until the
// initialize result has been seen.
let tokenTypesByIndex = ['keyword', 'comment', 'string', 'number', 'macro',
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

// ---- standard LSP features wired but not implemented yet ----
// Each provider forwards to the server and maps the (currently empty) result
// into the vscode type. The C# side returns empty collections until the real
// implementation lands, so these are pure plumbing: no logic lives here.

function provideFormatting(doc) {
    return client.request('textDocument/formatting', {
        textDocument: { uri: doc.uri.toString() },
        options: { tabSize: 4, insertSpaces: true },
    }).then((result) => toTextEdits(result));
}

function provideRangeFormatting(doc, range) {
    return client.request('textDocument/rangeFormatting', {
        textDocument: { uri: doc.uri.toString() },
        range: toLspRange(range),
        options: { tabSize: 4, insertSpaces: true },
    }).then((result) => toTextEdits(result));
}

function provideRename(doc, pos, newName) {
    return client.request('textDocument/rename', positionParams(doc, pos, { newName }))
        .then((result) => toWorkspaceEdit(result));
}

function providePrepareRename(doc, pos) {
    return client.request('textDocument/prepareRename', positionParams(doc, pos))
        .then((result) => {
            if (!result || !result.range) return null;
            return { range: toRange(result.range), placeholder: result.placeholder };
        });
}

function provideCodeActions(doc, range, context) {
    return client.request('textDocument/codeAction', {
        textDocument: { uri: doc.uri.toString() },
        range: toLspRange(range),
        context: {
            diagnostics: (context.diagnostics || []).map((d) => ({
                range: toLspRange(d.range),
                message: d.message,
                severity: d.severity,
                code: d.code,
            })),
        },
    }).then((result) => (result || []).map(toCodeAction).filter((a) => a));
}

function provideCodeLenses(doc) {
    return client.request('textDocument/codeLens', {
        textDocument: { uri: doc.uri.toString() },
    }).then((result) => (result || []).map(toCodeLens).filter((l) => l));
}

function provideSignatureHelp(doc, pos) {
    return client.request('textDocument/signatureHelp', positionParams(doc, pos))
        .then((result) => toSignatureHelp(result));
}

function provideInlayHints(doc, range) {
    return client.request('textDocument/inlayHint', {
        textDocument: { uri: doc.uri.toString() },
        range: toLspRange(range),
    }).then((result) => (result || []).map(toInlayHint).filter((h) => h));
}

function provideDeclaration(doc, pos) {
    return client.request('textDocument/declaration', positionParams(doc, pos))
        .then((result) => toLocations(result));
}

function provideTypeDefinition(doc, pos) {
    return client.request('textDocument/typeDefinition', positionParams(doc, pos))
        .then((result) => toLocations(result));
}

function provideImplementation(doc, pos) {
    return client.request('textDocument/implementation', positionParams(doc, pos))
        .then((result) => toLocations(result));
}

function provideDocumentHighlights(doc, pos) {
    return client.request('textDocument/documentHighlight', positionParams(doc, pos))
        .then((result) => (result || []).map((h) => {
            if (!h || !h.range) return null;
            return new vscode.DocumentHighlight(toRange(h.range), h.kind);
        }).filter((h) => h));
}

function provideFoldingRanges(doc) {
    return client.request('textDocument/foldingRange', {
        textDocument: { uri: doc.uri.toString() },
    }).then((result) => (result || []).map((r) => {
        if (!r) return null;
        return new vscode.FoldingRange(r.startLine, r.endLine, r.kind);
    }).filter((r) => r));
}

function provideSelectionRanges(doc, positions) {
    return client.request('textDocument/selectionRange', {
        textDocument: { uri: doc.uri.toString() },
        positions: positions.map((p) => ({ line: p.line, character: p.character })),
    }).then((result) => (result || []).map(toSelectionRange).filter((r) => r));
}

function provideDocumentLinks(doc) {
    return client.request('textDocument/documentLink', {
        textDocument: { uri: doc.uri.toString() },
    }).then((result) => (result || []).map((l) => {
        if (!l || !l.range) return null;
        return new vscode.DocumentLink(toRange(l.range), l.target ? vscode.Uri.parse(l.target) : undefined);
    }).filter((l) => l));
}

function provideWorkspaceSymbols(query) {
    return client.request('workspace/symbol', { query: query || '' })
        .then((result) => (result || []).map((s) => {
            if (!s || !s.location) return null;
            return new vscode.SymbolInformation(
                s.name, s.kind,
                s.location.range ? toRange(s.location.range) : undefined,
                vscode.Uri.parse(s.location.uri));
        }).filter((s) => s));
}

// ---- LSP <-> vscode conversion helpers (pure mapping, no logic) ----

function toRange(r) {
    return new vscode.Range(r.start.line, r.start.character, r.end.line, r.end.character);
}

function toLspRange(r) {
    return {
        start: { line: r.start.line, character: r.start.character },
        end: { line: r.end.line, character: r.end.character },
    };
}

function toTextEdits(result) {
    return (result || []).map((e) => {
        if (!e || !e.range) return null;
        return new vscode.TextEdit(toRange(e.range), e.newText);
    }).filter((e) => e);
}

function toWorkspaceEdit(result) {
    if (!result || !result.changes) return null;
    const edit = new vscode.WorkspaceEdit();
    for (const uri of Object.keys(result.changes)) {
        for (const e of result.changes[uri] || []) {
            if (e && e.range) edit.replace(vscode.Uri.parse(uri), toRange(e.range), e.newText);
        }
    }
    return edit;
}

function toCodeAction(a) {
    if (!a) return null;
    const action = new vscode.CodeAction(a.title, a.kind);
    if (a.edit) action.edit = toWorkspaceEdit(a.edit);
    return action;
}

function toCodeLens(l) {
    if (!l || !l.range) return null;
    const lens = new vscode.CodeLens(toRange(l.range));
    if (l.command) {
        lens.command = {
            title: l.command.title,
            command: l.command.command,
            arguments: l.command.arguments,
        };
    }
    return lens;
}

function toSignatureHelp(result) {
    if (!result || !result.signatures || result.signatures.length === 0) return null;
    const help = new vscode.SignatureHelp();
    help.signatures = result.signatures.map((s) => {
        const info = new vscode.SignatureInformation(s.label);
        if (s.documentation) {
            info.documentation = new vscode.MarkdownString(s.documentation.value);
        }
        if (s.parameters) {
            info.parameters = s.parameters.map((p) => new vscode.ParameterInformation(p.label));
        }
        return info;
    });
    help.activeSignature = result.activeSignature || 0;
    help.activeParameter = result.activeParameter || 0;
    return help;
}

function toInlayHint(h) {
    if (!h || !h.position) return null;
    const hint = new vscode.InlayHint(
        new vscode.Position(h.position.line, h.position.character), h.label);
    if (h.kind !== undefined && h.kind !== null) hint.kind = h.kind;
    return hint;
}

function toSelectionRange(r) {
    if (!r || !r.range) return null;
    const range = toRange(r.range);
    const parent = r.parent ? toSelectionRange(r.parent) : undefined;
    return new vscode.SelectionRange(range, parent);
}

// activate() can run more than once in the same extension host process (a
// "Reload Window" re-activates without restarting the process). Registering the
// providers / commands / listeners a second time would duplicate every event
// handler, so the body runs once and later calls only refresh the context.
let activated = false;
function activate(context) {
    context_ = context;
    initLog(context);
    installCrashHandlers();
    logLine('extension activated');
    logLine('log file: ' + (logFilePath || '<unavailable>'));
    logLine('crash log: ' + (getCrashLogPath() || '<unavailable>'));
    logLine('workspaceFolders: ' + JSON.stringify(workspaceFolderParams()));
    logLine('rootUri: ' + workspaceRootUri());

    if (activated) {
        logLine('activate called again in the same host; providers already registered');
        return;
    }
    activated = true;

    // Everything below registers providers / commands. A throw here disables
    // the whole extension (VS Code reports it once and stops activating), so
    // record it in the crash log before re-throwing: without this the failure
    // left no trace anywhere.
    try {
        activateCore(context);
    } catch (err) {
        const file = crashLog('activate', err);
        try {
            const shown = vscode.window.showErrorMessage(
                'SystemVerilog LSP failed to activate: ' + (err && err.message) +
                (file ? ' (see ' + file + ')' : ''));
            if (shown && typeof shown.catch === 'function') shown.catch(() => { /* host closing */ });
        } catch (e) { /* ignore */ }
        throw err;
    }
}

/** Registers the providers, views and commands (the body of activate). */
function activateCore(context) {

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
    if (!registerHierarchy) {
        crashLog('require(./hierarchy)', hierarchyLoadError);
        throw new Error('hierarchy module failed to load: ' +
            (hierarchyLoadError && hierarchyLoadError.message));
    }
    const hierarchyProvider = registerHierarchy(context, () => ensureClient(), logLine, showLog);
    hierarchyProviderRef = hierarchyProvider;

    // Message view (RtlEditor2's MessageView equivalent) in the Auxiliary Bar,
    // i.e. a pane on the right-hand side of the editor. It renders the
    // diagnostics the server already publishes, so it needs no extra protocol.
    //
    // The module is optional: if it failed to load (see the guarded require at
    // the top of this file) the rest of the extension must still activate.
    if (registerMessageView) {
        try {
            messageViewProviderRef = registerMessageView(context, logLine);
        } catch (err) {
            crashLog('registerMessageView', err);
            logLine('Message view disabled: ' + (err && err.message));
        }
    } else {
        crashLog('require(./messageView)', messageViewLoadError);
        logLine('Message view disabled: ' + (messageViewLoadError && messageViewLoadError.message));
    }
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
    // Forward disk changes to the server as workspace/didChangeWatchedFiles so
    // it can keep its file table in sync (the handler is a no-op for now; the
    // hierarchy view still picks changes up through its explicit rescan).
    const sendWatchedFiles = (uris, type) => {
        if (!client) return;
        client.send('workspace/didChangeWatchedFiles', {
            changes: (uris || []).map((u) => ({
                uri: (u && u.toString) ? u.toString() : String(u),
                type,
            })),
        });
    };
    context.subscriptions.push(
        sourceWatcher.onDidCreate((uri) => { sendWatchedFiles([uri], 1); hierarchyProvider.refresh(); })
    );
    context.subscriptions.push(
        sourceWatcher.onDidChange((uri) => { sendWatchedFiles([uri], 2); })
    );
    context.subscriptions.push(
        sourceWatcher.onDidDelete((uri) => { sendWatchedFiles([uri], 3); hierarchyProvider.refresh(); })
    );
    context.subscriptions.push(
        vscode.workspace.onDidRenameFiles((e) => {
            const deleted = (e.files || []).map((f) => f.oldUri);
            const created = (e.files || []).map((f) => f.newUri);
            sendWatchedFiles(deleted, 3);
            sendWatchedFiles(created, 1);
            hierarchyProvider.refresh();
        })
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
    // write the palette into user settings when absent.
    //
    // The palette itself comes from the server (rtl/theme), which derives it
    // from the plugin's CodeDrawStyle: the editor and the extension then share
    // one source of truth instead of the extension keeping its own copy.
    const existingColors = editorConfig.inspect('semanticTokenColorCustomizations');
    // Values this extension wrote in the past and that are now known to be
    // wrong. They must be replaced, otherwise the stale palette sticks in the
    // user's settings forever:
    //   #D4D4D4 - the very first hard-coded default
    //   #4EC9B0 - identifier mapped to ColorType.Identifier (teal), which made
    //             every plain reference teal instead of the default foreground
    const staleIdentifierColors = ['#D4D4D4', '#4EC9B0'];
    const existingIdentifier = existingColors.globalValue &&
        existingColors.globalValue.rules && existingColors.globalValue.rules.identifier;
    const prevDefault = typeof existingIdentifier === 'string' &&
        staleIdentifierColors.indexOf(existingIdentifier.toUpperCase()) >= 0;
    if (prevDefault) {
        logLine('replacing a stale semanticTokenColorCustomizations palette (identifier ' +
            existingIdentifier + ')');
    }
    if (!existingColors.globalValue && !existingColors.workspaceValue || prevDefault) {
        withClient(async () => {
            const theme = await client.request('rtl/theme', {});
            if (!theme || !theme.rules) {
                logLine('rtl/theme returned no rules; leaving colours untouched');
                return;
            }
            await editorConfig.update(
                'semanticTokenColorCustomizations',
                { rules: theme.rules },
                vscode.ConfigurationTarget.Global);
            logLine('semanticTokenColorCustomizations written from rtl/theme (user settings)');
        });
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
    // ---- standard LSP features wired but not implemented yet ----
    // The server advertises these capabilities and returns empty results until
    // the C# implementation lands, so registering the providers now makes the
    // round-trip work end to end without any logic on this side.
    const svDocSelector = [
        { scheme: 'file', language: 'systemverilog' },
        { scheme: 'file', language: 'verilog' },
    ];
    context.subscriptions.push(
        vscode.languages.registerDocumentFormattingEditProvider(
            svDocSelector,
            { provideDocumentFormattingEdits: (doc) => withClient(() => provideFormatting(doc)) })
    );
    context.subscriptions.push(
        vscode.languages.registerDocumentRangeFormattingEditProvider(
            svDocSelector,
            { provideDocumentRangeFormattingEdits: (doc, range) => withClient(() => provideRangeFormatting(doc, range)) })
    );
    context.subscriptions.push(
        vscode.languages.registerRenameProvider(
            svDocSelector,
            {
                provideRenameEdits: (doc, pos, newName) => withClient(() => provideRename(doc, pos, newName)),
                prepareRename: (doc, pos) => withClient(() => providePrepareRename(doc, pos)),
            })
    );
    context.subscriptions.push(
        vscode.languages.registerCodeActionsProvider(
            svDocSelector,
            { provideCodeActions: (doc, range, ctx) => withClient(() => provideCodeActions(doc, range, ctx)) })
    );
    context.subscriptions.push(
        vscode.languages.registerCodeLensProvider(
            svDocSelector,
            { provideCodeLenses: (doc) => withClient(() => provideCodeLenses(doc)) })
    );
    context.subscriptions.push(
        vscode.languages.registerSignatureHelpProvider(
            svDocSelector,
            { provideSignatureHelp: (doc, pos) => withClient(() => provideSignatureHelp(doc, pos)) },
            '(', ',')
    );
    context.subscriptions.push(
        vscode.languages.registerInlayHintsProvider(
            svDocSelector,
            { provideInlayHints: (doc, range) => withClient(() => provideInlayHints(doc, range)) })
    );
    context.subscriptions.push(
        vscode.languages.registerDeclarationProvider(
            svDocSelector,
            { provideDeclaration: (doc, pos) => withClient(() => provideDeclaration(doc, pos)) })
    );
    context.subscriptions.push(
        vscode.languages.registerTypeDefinitionProvider(
            svDocSelector,
            { provideTypeDefinition: (doc, pos) => withClient(() => provideTypeDefinition(doc, pos)) })
    );
    context.subscriptions.push(
        vscode.languages.registerImplementationProvider(
            svDocSelector,
            { provideImplementation: (doc, pos) => withClient(() => provideImplementation(doc, pos)) })
    );
    context.subscriptions.push(
        vscode.languages.registerDocumentHighlightProvider(
            svDocSelector,
            { provideDocumentHighlights: (doc, pos) => withClient(() => provideDocumentHighlights(doc, pos)) })
    );
    context.subscriptions.push(
        vscode.languages.registerFoldingRangeProvider(
            svDocSelector,
            { provideFoldingRanges: (doc) => withClient(() => provideFoldingRanges(doc)) })
    );
    context.subscriptions.push(
        vscode.languages.registerSelectionRangeProvider(
            svDocSelector,
            { provideSelectionRanges: (doc, positions) => withClient(() => provideSelectionRanges(doc, positions)) })
    );
    context.subscriptions.push(
        vscode.languages.registerDocumentLinkProvider(
            svDocSelector,
            { provideDocumentLinks: (doc) => withClient(() => provideDocumentLinks(doc)) })
    );
    context.subscriptions.push(
        vscode.languages.registerWorkspaceSymbolProvider(
            { provideWorkspaceSymbols: (query) => withClient(() => provideWorkspaceSymbols(query)) })
    );
    logLine('standard LSP providers registered (formatting / rename / codeAction / codeLens / signatureHelp / inlayHint / declaration / typeDefinition / implementation / documentHighlight / foldingRange / selectionRange / documentLink / workspaceSymbol)');

    // Snippets: the text is produced by the server (rtl/snippet); the
    // extension only inserts it.
    registerSnippetCommands(context);

    // Auto-connect form: the rows come from the server (rtl/autoConnect) and
    // the chosen connections go back through rtl/autoConnectApply. The
    // extension only renders the webview and forwards the choices.
    registerAutoConnectCommand(context);

    try {
        // The legend is the server's (adopted from initialize); the fallback
        // above only covers the window before initialize has answered.
        const legend = new vscode.SemanticTokensLegend(tokenTypesByIndex.slice(), []);
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
            // fetch may come back empty. Redraw once the document is open; the
            // server also pushes rtl/hierarchyInvalidated when it infers the
            // root, so the tree fills in without the user pressing refresh.
            hierarchyProvider.refreshIfEmpty();
        }
    }));
}

// ---- snippets (rtl/snippet) -------------------------------------------
//
// The snippet logic (what to insert: port enumeration, parameter overrides,
// connection generation, port inversion, modport derivation) lives in the
// server. The extension only performs the mechanical part: ask the server for
// the text, then insert it with TextEditor.insertSnippet (or replace a range).
// This keeps the extension a pure pipe, per the architecture note in
// memory/vscode_extension_architecture.md.

/** Converts a vscode Position to the LSP line/character shape. */
function toLspPosition(pos) {
    return { line: pos.line, character: pos.character };
}

/**
 * Requests a snippet from the server and applies it.
 *
 * @param {string} kind one of the server's snippet names
 *        (alwaysFF / moduleInstance / portConnectionCreate / portInvert / modport)
 * @param {string} [argument] extra argument (module name for moduleInstance)
 */
async function applySnippet(kind, argument) {
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

    const params = {
        textDocument: { uri: doc.uri.toString() },
        position: toLspPosition(editor.selection.active),
        kind,
    };
    if (argument) params.argument = argument;
    if (!editor.selection.isEmpty) {
        params.selection = {
            start: toLspPosition(editor.selection.start),
            end: toLspPosition(editor.selection.end),
        };
    }

    let result;
    try {
        const c = await ensureClient();
        result = await c.request('rtl/snippet', params);
    } catch (e) {
        logLine('rtl/snippet ERROR: ' + (e && e.stack ? e.stack : e));
        vscode.window.showErrorMessage('SystemVerilog LSP: snippet request failed (see the log).');
        return;
    }

    if (!result) {
        vscode.window.showWarningMessage('SystemVerilog LSP: no snippet returned.');
        return;
    }

    // The server may need a further choice (e.g. which module to instantiate).
    if ((!result.snippets || result.snippets.length === 0) && result.candidates && result.candidates.length > 0) {
        const picked = await vscode.window.showQuickPick(result.candidates, {
            placeHolder: 'Select a module to instantiate',
        });
        if (!picked) return;
        return applySnippet(kind, picked);
    }

    if (!result.snippets || result.snippets.length === 0) {
        const message = result.message || 'no snippet available at the caret';
        vscode.window.showInformationMessage('SystemVerilog LSP: ' + message);
        return;
    }

    for (const snippet of result.snippets) {
        await applyOneSnippet(editor, doc, snippet);
    }
}

/** Applies one snippet entry (insert at the caret, or replace a range). */
async function applyOneSnippet(editor, doc, snippet) {
    const snippetString = new vscode.SnippetString(snippet.snippetText);

    if (snippet.kind === 'replace') {
        const range = new vscode.Range(doc.positionAt(snippet.start), doc.positionAt(snippet.end));
        await editor.insertSnippet(snippetString, range);
    } else {
        await editor.insertSnippet(snippetString, editor.selection.active);
    }
    logLine('snippet applied: ' + (snippet.label || snippet.kind));
}

/** Registers the snippet commands. */
function registerSnippetCommands(context) {
    const commands = [
        ['systemverilogLsp.snippet.alwaysFF', 'alwaysFF', undefined],
        ['systemverilogLsp.snippet.moduleInstance', 'moduleInstance', undefined],
        ['systemverilogLsp.snippet.portConnectionCreate', 'portConnectionCreate', undefined],
        ['systemverilogLsp.snippet.portInvert', 'portInvert', undefined],
        ['systemverilogLsp.snippet.modport', 'modport', undefined],
    ];
    for (const [command, kind, argument] of commands) {
        context.subscriptions.push(
            vscode.commands.registerCommand(command, () => applySnippet(kind, argument))
        );
    }
    logLine('snippet commands registered (alwaysFF / moduleInstance / portConnectionCreate / portInvert / modport)');
}

/**
 * Registers the auto-connect command.
 *
 * The form itself (which signal matches a port) is decided by the server; this
 * only opens the webview and forwards the user's choices back.
 */
function registerAutoConnectCommand(context) {
    if (!autoConnectView) {
        crashLog('autoConnectView', autoConnectLoadError || new Error('module not loaded'));
        logLine('auto-connect command NOT registered (autoConnectView.js failed to load)');
        return;
    }
    context.subscriptions.push(
        vscode.commands.registerCommand('systemverilogLsp.autoConnect', () =>
            autoConnectView.showAutoConnect({ ensureClient, log: logLine }))
    );
    logLine('auto-connect command registered');
}

function deactivate() {
    // deactivate() runs while the host is being torn down, so every step is
    // guarded: a throw here is reported as a failed deactivation and can take
    // the host down with it.
    //
    // The process-level crash handlers are intentionally NOT removed: they are
    // installed once per process (see installCrashHandlers) and must stay in
    // place for the next activation in the same process.
    try {
        if (client) {
            if (client.disposables) {
                for (const d of client.disposables) {
                    try { d.dispose(); } catch (e) { /* ignore */ }
                }
            }
            client.dispose();
            client = null;
        }
    } catch (e) { /* ignore */ }
    try { openedDocs.clear(); } catch (e) { /* ignore */ }
    try { if (diagnosticCollection) diagnosticCollection.clear(); } catch (e) { /* ignore */ }
    // Allow the next activation to register everything again.
    activated = false;
}

module.exports = { activate, deactivate };
