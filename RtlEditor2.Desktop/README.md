# RtlEditor2 Parse Instability Investigation Summary

## Project Structure

```
RtlEditor2/
├── RtlEditor2.Desktop/          # Main desktop application
├── CodeEditor2/                 # Core editor component
│   └── CodeEditor2/            # CodeDocument, Parser, Views, etc.
├── CodeEditor2Plugin/           # Base plugin interface
├── CodeEditor2VerilogPlugin/   # Verilog/SystemVerilog language support
├── CodeEditor2AiPlugin/        # AI integration
├── CodeEditor2DrawIoPlugin/    # Draw.io diagram support
├── CodeEditor2IcarusVerilogPlugin/ # Icarus Verilog simulation
├── CodeEditor2MarkdownPlugin/  # Markdown support
├── CodeEditor2VivadoPlugin/    # Xilinx Vivado integration
├── AjkAvaloniaLibs/            # Utility library
├── AvaloniaEdit/               # Text editor control
└── TemplateEngineHost/         # Template engine
```

## Problem Summary

**Symptom:** Parse results are unstable when parsing deep hierarchies. Clicking through tree nodes causes parse results to change unpredictably, with text coloring and error states fluctuating.

## Root Cause Analysis

### Primary Issues Identified

#### 1. Parallel Parse Cancellation Without Coordination
**File:** `CodeEditor2VerilogPlugin/Tool/ParseHierarchy.cs`

When user clicks nodes rapidly:
- Previous parse task is cancelled
- New parse task starts immediately
- Cancelled task may still complete and update UI
- Both tasks may interleave AcceptParsedDocumentAsync calls

```csharp
public static async Task ParseAsync(CodeEditor2.Data.TextFile textFile, ParseMode parseMode)
{
    if (_cts != null)
    {
        textFile.ReparseRequested = true;
        _cts.Cancel(); // Immediate cancellation
    }
    // New task starts before old one fully completes
}
```

#### 2. Shared Root BuildingBlocks Dictionary
**File:** `CodeEditor2VerilogPlugin/Verilog/BuildingBlocks/BuildingBlock.cs`

Multiple concurrent parses modify the same shared dictionary:

```csharp
public Dictionary<string, BuildingBlock> BuildingBlocks { get; set; } = 
    new Dictionary<string, BuildingBlock>();
```

When multiple module instances reference the same source file, all try to modify this dictionary simultaneously.

#### 3. Non-Atomic Item Updates
**File:** `CodeEditor2VerilogPlugin/Data/VerilogCommon/Updater.cs`

The Items dictionary is cleared before adding new items:

```csharp
lock (item.Items)
{
    item.Items.Clear(); // Creates empty state temporarily
    foreach (CodeEditor2.Data.Item i in newSubItems.Values)
    {
        item.Items.AddOrUpdate(i.Name, i);
    }
}
```

#### 4. Key Generation Race in VerilogModuleInstance
**File:** `CodeEditor2VerilogPlugin/Data/VerilogModuleInstance.cs`

```csharp
private string _getKey()
{
    textFileLock.EnterReadLock();
    try
    {
        string moduleName = _moduleName;           // Read 1
        var parameterOverrides = _parameterOverrides; // Read 2
        return Verilog.ParsedDocument.KeyGenerator(this, moduleName, parameterOverrides);
    }
    finally
    {
        textFileLock.ExitReadLock();
    }
}
```

The two reads are not atomic together - another thread could modify between them.

#### 5. Parse Mode Dependent Behavior
**File:** `CodeEditor2VerilogPlugin/Verilog/BuildingBlocks/Root.cs`

```csharp
if (parsedDocument.ParseMode == Parser.VerilogParser.ParseModeEnum.LoadParse)
{
    module = await Module.ParseCreate(word, null, parsedDocument.Root, file, true);
    parsedDocument.ReparseRequested = true; // Always requests reparse
}
else
{
    module = await Module.ParseCreate(word, null, parsedDocument.Root, file, false);
    parsedDocument.ReparseRequested = false;
}
```

LoadParse always triggers reparse, creating cascading effects.

### Secondary Issues

#### 6. CodeDocument Color Copy Race
Colors are copied without version checking:

```csharp
// In TextFile.PostUIUpdate
var textFileDoc = CodeDocument;
var editorDoc = Global.codeView.CodeDocument;
if (textFileDoc != null && editorDoc != null && textFileDoc.Version >= editorDoc.Version)
{
    editorDoc = textFileDoc.Clone();
}
```

Race: Version check passes, then source changes before copy completes.

#### 7. Include File Update Timing
Include files are updated separately from main parse:

