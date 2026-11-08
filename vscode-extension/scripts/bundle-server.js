'use strict';

// Bundles the SystemVerilogLanguageServer publish output into
// vscode-extension/server/ so the packaged .vsix is self-contained.
//
// Usage (run from the vscode-extension folder):
//   node scripts/bundle-server.js
//
// It:
//   1. empties ./server (see below - it MUST start clean)
//   2. runs `dotnet publish` of the server for the current platform into ./server
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

// Start from an EMPTY folder. `dotnet publish -o` only adds / overwrites files;
// it never removes what a previous publish (or a previous dependency graph)
// left behind. Without this the folder silently accumulates: an old
// Playwright node.exe, every platform's native SkiaSharp / HarfBuzzSharp
// binaries, and the .pdb files of packages that ship symbols regardless of
// -p:DebugType=None. That reached 314 files / 734 MB, and the extension
// development host - which has to watch the whole extension folder - was
// killed with SIGABRT (exit code 134) seconds after "Loading development
// extension", with no exthost folder created at all. Keep the folder small:
// it is also what gets shipped inside the .vsix.
rmrf(publishOut);

console.log('publishing server -> ' + publishOut);
// -p:UseAppHost=true keeps the native launcher (SystemVerilogLanguageServer.exe).
// -p:CopyOutputSymbolsToPublishDirectory=false stops the symbols of referenced
// packages (SkiaSharp etc., ~250 MB) from being copied into the publish folder.
execSync(
    `dotnet publish "${csproj}" -c Release -o "${publishOut}" ` +
        `-p:DebugType=None -p:DebugSymbols=false -p:CopyOutputSymbolsToPublishDirectory=false`,
    { stdio: 'inherit', cwd: repoRoot }
);

// Drop what must never reach the debug host / the .vsix:
//   *.pdb          debug symbols (harmless to run, ~250 MB to ship)
//   .playwright/   browser-automation bundle pulled in by a referenced
//                  package (89 MB node.exe + JS tooling); the language
//                  server never uses it
//
// runtimes/ holds the native binaries of EVERY platform the referenced
// packages support (SkiaSharp / HarfBuzzSharp / ...). Only the host platform's
// copy can ever be loaded, so the rest is dead weight in the published
// extension folder (and in the .vsix, which is built on windows-latest).
function prune() {
    let removed = 0;
    const walk = (dir) => {
        for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
            const full = path.join(dir, entry.name);
            if (entry.isDirectory()) {
                if (entry.name === '.playwright') {
                    fs.rmSync(full, { recursive: true, force: true });
                    removed++;
                    continue;
                }
                walk(full);
                continue;
            }
            if (entry.name.toLowerCase().endsWith('.pdb')) {
                fs.rmSync(full, { force: true });
                removed++;
            }
        }
    };
    walk(publishOut);

    // keep only the host platform under runtimes/ (win-x64 on a Windows build)
    const runtimes = path.join(publishOut, 'runtimes');
    if (fs.existsSync(runtimes)) {
        const keep = process.platform === 'win32' ? ['win', 'win-']
            : process.platform === 'darwin' ? ['osx', 'unix'] : ['linux', 'unix'];
        for (const entry of fs.readdirSync(runtimes)) {
            if (keep.some((k) => entry === k.replace(/-$/, '') || entry.startsWith(k))) continue;
            fs.rmSync(path.join(runtimes, entry), { recursive: true, force: true });
            removed++;
        }
    }
    return removed;
}

const pruned = prune();
console.log(`bundled server into ${publishOut} (pruned ${pruned} entries)`);
