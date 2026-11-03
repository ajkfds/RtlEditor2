# SystemVerilog LSP (RtlEditor2) VS Code extension

Wraps the `SystemVerilogLanguageServer` (JSON-RPC over stdio) as a VS Code
extension.

## Build the server first

```
dotnet publish SystemVerilogLanguageServer/SystemVerilogLanguageServer.csproj -c Release -o SystemVerilogLanguageServer/publish
```

## Use

- Open the `vscode-extension` folder and press `F5` (Extension Development Host), or
- `npx vsce package` to produce a `.vsix` and install it via
  "Extensions: Install from VSIX...".

## Configuration

| Setting | Description |
|---|---|
| `systemverilogLsp.serverPath` | Path to the published server executable. If empty, the extension looks for `SystemVerilogLanguageServer/publish/SystemVerilogLanguageServer(.exe)` and falls back to `dotnet run --project`. |

## Provided features (server-side)

- `textDocument/definition`
- `textDocument/references`
- `textDocument/hover` (markdown)
- `textDocument/documentSymbol`
- `textDocument/completion`
- `textDocument/semanticTokens/full`
- `rtl/hierarchy` (custom) — the RTL instance hierarchy used by the tree view

## RTL Hierarchy view

VS Code's built-in File Explorer is reserved for the file system and offers no
provider API for third-party child nodes, so this extension contributes its own
**RTL Hierarchy** tree view inside the Explorer container. It shows the same
information as RtlEditor2's NavigatePanel: workspace files, and under each file
the module/interface instance hierarchy.

- The top level is the **whole workspace**: every `.sv` / `.svh` / `.v` / `.vh`
  file under the workspace folders, grouped into a folder tree — not only the
  documents that happen to be open. Files are loaded from disk by the server at
  `initialize`, so unopened files still appear with their instance hierarchy.
- Every workspace folder is reported to the server, and adding / removing a
  folder in the window extends / trims the tree
  (`workspace/didChangeWorkspaceFolders`).
- If a file is opened **without** opening a folder (File > Open File), the
  server has no workspace root from `initialize`. It then infers one from the
  opened file: the nearest enclosing project directory (a `.git` / `.svn` /
  `.hg` entry, or a `*.sln` / `*.slnx` / `*.code-workspace` / `*.csproj` /
  `*.f` file) and scans it, so the whole project shows up instead of only the
  opened document.
- Build output / VCS directories (`obj`, `bin`, `publish`, `node_modules`,
  `.git`, `.svn`, `.hg`) are skipped.
- The tree is lazy: expanding a node issues one `rtl/hierarchy` request —
  `folderPath` for a folder, `uri` + `nodePath` (the sequence of child indexes
  from the workspace root) for a file / instance — so large projects are not
  serialised in one go.
- Clicking an instance reveals its instantiation site in the editor.
- **Copy Instance Path** (context menu / inline button) copies e.g.
  `u_mid.u_leaf`.
- The tree refreshes automatically when a SystemVerilog/Verilog file is saved
  (redraw only) or when source files are created / deleted / renamed on disk
  (workspace re-scan). The refresh icon in the view title forces a full
  re-scan of the workspace folders.
- The instance hierarchy comes from the parser, so unresolved instances appear
  as leaf nodes and recursive instantiation is cut off at the repeated module.
