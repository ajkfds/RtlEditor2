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

## Recent Fixes

- ✅ **Parse Request Queue**: Implemented request queuing instead of immediate cancellation.
- ✅ **Atomic BuildingBlock Registration**: Added locks to `Root.BuildingBlocks` dictionary.
- ✅ **Updater.UpdateAsync Atomicity**: Fixed clear-then-add pattern.
- ✅ **Composite Key Lock**: Protected key generation in `VerilogModuleInstance`.
- ✅ **Parse Mode Sequencing Gate**: Ensured proper parse mode ordering.
- ✅ **Version-Stamped Color Copy**: Added version checking during color copy.
- ✅ **Wait Order Statement**: `parseCreate_wait_order` implementation verified.
- ✅ **Sequence/Property Expressions**: Complete implementation in dedicated files.
- ✅ **Function Constructor Access (CS0122)**: Changed `Function` constructor from `protected` to `internal` to allow access from `MethodPrototype.ParseConstructorPrototype`.

---

## 未実装 SystemVerilog 仕様リスト

プロジェクト `CodeEditor2VerilogPlugin/` で未実装の SystemVerilog 仕様を記載します。

### 実装完了した仕様 (✅)

| # | 仕様 | ファイル |
|---|------|---------|
| 1 | Clocking Declaration | `Verilog/BuildingBlocks/Clocking.cs` |
| 2 | Constraint Declaration | `Verilog/Coverage/ConstraintDeclaration.cs` |
| 3 | Covergroup/Coverpoint/Cross | `Verilog/Coverage/CovergroupDeclaration.cs` |
| 4 | Sequence Expression | `Verilog/Sequence/SequenceExpr.cs` |
| 5 | Sequence Instance | `Verilog/Sequence/SequenceInstance.cs` |
| 6 | Sequence Declaration | `Verilog/Sequence/SequenceDeclaration.cs` |
| 7 | Sequence Match Item | `Verilog/Sequence/SequenceMatchItem.cs` |
| 8 | Dist Expression | `Verilog/Sequence/DistExpression.cs` |
| 9 | Property Expression | `Verilog/Property/PropertyExpr.cs` |
| 10 | Property Declaration | `Verilog/Property/PropertyDeclaration.cs` |
| 11 | Property Spec | `Verilog/Assertion/PropertySpec.cs` |
| 12 | Property Instance | `Verilog/Property/PropertyInstance.cs` |
| 13 | Property Operator | `Verilog/Property/PropertyOperator.cs` |
| 14 | Randsequence | `Verilog/Statements/RandsequenceStatement.cs` |
| 15 | Randcase | `Verilog/Statements/RandcaseStatement.cs` |
| 16 | DPI Export | `Verilog/DpiImportExport.cs` |
| 17 | Let Declaration | `Verilog/DataObjects/LetDeclaration.cs` |
| 18 | Bind Directive | `Verilog/ModuleItems/BindDirective.cs` |
| 19 | Net Alias | `Verilog/ModuleItems/NetAlias.cs` |
| 20 | Default Clocking | `Verilog/BuildingBlocks/Clocking.cs` |
| 21 | Deferred Immediate Assertion | `Verilog/Statements/ImmidiateAssertionStatement.cs` |
| 22 | Expect Property Statement | `Verilog/Statements/ExpectPropertyStatement.cs` |
| 23 | Assert Property Statement | `Verilog/Assertion/AssertPropertyStatement.cs` |
| 24 | Assume Property Statement | `Verilog/Assertion/AssumePropertyStatement.cs` |
| 25 | Cover Property Statement | `Verilog/Assertion/CoverPropertyStatement.cs` |
| 26 | Restrict Property Statement | `Verilog/Assertion/RestrictPropertyStatement.cs` |
| 27 | Cover Sequence Statement | `Verilog/Assertion/CoverSequenceStatement.cs` |
| 28 | Immediate Assertion | `Verilog/Statements/ImmidiateAssertionStatement.cs` |
| 29 | Anonymous Program | `Verilog/BuildingBlocks/Program.cs` |
| 30 | Timeunits Declaration | `Verilog/DataObjects/TimeunitsDeclaration.cs` |
| 31 | Wait Order | `Verilog/Statements/WaitStatement.cs` |
| 32 | Virtual Interface | `Verilog/DataObjects/DataTypes/VirtualInterfaceType.cs` |
| 33 | Program Instantiation | `Verilog/ModuleItems/ProgramInstantiation.cs` |
| 34 | Disable Fork | `Verilog/Statements/ParallelBlock.cs` |
| 35 | Join Variants | `Verilog/Statements/ParallelBlock.cs` |
| 36 | Fork-Join Block Label | `Verilog/Statements/ParallelBlock.cs` |
| 37 | Action Block (if-else) | `Verilog/Statements/ImmidiateAssertionStatement.cs` |

