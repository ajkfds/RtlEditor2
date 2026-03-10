# RtlEditor2

A lightweight, modular Integrated Development Environment (IDE) for RTL design, built with **Avalonia UI**.

> **Beta Version Notice**: This project is currently in the early prototype stage.

## Key Features

- **Hierarchy Awareness**: Built-in Verilog/SystemVerilog parser that understands `parameter` and `generate` blocks.
- **Tree Navigation**: Visualize and navigate through complex module instance hierarchies.
- **Intelligent Completion**: Context-aware code completion based on the parsed RTL structure.
- **Cross-Platform**: Powered by Avalonia UI, supporting both **Linux** and **Windows**.
- **Modular Architecture**: Highly customizable through a plugin-based system.

## Language Support

- **Verilog (IEEE 1364)**: Core features fully supported.
- **SystemVerilog (IEEE 1800)**: Partial support (Active development in progress).

## Project Structure

```
RtlEditor2/
├── RtlEditor2.Desktop/          # Main desktop application
├── CodeEditor2/                  # Core editor component
│   └── CodeEditor2/             # CodeDocument, Parser, Views, etc.
├── CodeEditor2Plugin/            # Base plugin interface (IPlugin)
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

### Core Components

- **CodeEditor2/CodeEditor2/CodeEditor2/**:
  - `CodeEditor/`: Core editor components (CodeDocument, Parser, etc.)
  - `Data/`: Project, File, Folder management
  - `Parser/`: Parsing infrastructure
  - `Views/`, `ViewModels/`: UI layer
  - `Controller.cs`: Main controller

- **CodeEditor2Plugin/**: Plugin base system
  - `IPlugin`: Plugin interface
  - `PulginManager`: Plugin management

- **CodeEditor2VerilogPlugin/**: Verilog language support
  - `Parser/`: Verilog parser
  - `FileTypes/`: File type detection
  - `NavigatePanel/`: Tree navigation

## License

MIT License
