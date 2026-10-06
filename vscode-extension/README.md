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
