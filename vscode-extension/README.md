# SystemVerilog LSP (RtlEditor2)

A VS Code extension that brings RtlEditor2's SystemVerilog/Verilog language
server and RTL instance hierarchy to VS Code.

The language server is bundled inside the extension (no separate .NET install
or build step needed once it is packaged).

## Features

- Go to definition / Find references / Hover (markdown) / Outline
  (`documentSymbol`) / Completion
- Semantic-token syntax highlighting driven by the parser
- Diagnostics (parse errors / warnings / notices) as editor squiggles and in the
  Problems panel
- **RTL Hierarchy** tree view (see below) with the same content as RtlEditor2's
  NavigatePanel
- **Messages** pane (see below) on the right-hand side, the equivalent of
  RtlEditor2's MessageView
- **Snippets** from the editor (see below): alwaysFF block, module instance,
  port connection fill, port inversion, modport generation

## Snippets

The snippet logic lives in the language server (the same code the RtlEditor2
editor uses), so the extension only inserts the text it returns. Available from
the command palette (`SystemVerilog Snippet: ...`) and the editor right-click
menu:

| Command | What it does |
|---|---|
| `Insert alwaysFF Block` | `always @(posedge clk or negedge rst)` block; the clock / reset are taken from the ports annotated `// @sync: clock` / `// @sync: reset` |
| `Insert Module Instance` | Full instantiation of a module (parameter overrides + one named port connection per port). Shows a module picker first |
| `Fill Port Connections` | Rewrites the instantiation under the caret, filling every port connection |
| `Invert Selected Ports` | Flips `input`/`output` and the `_I`/`_O` name suffix of the selected port declarations |
| `Generate Modport` | Modport listing every logic signal of the enclosing interface |

The inserted text uses VS Code snippet tab stops (`${1:name}`), so `Tab` jumps
between the fields. The interactive key handling of the editor's snippets is not
reproduced.

## RTL Hierarchy view

VS Code's built-in File Explorer cannot be extended with extra child nodes, so
this extension contributes its own **RTL Hierarchy** tree view inside the
Explorer container.

- The top level mirrors the project layout: every file and folder of the
  workspace is listed (not only `.sv`/`.svh`/`.v`/`.vh`). A SystemVerilog /
  Verilog file can be expanded into its module/interface instance hierarchy;
  other files are leaves that open in the editor.
- If a file is opened without a folder, the server infers the project root from
  the opened file (nearest `.git` / `*.sln` / `*.code-workspace` / ...).
- Build output / VCS folders (`obj`, `bin`, `publish`, `node_modules`, `.git`,
  `.svn`, `.hg`) are skipped.
- The tree is lazy: expanding a node issues one `rtl/hierarchy` request, and a
  source file is parsed only when expanded, so the top level answers immediately
  even for a large project.
- Clicking a node re-parses its file and refreshes the tree (so edits and their
  diagnostics show up). The view refresh icon forces a full workspace re-scan;
  files created / deleted / renamed on disk are picked up automatically.
- Right-click menu (mirrors the editor's NavigatePanel): Add Folder, Add Blank
  File, Delete, Open in Explorer, Copy Path, Property, Reveal in Editor, Copy
  Instance Path (e.g. `u_mid.u_leaf`).
- Icons come from the editor's own SVG assets (shipped in `icons/`, light and
  dark variants): SystemVerilog / Verilog / header documents, module and
  interface instances, imported packages. A file with parse errors or warnings
  is tinted with the theme's error / warning colour, like the editor's
  NavigatePanel badge.

## Messages pane

The **SystemVerilog Messages** view lives in the Auxiliary Bar (the secondary
side bar on the right-hand side of the window), so the parse messages stay
visible next to the editor - the same role as RtlEditor2's MessageView.

- Each entry shows a severity icon (error / warning / info / hint), the line
  number and the message text, sorted by file and position with errors first.
- Clicking an entry opens the file and selects the reported range.
- The scope selector switches between **Current file** (the focused document,
  like the editor) and **Whole workspace** (every file the server has parsed).
- The view title shows the error / warning counts.
- Open it with `SystemVerilog: Show Messages` from the command palette, or the
  warning icon in the Auxiliary Bar.

