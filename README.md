# RtlEditor2

A lightweight, **modular IDE library** for RTL design, built with **Avalonia UI**.

![screenShot](images/screenShot.png)

> [!IMPORTANT]
> **Beta Version Notice**: This project is currently in the early prototype stage. While functional, features are subject to change, and SystemVerilog support is ongoing.

## What is RtlEditor2?

RtlEditor2 is not just a finished IDE application—it is a **highly customizable IDE library** that you can tailor to your specific needs. By adjusting and recompiling plugins, you can create a customized editor with the exact features you require for your RTL design workflow.

**Key Characteristics**:
- 📦 **IDE Library**: Build your own custom RTL IDE by selecting and configuring plugins
- 🔌 **Plugin-Based Architecture**: Add/remove features by enabling or disabling plugins
- 🌍 **Cross-Platform**: Runs on both **Linux** and **Windows** without code changes
- ⚡ **Tailor-Made**: Implement only the features you need for your specific implementation environment

## Key Features

* **🏗️ Hierarchy Awareness**: Built-in Verilog/SystemVerilog parser that understands `parameter` and `generate` blocks.
* **🌳 Tree Navigation**: Visualize and navigate through complex module instance hierarchies.
* **🤖 Intelligent Completion**: Context-aware code completion based on the parsed RTL structure.
* **🌍 Cross-Platform**: Powered by Avalonia UI, supporting both **Linux** and **Windows** with a single codebase.
* **🔌 Modular Plugin Architecture**: Highly customizable through a plugin-based system. Select and configure plugins to build your own custom RTL IDE for your specific implementation environment.

## Language Support

* **Verilog (IEEE 1364)**: Core features fully supported.
* **SystemVerilog (IEEE 1800)**: Partial support (Active development in progress).

**Supported Verilog Features**:
- trireg declarations with drive/charge strength
- tranif0, tranif1, rtranif0, rtranif1, tran, rtran gate instantiations
- let declarations and function calls
- Streaming concatenation
- case inside with range expressions
- Clocking blocks and default clocking
- Struct/union packed types

## Customization (Plugin System)

This editor is designed with modularity at its core. By **adjusting plugins and recompiling**, you can create a customized IDE that perfectly matches your implementation environment requirements.

### How to Customize

1. **Select Plugins**: Enable or disable plugins based on your needs
2. **Configure Features**: Adjust plugin settings for your specific workflow
3. **Recompile**: Build the IDE with your custom plugin configuration
4. **Deploy**: Use your tailored RTL IDE on Linux or Windows

### Available Plugins

| Plugin | Description |
|--------|-------------|
| CodeEditor2VerilogPlugin | Verilog/SystemVerilog language support |
| CodeEditor2AiPlugin | AI integration (LLM support) |
| CodeEditor2DrawIoPlugin | Draw.io diagram support |
| CodeEditor2IcarusVerilogPlugin | Icarus Verilog simulation integration |
| CodeEditor2MarkdownPlugin | Markdown support |
| CodeEditor2VivadoPlugin | Xilinx Vivado integration |

---

## Project Structure

```
RtlEditor2/                           # Main repository
├── RtlEditor2.Desktop/               # Main desktop application (entry point)
├── CodeEditor2/                      # Core editor component
├── CodeEditor2Plugin/                # Base plugin interface
├── CodeEditor2VerilogPlugin/         # Verilog/SystemVerilog language support
├── CodeEditor2AiPlugin/              # AI integration plugin (optional)
├── CodeEditor2DrawIoPlugin/          # Draw.io diagram support (optional)
├── CodeEditor2IcarusVerilogPlugin/   # Icarus Verilog simulation (optional)
├── CodeEditor2MarkdownPlugin/        # Markdown support (optional)
├── CodeEditor2VivadoPlugin/          # Xilinx Vivado integration (optional)
├── AjkAvaloniaLibs/                  # Avalonia UI utility library
├── AvaloniaEdit/                     # Text editor control
├── SystemVerilogLanguageServer/      # LSP server (bundled into the VS Code extension)
├── SystemVerilogLanguageServer.Tests/# LSP server parser / hierarchy tests
└── vscode-extension/                 # VS Code extension (see "VS Code extension" below)

# Build & Run
- Both Linux and Windows: dotnet build && dotnet run
- No platform-specific code required
```

## Build
```bash
# Build entire project
dotnet build "RtlEditor2.Desktop.csproj"
```

---

## VS Code extension (`.vsix`)

The repository also ships a **VS Code extension** under `vscode-extension/` that
exposes the SystemVerilog language server (definition, references, hover,
completion, diagnostics, RTL Hierarchy view). It bundles the published server, so
the generated `.vsix` is self-contained: end users need neither .NET nor this
repository.

### Generate the `.vsix`

Prerequisites: **.NET 10 SDK**, **Node.js 18+**, and `@vscode/vsce`
(`npm install -g @vscode/vsce`). `vsce` is optional — the script falls back to
`npx --yes @vscode/vsce` when it is not installed globally.

```bash
cd vscode-extension
npm run vsix
```

`npm run vsix` (`scripts/make-vsix.js`) runs four steps and stops at the first
problem with a readable message:

| # | Step | What it does |
|---|------|--------------|
| 1 | **toolchain check** | node / dotnet / `vsce`, and that `package.json` has a `repository.url` |
| 2 | **bundle the server** | `scripts/bundle-server.js` empties `server/`, runs `dotnet publish` into it, then prunes symbols, `.playwright/` and the non-host native runtimes |
| 3 | **verify the bundle** | runs `--selftest` against the exact DLL that will be shipped, so a missing / stale server fails here instead of silently shipping an extension that does nothing |
| 4 | **package** | removes stale `.vsix` files, runs `vsce package --no-dependencies`, and prints the resulting path, size and install command |

Produced file: `vscode-extension/systemverilog-lsp-<version>.vsix`

```bash
# install it
code --install-extension vscode-extension/systemverilog-lsp-0.1.0.vsix
```

Useful flags:

```bash
node scripts/make-vsix.js --skip-bundle   # repackage the existing server/ (step 4 only)
node scripts/make-vsix.js --out dist      # also copy the .vsix into dist/
```

`npm run vsix:fast` (== `npm run package:nobundle`) is the `--skip-bundle` form,
for CI that already published the server.

> [!NOTE]
> `vscode-extension/server/` should stay around **100 MB**. It once grew to
> 734 MB because `dotnet publish -o` never removes old files (stale `.pdb`
> files, every platform's native runtimes, a bundled Playwright `node.exe`).
> A folder that large makes the extension development host die with exit code
> 134 right after `Loading development extension`. The build script prunes it;
> see `vscode-extension/README.md` for details.

### Release

`.github/workflows/vscode-extension-release.yml` builds the `.vsix` on
`windows-latest`, uploads it as an artifact, and attaches it to a GitHub Release
when you push a `v*` tag.

---

## Roadmap / Known Limitations

### In Progress
- Enhanced linting integration
- Performance optimization for massive design hierarchies
- Full SystemVerilog syntax coverage

## License

This project is licensed under the **MIT License**. See the [LICENSE](https://google.com/search?q=LICENSE) file for details.
