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

## Configuration

| Setting | Description |
|---|---|
| `systemverilogLsp.serverPath` | Path to a specific `SystemVerilogLanguageServer` executable. Leave empty to use the bundled server under the extension's `server/` folder. |

## Development (F5)

Open the `vscode-extension` folder and press `F5` (Extension Development Host).
For development the extension also looks for a server published at
`SystemVerilogLanguageServer/publish/` (inside or next to the extension folder),
so you can run:

```bash
dotnet publish SystemVerilogLanguageServer/SystemVerilogLanguageServer.csproj -c Release -o SystemVerilogLanguageServer/publish
```

## License

MIT
