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

- The tree is lazy: expanding a node issues one `rtl/hierarchy` request with a
  `nodePath` (the sequence of child indexes from the workspace root), so large
  projects are not serialised in one go.
- Clicking an instance reveals its instantiation site in the editor.
- **Copy Instance Path** (context menu / inline button) copies e.g.
  `u_mid.u_leaf`.
- The tree refreshes automatically when a SystemVerilog/Verilog file is saved,
  or manually from the view title (refresh icon).
- The instance hierarchy comes from the parser, so unresolved instances appear
  as leaf nodes and recursive instantiation is cut off at the repeated module.
