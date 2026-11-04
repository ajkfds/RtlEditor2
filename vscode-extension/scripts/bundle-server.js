'use strict';

// Bundles the SystemVerilogLanguageServer publish output into
// vscode-extension/server/ so the packaged .vsix is self-contained.
//
// Usage (run from the vscode-extension folder):
//   node scripts/bundle-server.js
//
// It:
//   1. runs `dotnet publish` of the server for the current platform
//   2. copies the publish output into ./server
//   3. drops *.pdb (debug symbols) to reduce size
//
// Run `npm run package` to bundle + build the .vsix in one step.

const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const extDir = path.resolve(__dirname, '..');
const repoRoot = path.resolve(extDir, '..');
const csproj = path.join(repoRoot, 'SystemVerilogLanguageServer', 'SystemVerilogLanguageServer.csproj');
const publishOut = path.join(extDir, 'server');

if (!fs.existsSync(csproj)) {
    console.error('server project not found: ' + csproj);
    console.error('run this from the RtlEditor2 repository (vscode-extension/scripts).');
    process.exit(1);
}

function rmrf(p) {
    if (fs.existsSync(p)) fs.rmSync(p, { recursive: true, force: true });
}

console.log('publishing server -> ' + publishOut);
// -p:UseAppHost=true keeps the native launcher (SystemVerilogLanguageServer.exe).
execSync(
    `dotnet publish "${csproj}" -c Release -o "${publishOut}" -p:DebugType=None -p:DebugSymbols=false`,
    { stdio: 'inherit', cwd: repoRoot }
);

// remove any leftover pdb (DebugType=None should already skip them)
for (const entry of fs.readdirSync(publishOut)) {
    if (entry.toLowerCase().endsWith('.pdb')) {
        fs.rmSync(path.join(publishOut, entry), { force: true });
    }
}

console.log('bundled server into ' + publishOut);
