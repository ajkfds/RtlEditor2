'use strict';

const vscode = require('vscode');
const {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind,
} = require('vscode-languageclient/node');

let client = null;

function resolveServerCommand(context) {
    const config = vscode.workspace.getConfiguration('systemverilogLsp');
    let serverPath = config.get('serverPath', '');

    // Default: look for the published server next to the repository layout
    // (../SystemVerilogLanguageServer/publish/SystemVerilogLanguageServer.exe)
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
        return { command: serverPath, args: [], dotnetRun: false };
    }

    // Fallback: dotnet run --project
    const projectCandidates = [
        vscode.Uri.joinPath(context.extensionUri, 'SystemVerilogLanguageServer', 'SystemVerilogLanguageServer.csproj').fsPath,
        vscode.Uri.joinPath(context.extensionUri, '..', 'SystemVerilogLanguageServer', 'SystemVerilogLanguageServer.csproj').fsPath,
    ];
    for (const candidate of projectCandidates) {
        if (require('fs').existsSync(candidate)) {
            return { command: 'dotnet', args: ['run', '--project', candidate], dotnetRun: true };
        }
    }

    return null;
}

function activate(context) {
    const server = resolveServerCommand(context);
    if (!server) {
        vscode.window.showErrorMessage(
            'SystemVerilog LSP: server executable not found. ' +
            'Set "systemverilogLsp.serverPath" to the published SystemVerilogLanguageServer binary, ' +
            'or build it with: dotnet publish SystemVerilogLanguageServer/SystemVerilogLanguageServer.csproj -c Release'
        );
        return;
    }

    const serverOptions = {
        run: { command: server.command, args: server.args, transport: TransportKind.stdio },
        debug: { command: server.command, args: server.args, transport: TransportKind.stdio },
    };

    const clientOptions = {
        documentSelector: [
            { scheme: 'file', language: 'systemverilog' },
            { scheme: 'file', language: 'verilog' },
        ],
        synchronize: {
            // nothing to watch yet; file watching is on the server side (in-memory only)
        },
    };

    client = new LanguageClient(
        'systemverilogLsp',
        'SystemVerilog Language Server',
        serverOptions,
        clientOptions
    );

    client.start();
}

function deactivate() {
    if (client) {
        return client.stop();
    }
    return undefined;
}

module.exports = { activate, deactivate };