---

### 未実装仕様

#### 高優先度

| # | 仕様 | ファイル | 備考 |
|---|------|---------|------|
| 1 | **Checker Declaration** | `Verilog/BuildingBlocks/Checker.cs` | BNFコメントのみ、解析処理なし |
| 2 | **Type Reference** | `Verilog/DataObjects/DataTypes/` | Variable.Create参照コメントのみ |

#### 中優先度

| # | 仕様 | ファイル | 備考 |
|---|------|---------|------|
| 3 | **With Dist** | `Verilog/` | `randomize() with constraint` 形式 |
| 4 | **Interconnect Declaration** | `Verilog/DataObjects/Nets/Net.cs` | BNFに言及あり |
| 5 | **Default Nettype** | `Verilog/` | コンパイラ指示 `'default_nettype` |
| 6 | **Assert Failure Action** | `Verilog/` | `$error`, `$fatal` 等 |

#### 低優先度

| # | 仕様 | ファイル | 備考 |
|---|------|---------|------|
| 7 | **UDP Primitive Declaration** | `Verilog/` | `primitive`/`endprimitive`/`table`/`endtable` |
| 8 | **Primitive Table Entry** | `Verilog/` | combinational/sequential テーブルエントリ |
| 9 | **Charge Strength** | `Verilog/DataObjects/Nets/Net.cs` | `ChargeStrength.ParseCreate` 呼び出しあり |
| 10 | **Config Declaration** | `Verilog/BuildingBlocks/Root.cs` | `config` キーワード登録済み |
| 11 | **Cell & Library** | `Verilog/General.cs` | キーワードのみ登録 |
| 12 | **Tagged Union** | `Verilog/` | `tagged` キーワード登録済み |
| 13 | **Class Constructor Prototype** | `Verilog/BuildingBlocks/Class.cs` | `extern new` prototype |

---

### 実装状況サマリー

| カテゴリ | 実装済み | 未実装 | 備考 |
|----------|---------|--------|------|
| SVA (アサーション) | ✅ 全対応 | - | Sequence, Property, Immediate |
| Constraint & Coverage | ✅ 全対応 | - | Constraint, Covergroup |
| Fork/Join Control | ✅ 全対応 | - | Disable Fork, Join variants |
| Binding & Configuration | ✅ 部分対応 | 2件 | Config, Default Nettype |
| Data & Variables | ✅ 部分対応 | 1件 | Type Reference |
| Primitive/Table | ❌ | 2件 | UDP |
| Net Strength | ⚠️ | 1件 | Charge Strength |
| 其他 | 36件実装 | 5件 | Checker, Tagged Union等 |

---

## 調査記録: UIスレッドロック問題

### 問題概要
SystemVerilogコードを大量入力時に、パース中にUIスレッドがロックされ редактирование が長時間できなくなる。

### 主要因 (調査済み - 修正なし)