The pane renders the diagnostics the server already publishes through the
standard `textDocument/publishDiagnostics` notification, so it adds no custom
protocol and does not change the LSP standard. It complements (rather than
replaces) VS Code's built-in Problems panel: the Problems panel groups by file
in a tree, this pane is a flat, severity-coloured list like the editor's.

## Install

### From a `.vsix`

1. Build the VSIX (see below) or download it from GitHub Releases.
2. `Extensions: Install from VSIX...` in VS Code.
3. Open a folder containing `.sv`/`.svh`/`.v`/`.vh` files.

The extension ships the server under its `server/` folder, so nothing else is
required on end-user machines.

## Build / package the extension

Prerequisites for building: .NET SDK, Node.js, and `npm i -g @vscode/vsce`.

```bash
cd vscode-extension
npm run package
```

`npm run package` first bundles the server (`scripts/bundle-server.js` runs
`dotnet publish` into `vscode-extension/server/`) and then runs `vsce package`,
producing `systemverilog-lsp-<version>.vsix`.

To package without rebuilding the server (e.g. CI that already published it):

```bash
npm run package:nobundle
```

### Release to GitHub

A GitHub Actions workflow is provided at
`.github/workflows/vscode-extension-release.yml`. It builds the server, packages
the `.vsix` and attaches it to a GitHub Release when you push a `v*` tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

You can also run the workflow manually (Actions → "Release VS Code extension" →
Run workflow); the `.vsix` is uploaded as a downloadable artifact.

### Icon (optional)

To show an icon on the Marketplace / Extensions list, add a 128x128 `icon.png`
under `vscode-extension/` and reference it from `package.json`:

```json
"icon": "icon.png"
```

## Configuration

| Setting | Description |
|---|---|
| `systemverilogLsp.serverPath` | Path to a specific `SystemVerilogLanguageServer` executable. Leave empty to use the bundled server under the extension's `server/` folder. |

## Development (F5)

### Debug both sides at once (recommended)

Open `RtlEditor2.code-workspace` in VS Code and press `F5`, choosing
**Extension + Language Server**. This is a compound launch that starts:

1. **Launch: SystemVerilog Language Server (C#)** — the server under the C#
   debugger (breakpoints in the server work). It listens on
   `127.0.0.1:9250` (`--port 9250`).
2. **Run Extension** — the Extension Development Host (TS/JS side).

The two talk over TCP instead of stdio: the launch configuration sets
`SVLSP_SERVER_PORT=9250` in the extension host's environment, and
`extension.js` then **connects** to the already-running server instead of
spawning it. Without that variable the extension spawns the server as usual
(stdio), which is what the packaged `.vsix` does.

The same configurations (and the compound) are also defined in
`vscode-extension/.vscode/launch.json`, so opening the `vscode-extension`
folder directly and pressing `F5` works too. There, **Run Extension** spawns
the server as usual and **Run Extension (connect to C# debug server)** is the
TCP variant used by the compound.

Notes:

- The server is built first by the `build-server` task
  (`dotnet build SystemVerilogLanguageServer/SystemVerilogLanguageServer.csproj`).
- The extension retries the TCP connection for 30 seconds, so the launch order
  between the two configurations does not matter.
- `Developer: Reload Window` in the Extension Development Host reconnects to
  the same server (the server accepts connections in a loop).
- In a `.code-workspace` file the plain `${workspaceFolder}` variable cannot be
  resolved (a workspace may contain several folders), so the entries there use
  `${workspaceFolder:RtlEditor2}` and the folder is given an explicit `name`.
  If you rename the folder, update those variables too.
- To debug only the extension, run **Run Extension** (the non-TCP one); it
  spawns the server itself and needs no other configuration.

### Spawning a published server

For development the extension also looks for a server published at
`SystemVerilogLanguageServer/publish/` (inside or next to the extension folder),
so you can run:

```bash
dotnet publish SystemVerilogLanguageServer/SystemVerilogLanguageServer.csproj -c Release -o SystemVerilogLanguageServer/publish
```

## License

MIT
