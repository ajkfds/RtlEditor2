'use strict';

// Builds the installable .vsix for the SystemVerilog LSP extension.
//
// Usage (from the repository root, or the vscode-extension folder):
//   node vscode-extension/scripts/make-vsix.js
//   node vscode-extension/scripts/make-vsix.js --skip-bundle   # repackage only
//   node vscode-extension/scripts/make-vsix.js --out dist      # copy the .vsix there
//
// It runs the four steps the CI workflow runs, with the checks the workflow
// does NOT do (so a local build fails with a readable message instead of
// producing a broken .vsix):
//
//   1. verify the toolchain (node / dotnet / vsce)
//   2. bundle the language server into server/   (scripts/bundle-server.js)
//   3. sanity check the bundle (must start + answer --selftest)
//   4. package with vsce and report the resulting file
//
// Why the extra sanity check: a .vsix with a missing or stale server is
// installable but every feature silently does nothing (the extension spawns a
// server that cannot start). Running --selftest against the exact bytes that
// get shipped catches that here.

const { execSync, spawnSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const extDir = path.resolve(__dirname, '..');
const repoRoot = path.resolve(extDir, '..');
const serverDir = path.join(extDir, 'server');
const manifestPath = path.join(extDir, 'package.json');

const argv = process.argv.slice(2);
const skipBundle = argv.includes('--skip-bundle');
const outIndex = argv.indexOf('--out');
const outDir = outIndex >= 0 ? path.resolve(extDir, argv[outIndex + 1] || 'dist') : null;

function fail(message, hint) {
    console.error('\n[ERROR] ' + message);
    if (hint) console.error('        ' + hint);
    process.exit(1);
}

function step(message) {
    console.log('\n== ' + message);
}

/** Runs a command and returns its trimmed stdout; exits on failure. */
function run(command, options = {}) {
    const result = spawnSync(command, {
        cwd: options.cwd || extDir,
        shell: true,
        stdio: options.capture ? 'pipe' : 'inherit',
        encoding: 'utf8',
    });
    if (result.error) fail(`${command} could not be started: ${result.error.message}`);
    if (result.status !== 0) {
        fail(`\`${command}\` failed with exit code ${result.status}`,
            options.hint || '');
    }
    return (result.stdout || '').trim();
}

// ---------------------------------------------------------------------------
// 1. toolchain
// ---------------------------------------------------------------------------

step('checking the toolchain');

const nodeMajor = Number(process.versions.node.split('.')[0]);
if (nodeMajor < 18) {
    fail(`node ${process.versions.node} is too old`, 'vsce requires node 18 or newer.');
}
console.log(`  node   ${process.versions.node}`);

try {
    const dotnetVersion = run('dotnet --version', {
        capture: true,
        hint: 'Install the .NET 10 SDK: https://dotnet.microsoft.com/download',
    });
    console.log(`  dotnet ${dotnetVersion}`);
} catch {
    fail('dotnet was not found on PATH', 'Install the .NET 10 SDK.');
}

// vsce is normally a global tool, but "npx --yes" works without installing it.
// Probe once and remember the invocation that works.
let vsceCommand = null;
for (const candidate of ['vsce', 'npx --yes @vscode/vsce']) {
    const probe = spawnSync(candidate.split(' ')[0], candidate.split(' ').slice(1).concat('--version'), {
        cwd: extDir, shell: true, stdio: 'pipe', encoding: 'utf8',
    });
    if (probe.status === 0) {
        vsceCommand = candidate;
        console.log(`  vsce   ${candidate} (${(probe.stdout || '').trim()})`);
        break;
    }
}
if (!vsceCommand) {
    fail('vsce was not found',
        'npm install -g @vscode/vsce   (or let this script use npx by installing node/npm)');
}

const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
if (!manifest.repository || !manifest.repository.url) {
    // vsce refuses to package without it, and the message is cryptic.
    fail('package.json has no repository.url', 'Add a repository field before packaging.');
}

// ---------------------------------------------------------------------------
// 2. bundle the server
// ---------------------------------------------------------------------------

if (skipBundle) {
    step('bundling the language server (skipped: --skip-bundle)');
    if (!fs.existsSync(path.join(serverDir, 'SystemVerilogLanguageServer.dll'))) {
        fail('server/ holds no bundled server, so --skip-bundle cannot be used',
            'Run once without --skip-bundle.');
    }
} else {
    step('bundling the language server');
    // bundle-server.js empties server/ first and prunes symbols / .playwright /
    // the non-host runtimes; skipping either step is what made the folder grow
    // to 734 MB and killed the extension debug host.
    run(`node "${path.join(__dirname, 'bundle-server.js')}"`, { cwd: repoRoot });
}

// ---------------------------------------------------------------------------
// 3. sanity check the bundle
// ---------------------------------------------------------------------------

step('verifying the bundled server answers --selftest');
const serverDll = path.join(serverDir, 'SystemVerilogLanguageServer.dll');
if (!fs.existsSync(serverDll)) {
    fail('the bundled server is missing: ' + serverDll);
}
const selftest = spawnSync('dotnet', [serverDll, '--selftest'], {
    cwd: extDir, stdio: 'pipe', encoding: 'utf8',
});
if (selftest.status !== 0) {
    fail(`the bundled server failed --selftest (exit ${selftest.status})`,
        (selftest.stderr || '').split('\n').slice(0, 5).join('\n        '));
}
if (!/semanticTokensProvider/.test(selftest.stdout || '')) {
    fail('the bundled server did not report its capabilities',
        'The publish output looks incomplete; re-run without --skip-bundle.');
}

function dirStats(dir) {
    let files = 0;
    let bytes = 0;
    const walk = (d) => {
        for (const entry of fs.readdirSync(d, { withFileTypes: true })) {
            const full = path.join(d, entry.name);
            if (entry.isDirectory()) { walk(full); continue; }
            files++;
            bytes += fs.statSync(full).size;
        }
    };
    walk(dir);
    return { files, mb: bytes / (1024 * 1024) };
}

const stats = dirStats(serverDir);
console.log(`  ${stats.files} file(s), ${stats.mb.toFixed(1)} MB`);
if (stats.mb > 200) {
    // Not fatal: the .vsix is still valid. But a folder this large means the
    // prune step was skipped, and it also slows every extension debug launch.
    console.warn(`  [WARN] server/ is ${stats.mb.toFixed(0)} MB; expected ~100 MB.`);
}

// ---------------------------------------------------------------------------
// 4. package
// ---------------------------------------------------------------------------

step('packaging the .vsix');
for (const stale of fs.readdirSync(extDir).filter((f) => f.endsWith('.vsix'))) {
    // vsce would otherwise leave the previous build next to the new one and the
    // "which file do I install?" question becomes guesswork.
    fs.rmSync(path.join(extDir, stale), { force: true });
}

run(`${vsceCommand} package --no-dependencies`, {
    cwd: extDir,
    hint: 'Add --allow-missing-repository / fix the reported manifest problem.',
});

const produced = fs.readdirSync(extDir).filter((f) => f.endsWith('.vsix'));
if (produced.length !== 1) {
    fail(`expected exactly one .vsix, found ${produced.length}: ${produced.join(', ')}`);
}

const vsixPath = path.join(extDir, produced[0]);
const vsixMb = fs.statSync(vsixPath).size / (1024 * 1024);

let finalPath = vsixPath;
if (outDir) {
    fs.mkdirSync(outDir, { recursive: true });
    finalPath = path.join(outDir, produced[0]);
    fs.copyFileSync(vsixPath, finalPath);
}

console.log('\n== done');
console.log(`  extension ${manifest.name} ${manifest.version}`);
console.log(`  vsix      ${finalPath}`);
console.log(`  size      ${vsixMb.toFixed(1)} MB`);
console.log('\nInstall with:');
console.log(`  code --install-extension "${finalPath}"`);