| # | 原因 | 場所 | 重要度 |
|---|------|------|--------|
| 1 | `Updater.UpdateAsync`がUIスレッド前提の設計 | `VerilogCommon/Updater.cs:14` | **最優先** |
| 2 | `UpdateAsync`での`InvokeAsync`使用 (同期的marshal) | `VerilogFile.cs:489`, `VerilogModuleInstance.cs:511` | 高 |
| 3 | `AcceptParsedDocumentAsync`呼び出しチェーン | `ParseHierarchy.cs`, `VerilogFile.cs` | 高 |
| 4 | 編集時パースにデバウンスなし | `CodeView.axaml.cs:269` | 中 |
| 5 | Includeファイルの再帰的処理でのmarshal | `VerilogFile.cs:208` | 中 |

### 詳細

#### 呼び出しフロー
```
編集操作 → CodeViewParser.EntryParse() [UI]
    ↓
ParseWorker.Parse() [Task.Run - Background]
    ↓
AcceptParsedDocumentAsync() [Background]
    ↓
verilogFile.UpdateAsync() [Background → UI marshal via InvokeAsync] ← ブロック!
    ↓
Updater.UpdateAsync() [UI Thread]
```

#### 根本原因
1. **`Dispatcher.UIThread.InvokeAsync`** の使用は**同期的marshal**であり、呼び出し元をブロックする
2. パース完了後の後処理が背景スレッドで実行される
3. デバウンス機構の不足

### 備考
- 調査のみの実施。修正は未実施。

---

---

## 調査記録: includeファイル編集時のUIスレッドロック問題

### 問題概要
includeファイルを編集したときに、UIスレッドがパース終わるまでロックされている。

### 呼び出しフロー

```
編集操作 → CodeViewParser.EntryParse() [Background Task]
    ↓
ParseWorker.Parse() → runSingleParse()
    ↓
VerilogHeaderInstance.AcceptParsedDocumentAsync()
    ├→ textFile.CodeDocument?.CopyColorMarkFrom() [問題なし]
    ├→ Controller.CodeEditor.PostRefresh() [Postのみ]
    └→ await UpdateAsync() ←─────────────────── (A)
              ↓
        VerilogHeaderInstance.UpdateAsync()
              ↓
        await base.UpdateAsync() [TextFile.UpdateAsync]
              │  ※UIスレッドチェックなし (Background Task実行中)
              │  ※内部的にもう一つのawaitパスなし
              ↓
        // 親ファイル階層を辿る
        if (parentItem is VerilogFile)
        {
            await VerilogCommon.Updater.UpdateAsync(vFile, ...) ←── (B)
                  ↓
            VerilogFile.UpdateAsync()
                  ├→ await base.UpdateAsync() [TextFile.UpdateAsync]
                  │     ※ここでUIスレッドチェック発生
                  │     → Dispatcher.UIThread.InvokeAsync() 同期marshal ← ブロック!
                  │
                  ├→ await VerilogCommon.Updater.UpdateAsync()
                  │
                  └→ VerilogFile.updateIncludeFilesAsync()
                        ├→ 各includeのCodeDocument更新
                        └→ await updateIncludeFilesAsync(nested Include)
                              └→ 各nested includeでもUpdateAsync() call

    // その後Background Taskが継続
    VerilogFile.AcceptParsedDocumentAsync()
        ├→ ParsedDocument設定
        ├→ textFile.CodeDocument?.CopyColorMarkFrom() [問題なし]
        ├→ Controller.CodeEditor.PostRefresh() [Postのみ]
        ├→ await updateIncludeFilesAsync()
        └→ await UpdateAsync() ←─────────────────── (C)
              ↓
          TextFile.UpdateAsync() [再度呼び出し]
              // UIスレッドで実行already (InvokeAsync完了済み)
              // → そのままbase.UpdateAsync()続行
              └→ NavigatePanelNode.UpdateVisual()
                  └→ Controller.CodeEditor.PostRefresh()
```

### 主要因

