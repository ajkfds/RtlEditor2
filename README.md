# RtlEditor2

A lightweight, modular Integrated Development Environment (IDE) for RTL design, built with **Avalonia UI**.

![screenShot](images/screenShot.png)

> [!IMPORTANT]
> **Beta Version Notice**: This project is currently in the early prototype stage. While functional, features are subject to change, and SystemVerilog support is ongoing.

## Key Features

* **Hierarchy Awareness**: Built-in Verilog/SystemVerilog parser that understands `parameter` and `generate` blocks.
* **Tree Navigation**: Visualize and navigate through complex module instance hierarchies.
* **Intelligent Completion**: Context-aware code completion based on the parsed RTL structure.
* **Cross-Platform**: Powered by Avalonia UI, supporting both **Linux** and **Windows**.
* **Modular Architecture**: Highly customizable through a plugin-based system. You can reconfigure and recompile the editor to tailor the environment to your specific workflow.

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

This editor is designed with modularity at its core. By swapping out or modifying plugins and recompiling the source, you can optimize the IDE for your specific HDL environment.

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
RtlEditor2/
├── RtlEditor2.Desktop/          # Main desktop application
├── CodeEditor2/                  # Core editor component
├── CodeEditor2Plugin/            # Base plugin interface
├── CodeEditor2VerilogPlugin/     # Verilog/SystemVerilog language support
├── CodeEditor2AiPlugin/          # AI integration plugin
├── CodeEditor2DrawIoPlugin/      # Draw.io diagram support
├── CodeEditor2IcarusVerilogPlugin/ # Icarus Verilog simulation integration
├── CodeEditor2MarkdownPlugin/    # Markdown support
├── CodeEditor2VivadoPlugin/      # Xilinx Vivado integration
├── AjkAvaloniaLibs/              # Utility library
├── AvaloniaEdit/                 # Text editor control
└── TemplateEngineHost/           # Template engine
```

## Build

```bash
# Build entire project (show errors only)
dotnet build "RtlEditor2.Desktop.csproj" -clp:ErrorsOnly

# Build specific plugin
dotnet build CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin.csproj -clp:ErrorsOnly
```

---

## Roadmap / Known Limitations

### In Progress
- Enhanced linting integration
- Performance optimization for massive design hierarchies
- Full SystemVerilog syntax coverage

### Partially Implemented
- Parameterized classes
- Named sequential blocks (end:label syntax)
- Associative array methods (.size(), .delete())
- Dynamic array methods (.size(), .delete())
- Module instance wildcard port connection (.*)
- const function evaluation

## License

This project is licensed under the **MIT License**. See the [LICENSE](https://google.com/search?q=LICENSE) file for details.
