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

## Recent Fixes

- Fixed CS0246 and CS1503 errors in TextFile.cs by adding `using CodeEditor2.CodeEditor.TextDecollation;` directive to resolve `LineInformation` type reference.
- Added restart resilience to FileBasedPipe receiver: `InitializeAsReceiver()` now reads existing `ack.dat` file to restore `_lastProcessedId` instead of always starting from 0, preventing duplicate message reception when receiver restarts while `data.dat` still contains unacknowledged messages.

---

## Parse Instability Investigation

### Problem
Tree navigation produces inconsistent parse results. Clicking through tree nodes causes parse results to change unpredictably, with text coloring and error states fluctuating.

### Root Causes Identified

| # | Issue | Location | Severity |
|---|-------|----------|----------|
| 1 | Parallel Parse Cancellation Without Coordination | `Tool/ParseHierarchy.cs` | **High** |
| 2 | Shared Root BuildingBlocks Dictionary | `BuildingBlock.cs` | **High** |
| 3 | Non-Atomic Items Update in Updater | `VerilogCommon/Updater.cs` | **Medium** |
| 4 | Key Generation Race in VerilogModuleInstance | `VerilogModuleInstance.cs` | **Medium** |
| 5 | Parse Mode Dependent Behavior | `Root.cs`, `Module.cs` | **Medium** |
| 6 | CodeDocument Color Copy Race | `CodeDocument.cs`, `ColorHandler.cs` | **Low** |
| 7 | Include File Update Timing | `VerilogFile.cs` | **Low** |
| 8 | ParseWorker Sub-file Check Race | `ParseWorker.cs` | **Low** |

### Investigation Reports

| Project | Report |
|---------|--------|
| `CodeEditor2VerilogPlugin/` | `README.md` in project folder |
| `CodeEditor2/` | `README.md` in project folder |
| `CodeEditor2Plugin/` | `README.md` in project folder |
| `RtlEditor2.Desktop/` | `README.md` in project folder |

### Recommended Fixes

1. **Parse Request Queue**: Replace immediate cancellation with request queuing
2. **Atomic BuildingBlock Updates**: Add locks to `Root.BuildingBlocks`
3. **Atomic Items Update**: Fix `Updater.UpdateAsync` clear-then-add pattern
4. **Composite Key Lock**: Protect key generation in `VerilogModuleInstance`
5. **Parse Mode Sequencing Gate**: Ensure proper parse mode ordering
6. **Version-Stamped Color Copy**: Add version checking during color copy

### Next Actions
- [ ] Implement Parse Request Queue in ParseHierarchy
- [ ] Add atomic BuildingBlock registration
- [ ] Fix Updater.UpdateAsync atomicity
- [ ] Add composite key lock to VerilogModuleInstance
- [ ] Implement Parse Mode Sequencing Gate
- [ ] Add version-stamped color copy
- [ ] Create test cases to verify fixes
- [ ] Performance testing under rapid node clicks

## License

MIT License