| # | 原因 | 場所 | 重要度 |
|---|------|------|--------|
| 1 | **VerilogHeaderInstance.UpdateAsyncでの親ファイルUpdate** | `VerilogHeaderInstance.cs:363-366` | **高** |
| 2 | **TextFile.UpdateAsyncでの同期marshal** | `TextFile.cs:517-520` | **高** |
| 3 | **UpdateAsyncの再帰呼び出しチェーン** | `VerilogFile.updateIncludeFilesAsync` | 中 |
| 4 | **Includeファイルのネスト処理** | 同上、再帰呼び出し | 中 |

### 詳細分析

#### 1. VerilogHeaderInstance.UpdateAsyncでの親ファイルUpdate (高)

```csharp
// VerilogHeaderInstance.cs
public override async Task UpdateAsync()
{
    await base.UpdateAsync();  // TextFile.UpdateAsync → 同期marshal発生の可能性

    // 親ファイル階層を辿ってUpdateAsyncを呼ぶ
    CodeEditor2.Data.Item? parentItem = Parent;
    while (true)
    {
        if (parentItem == null) break;
        if (parentItem is VerilogHeaderFile)
        {
            parentItem = parentItem.Parent;
        }
        else
        {
            break;
        }
    }

    if (parentItem != null && parentItem is VerilogFile)
    {
        VerilogFile vFile = (VerilogFile)parentItem;
        await VerilogCommon.Updater.UpdateAsync(vFile, itemUpdateSemaphore); // ← ここで親ファイルを更新
    }
}
```

**問題点**: includeファイル（`VerilogHeaderInstance`）の更新時に、毎回親ファイル（`VerilogFile`）の更新を行ってしまう。これにより、includeファイルを編集しただけで、パース処理が完了するまでUIスレッドがブロックされる可能性がある。

#### 2. TextFile.UpdateAsyncでの同期marshal (高)

```csharp
// TextFile.cs
public override async Task UpdateAsync()
{
    if (!Dispatcher.UIThread.CheckAccess())
    {
        await Dispatcher.UIThread.InvokeAsync(() => UpdateAsync());  // ← 同期marshal
        return;
    }

    await base.UpdateAsync();
    PostStatusCheck();
    // ...
}
```

**問題点**: `Dispatcher.UIThread.InvokeAsync()`は同期marshalであり、呼び出し元をブロックする。

#### 3. UpdateAsyncの再帰呼び出しチェーン (中)

```csharp
// VerilogFile.updateIncludeFilesAsync
internal static async Task updateIncludeFilesAsync(...)
{
    foreach (var includeFile in parsedDocument.IncludeFiles.Values)
    {
        // ...
        await updateIncludeFilesAsync(includeFile.VerilogParsedDocument, item.Items);  // 再帰呼び出し
    }
}
```

**問題点**: ネストされたincludeファイルがある場合、浅い階層でも全てのincludeファイルを処理してから戻る。

### 呼び出しタイミングの問題

1. **ParseWorker.Parse()** は `Task.Run()` により **Background Task** で実行される
2. **VerilogHeaderInstance.AcceptParsedDocumentAsync()** もこのBackground Task内で実行される
3. **UpdateAsync()** 内での `Dispatcher.UIThread.InvokeAsync()` は **Background Task → UIスレッド** へのmarshalを要求
4. marshalは同期的であるため、marshalが完了するまでBackground Taskがブロックされる

### 備考
- 調査のみの実施。修正は未実施。
- AGENTS.mdの「UIスレッドロック問題」(調査記録とは別の問題)も参照。

---

## 調査記録: NavigatePanel ノードクリック時のUIスレッドロック

### 問題概要
NavigatePanel でノードをクリックした場合に、大規模プロジェクトや深い階層構造を持つファイルで UI スレッドが長時間ロックされる場合がある。

### 呼び出しフロー (VerilogFileNode/VerilogModuleInstanceNode 共通)