```csharp
await updateIncludeFilesAsync(vParsedDocument, Items);
```

If Items is in flux during this, include references may be lost.

#### 8. ParseWorker Sub-file Check Race
```csharp
foreach (Data.Item item in items)
{
    if (!subFile.ReparseRequested) continue; // May change between checks
    await runSingleParse(subFile, token);
}
```

## Recommended Fixes

### Fix 1: Parse Request Queue
```csharp
private static ConcurrentQueue<ParseRequest> _parseQueue = new();
public static async Task ProcessQueueAsync()
{
    while (_parseQueue.TryDequeue(out var request))
    {
        await ParseAsync(request.TextFile, request.Mode);
    }
}
```

### Fix 2: Atomic BuildingBlock Updates
```csharp
private readonly object buildingBlocksLock = new();
public void AddBuildingBlock(string name, BuildingBlock block)
{
    lock (buildingBlocksLock)
    {
        BuildingBlocks[name] = block; // Atomic upsert
    }
}
```

### Fix 3: Atomic Items Update
```csharp
var snapshot = new Dictionary<string, Item>(item.Items);
snapshot.Clear();
foreach (var newItem in newSubItems) snapshot[newItem.Name] = newItem;
Interlocked.Exchange(ref item._items, snapshot);
```

### Fix 4: Composite Key Lock
```csharp
private string _getKey()
{
    textFileLock.EnterReadLock();
    try
    {
        return GenerateKey(_moduleName, _parameterOverrides);
    }
    finally
    {
        textFileLock.ExitReadLock();
    }
}
```

### Fix 5: Parse Mode Sequencing Gate
```csharp
private SemaphoreSlim _parseModeGate = new(1, 1);
public async Task ParseAsync(ParseModeEnum mode)
{
    await _parseModeGate.WaitAsync();
    try { /* ... */ }
    finally { _parseModeGate.Release(); }
}
```

### Fix 6: Version-Stamped Color Copy
```csharp
public void CopyColorMarkFrom(CodeDocument document)
{
    lock (document.textDocument) // Hold lock during copy
    {
        TextColors.LineInformation = document.TextColors.LineInformation.Clone();
        // ... rest of copy
    }
}
```

## Investigation Completed Files

| File | Status |
|------|--------|
| `Tool/ParseHierarchy.cs` | ✅ Analyzed |
| `Verilog/BuildingBlocks/BuildingBlock.cs` | ✅ Analyzed |
| `Verilog/BuildingBlocks/Root.cs` | ✅ Analyzed |
| `Verilog/BuildingBlocks/Module.cs` | ✅ Analyzed |
| `Data/VerilogFile.cs` | ✅ Analyzed |
| `Data/VerilogModuleInstance.cs` | ✅ Analyzed |
| `Data/VerilogCommon/Updater.cs` | ✅ Analyzed |
| `Data/InstanceTextFile.cs` | ✅ Analyzed |
| `Data/VerilogHeaderInstance.cs` | ✅ Analyzed |
| `Data/ProjectProperty.cs` | ✅ Analyzed |
| `CodeEditor/Parser/ParseWorker.cs` | ✅ Analyzed |
| `CodeEditor/Parser/CodeViewParser.cs` | ✅ Analyzed |
| `Data/TextFile.cs` | ✅ Analyzed |
| `CodeEditor/CodeDocument.cs` | ✅ Analyzed |
| `CodeEditor/CodeDocument/MarkHandler.cs` | ✅ Analyzed |
| `CodeEditor/CodeDocument/ColorHandler.cs` | ✅ Analyzed |
| `Controller_CodeEditor.cs` | ✅ Analyzed |
| `Views/CodeView.axaml.cs` | ✅ Analyzed |
| `Verilog/WordScanner.cs` | ✅ Analyzed |
| `Verilog/ParsedDocument.cs` | ✅ Analyzed |
| `Verilog/NameSpace.cs` | ✅ Analyzed |
| `Verilog/NamedElements.cs` | ✅ Analyzed |
| `Verilog/ModuleItems/ModuleInstantiation.cs` | ✅ Analyzed |

## Next Steps

1. [ ] Implement Parse Request Queue in ParseHierarchy
2. [ ] Add atomic BuildingBlock registration
3. [ ] Fix Updater.UpdateAsync atomicity
4. [ ] Add composite key lock to VerilogModuleInstance
5. [ ] Implement Parse Mode Sequencing Gate
6. [ ] Add version-stamped color copy
7. [ ] Create test cases to verify fixes
8. [ ] Performance testing under rapid node clicks