```
ノードクリック [UI Thread]
    ↓
TreeControl.SelectNode() → TreeNode.OnSelected() [UI Thread]
    ↓
VerilogFileNode.OnSelected() / VerilogModuleInstanceNode.OnSelected() [UI Thread]
    ├→ base.OnSelected() [コンテキストメニュー作成]
    │
    ├→ await Controller.CodeEditor.SetTextFileAsync(TextFile, true) [UI Thread]
    │     └→ CodeView.SetTextFileAsync() → skipEvents管理
    │           └→ codeViewParser.EntryParse() [fire-and-forget, Background]
    │
    ├→ UpdateVisual() [UI Thread]
    │
    └→ Tool.ParseHierarchy.PostParseAsync(TextFile, SearchReparseReqestedTree) [UI → Background Post]
              ↓
        ParseHierarchy.ProcessQueueAsync() [Background]
              ↓
        ParseInternalAsync() → runParallel()
              ↓
        Workerタスク群が並列実行
              ↓
        各ファイルのparser.ParseAsync()完了
              ↓
        await verilogFile.AcceptParsedDocumentAsync(parser) [Background]
              ├→ ParsedDocument 設定・入れ替え
              ├→ source.RegisterInstanceParsedDocument() (Instance特有)
              ├→ await acceptParameterizedParsedDocumentAsync()
              │     └→ await VerilogFile.updateIncludeFilesAsync()
              │           └→ 各includeの CodeDocument.CopyColorMarkFrom()
              │           └→ 再帰的に nested include 処理
              │
              ├→ await UpdateAsync() [Background → UI marshal via InvokeAsync] ★ 主要因
              │     └→ VerilogCommon.Updater.UpdateAsync()
              │           └→ item.Items のアトミック更新 (semaphore使用)
              │           └→ 各子ノードの NavigatePanelNode.UpdateVisual()
              │                 └→ ノード数が多いとUI更新が連続発生
              │
              └→ NavigatePanelNode.UpdateVisual() [Instanceのみ]
                    └→ UpdateSubNodes()
```

### ノード種類別の相違点

#### VerilogFileNode.OnSelected()
```csharp
// VerilogFileNode.cs
public override async void OnSelected()
{
    // ...
    await CodeEditor2.Controller.CodeEditor.SetTextFileAsync(TextFile, true);  // parseEntry=true
    // ...
    // post hier parse on background
    Tool.ParseHierarchy.PostParseAsync(TextFile, Tool.ParseHierarchy.ParseMode.SearchReparseReqestedTree);
}
```

#### VerilogModuleInstanceNode.OnSelected()
```csharp
// VerilogModuleInstanceNode.cs
public override async void OnSelected()
{
    // ...
    await CodeEditor2.Controller.CodeEditor.SetTextFileAsync(ModuleInstance, true);
    UpdateVisual();  // ← OnSelected内で直接UpdateVisual呼び出し
    // ...
    // post hier parse on background
    Tool.ParseHierarchy.PostParseAsync(ModuleInstance, Tool.ParseHierarchy.ParseMode.SearchReparseReqestedTree);
}
```

#### VerilogHeaderInstanceNode.OnSelected()
```csharp
// VerilogHeaderInstanceNode.cs
public override async void OnSelected()
{
    // ...
    await CodeEditor2.Controller.CodeEditor.SetTextFileAsync(TextFile);  // parseEntryデフォルト=true
    // PostParseAsync呼び出しなし (Headerは親ファイルと一体でパースされるため)
}
```

### 主要因

| # | 原因 | 場所 | 重要度 |
|---|------|------|--------|
| 1 | **AcceptParsedDocumentAsync内でのUpdateAsync呼び出し** | `VerilogFile.cs:208`, `VerilogModuleInstance.cs` | **高** |
| 2 | **ParseHierarchy キュー処理でのAcceptParsedDocumentAsync呼び出し** | `ParseHierarchy.cs:289`, `ParseHierarchy.cs:351` | **高** |
| 3 | **UpdateVisualの呼び出しチェーン** | 各NodeクラスのUpdateSubNodes() | 中 |
| 4 | **UpdateAsync内のInvokeAsync marshal** | `TextFile.UpdateAsync()`, `Updater.UpdateAsync()` | 高 |
| 5 | **InstanceTextFileでのソースファイルへのAcceptParsedDocumentAsync伝搬** | `VerilogModuleInstance.cs:261-264` | 高 |

### 詳細分析

#### 1. AcceptParsedDocumentAsync内でのUpdateAsync呼び出し (高)

```csharp
// VerilogFile.cs
public override async Task AcceptParsedDocumentAsync(...)
{
    // ...
    await updateIncludeFilesAsync(vParsedDocument, Items);
    await UpdateAsync();  // ← パース完了後に呼び出し
}

// VerilogModuleInstance.cs
public override async Task AcceptParsedDocumentAsync(...)
{
    // ...
    source.RegisterInstanceParsedDocument(key, newParsedDocument, this);
    await acceptParameterizedParsedDocumentAsync(newParsedDocument);
    // ...
    // ソースファイルが単一モジュールの場合、親ファイルも更新
    if (source.ParsedDocument?.Root?.BuildingBlocks.Count == 1)
    {
        await source.AcceptParsedDocumentAsync(parser);  // ← 伝搬
    }
    // ...
    NavigatePanelNode.UpdateVisual();
}
```

**問題点**: `AcceptParsedDocumentAsync` は `ParseHierarchy` のパースワーカーから呼び出され、その中で `UpdateAsync()` を実行する。深い階層構造では、この呼び出しが連鎖的に発生し、処理時間が長くなる。

#### 2. ParseHierarchy キュー処理でのAcceptParsedDocumentAsync (高)

```csharp
// ParseHierarchy.cs - parseTextFile()
private static async Task parseTextFile(...)
{
    if (doParse)
    {
        var parser = verilogFile.CreateDocumentParser(...);
        await parser.ParseAsync();
        if (parser.ParsedDocument != null)
        {
            await verilogFile.AcceptParsedDocumentAsync(parser);  // ← パース完了後に呼び出し
            // ...
        }
    }
    // 子アイテムをワークキューに追加
    foreach (var item in items)
    {
        if (item is CodeEditor2.Data.TextFile tfile)
        {
            EnqueueWork(new ParseTask(...), ...);  // 次のファイルのキューに追加
        }
    }
}

// ParseHierarchy.cs - reparseText()
private static async Task reparseText(...)
{
    // ...
    await parser.ParseAsync();
    if (parser.ParsedDocument != null)
    {
        await verilogFile.AcceptParsedDocumentAsync(parser);  // ← 再度呼び出し
        // ...
    }
}
```

**問題点**: `ParseHierarchy` は `SearchReparseReqestedTree` モードでパースするため、対象ファイルの全インスタンスを走査してパースする。パース完了後に `AcceptParsedDocumentAsync` が呼び出され、そのたびに `UpdateAsync` が実行される。インスタンス数が多いほど呼び出し回数が増加する。

#### 3. UpdateVisualの呼び出しチェーン (中)

```csharp
// VerilogFileNode.cs
public override void UpdateVisual()
{
    // ...
    UpdateSubNodes();  // 子ノードを更新
}

private void UpdateSubNodes()
{
    // ノード数が多い場合、多くのUI更新が発生
    List<CodeEditor2.NavigatePanel.NavigatePanelNode> newNodes = new ...;
    foreach (CodeEditor2.Data.Item item in VerilogFile.Items)
    {
        newNodes.Add(item.NavigatePanelNode);
    }
    // Nodesの挿入・削除・Dispose処理
}

// VerilogModuleInstanceNode.cs
public override void UpdateVisual()
{
    // ...
    UpdateSubNodes();  // VerilogFileNodeと同様の処理
    // さらに子のNavigatePanelNode.UpdateVisual()も呼び出し
}
```

**問題点**: 子ノード数が多い場合、`UpdateSubNodes()` で多くの UI 更新が発生し、UI スレッドを占有する。また、各ノードの `UpdateVisual()` が連鎖的に呼び出される。

#### 4. UpdateAsync内のInvokeAsync marshal (高)

```csharp
// TextFile.cs
public override async Task UpdateAsync()
{
    await base.UpdateAsync();
    PostStatusCheck();

    Dispatcher.UIThread.Post(() =>  // Post使用
    {
        NavigatePanelNode?.UpdateVisual();
        if (Controller.NavigatePanel.GetSelectedFile() == this)
        {
            Controller.CodeEditor.PostRefresh();
            if (ParsedDocument != null) Controller.MessageView.Update(ParsedDocument);
        }
    });
}
```

**問題点**: `VerilogFile.UpdateAsync()` と `VerilogModuleInstance.UpdateAsync()` は `Updater.UpdateAsync()` を呼び出すが、このメソッドが重い処理を行う。また、`AcceptParsedDocumentAsync` 内で `UpdateAsync()` を呼び出すため、パース完了後に marshal が発生する。

#### 5. InstanceTextFileでのソースファイルへのAcceptParsedDocumentAsync伝搬 (高)

```csharp
// VerilogModuleInstance.cs
public override async Task AcceptParsedDocumentAsync(...)
{
    // ...
    source.RegisterInstanceParsedDocument(key, newParsedDocument, this);
    await acceptParameterizedParsedDocumentAsync(newParsedDocument);

    // ソースファイルが単一モジュールのhevilogファイルの場合、親ファイルを更新
    if (source.ParsedDocument?.Root?.BuildingBlocks.Count == 1)
    {
        await source.AcceptParsedDocumentAsync(parser);  // ← ソースファイルにも伝搬
    }
    // ...
}
```

**問題点**: `VerilogModuleInstance` がパース完了時に、ソースファイル (`VerilogFile`) にも `AcceptParsedDocumentAsync` を呼び出す。これにより、同じファイルを複数回更新する可能性がある。

### VerilogModuleInstanceのInstanceTextFile.UpdateAsync問題

```csharp
// InstanceTextFile (親クラス) は UpdateAsync() をoverrideしていない
// VerilogModuleInstance.UpdateAsync() は:
public override async Task UpdateAsync()
{
    await base.UpdateAsync();  // TextFile.UpdateAsync()
    await VerilogCommon.Updater.UpdateAsync(this, itemUpdateSemaphore);
    // ※ UpdateVisual() は呼び出さない (AcceptParsedDocumentAsync内で呼び出される)
}
```

**問題点**: `InstanceTextFile` には `UpdateAsync()` がなく、`TextFile.UpdateAsync()` がそのまま使用される。ただし、`VerilogModuleInstance` は `AcceptParsedDocumentAsync` 内で `NavigatePanelNode.UpdateVisual()` を呼び出す。

### ロック時間の推定要素

| 要素 | 影響 |
|------|------|
| プロジェクト階層深度 | 深いほど `ParseHierarchy` の処理時間が長い |
| インスタンス数 | インスタンスが多いほど `AcceptParsedDocumentAsync` 呼び出し回数増加 |
| include ファイル数 | 各 include ファイルの `updateIncludeFilesAsync` が再帰的に実行 |
| 子ノード数 | `UpdateSubNodes()` で UI 更新回数が増加 |
| パース速度 | ファイルサイズ、複雑さに依存 |
| 単一モジュールファイル数 | `VerilogModuleInstance` でのソースファイル伝搬回数 |

### 備考
- 調査のみの実施。修正は未実施。
- ノードクリック時の主要処理は `VerilogFileNode.OnSelected()` と `VerilogModuleInstanceNode.OnSelected()` で行われる
- `PostParseAsync` は fire-and-forget で実行されるが、完了後に `AcceptParsedDocumentAsync` が呼び出され、その中で UI 操作が発生する
- `VerilogHeaderInstanceNode` は `PostParseAsync` を呼び出さない（ヘッダーファイルは親ファイルと一体でパースされるため）

---

## License

MIT License
