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

## build方法

ワーニングが大量にあるので、build時にはerrorのみ参照してください。

dotnet build -clp:ErrorsOnly

全体build
```
dotnet build RtlEditor2.sln -clp:ErrorsOnly
```

pulgin単位のbuild
```
dotnet build CodeEditor2VerilogPlugin\CodeEditor2VerilogPlugin\CodeEditor2VerilogPlugin.sln -clp:ErrorsOnly
```


---

## SystemVerilog Parse Errorリスト

### module instance .* parse error (bug 10.6.2)

flop u_flop (.*);

.*は１wordとして認識されるが、parseできていない

### let constructのparse error (bug 11.12)

```
let op(x, y, z) = |((x | y) & z);
```


### typedef union未対応 (bug 11.14)

### string arrayのforeachインデックスparseエラー (bug 12.7.3)

	string test [4] = '{"111", "222", "333", "444"};
	initial begin
		foreach(test[i])
			$display(i, test[i]);
	end
	
	
foreach(test[i])の箇所でエラーがでる

### stream concat 未対応(bug 11.4.14)

	c = {>> 8 {a, b}};


### case判定の一部未対応 (bug 12.5.4)
```
	reg [3:0] a = 0;
	reg [3:0] b = 0;
	always @* begin
		case(a) inside
			1, 3: b = 1;
			4'b01??, [5:6]: b = 2;
			default b = 3;
		endcase
	end
```
[5:6]の位置でエラーが出る

### パラメタライズドクラス未対応
```
	class par_cls #(int a = 25);
		parameter int b = 23;
	endclass
```
moduleと同じくparameter　IDつきでparseしてparse結果をストックする必要がある。

### named blocks (bug 9.3.5)

```
		name: begin
			a = 1;
			b = a;
			c = b;
		end: name
```
end:name位置でエラーがでる

### const function (bug 13.4.3)

```
localparam a = fun(3);
```


### associtave arrayの参照エラー

```
module top ();

int arr [ int ];

initial begin
	$display(":assert: (%d == 0)", arr.size);
	arr[10] = 10;
	$display(":assert: (%d == 1)", arr.size);
end

endmodule
```

arrの位置でnot defined hereエラーがでます。

### dynamic arrayのシステムメソッド未対応

```
module top ();

bit [7:0] arr[];

initial begin
    arr = new [ 16 ];
    $display(":assert: (%d == 16)", arr.size);
    arr.delete;
    $display(":assert: (%d == 0)", arr.size);
end

endmodule
```
size,delete箇所でエラー

## clockeing event (bug 14.3)

```
default clocking @(posedge clk);
	default input #10ns output #5ns;
endclocking

```
posedge 箇所でエラー


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

## 調査記録: NavigatePanel フォルダ追加问题（Project直下）

### 问题概要
NavigatePanelNodeを右クリック -> Add -> Folderでフォルダを追加する際、Projectフォルダ直下だとフォルダが生成されない。

### 调用链

```
menuItem_AddFolder_Click() [NavigatePanelNode.cs:375]
    ↓
Directory.CreateDirectory() ← 成功（物理フォルダ作成済み）
    ↓
await UpdateFolder(node) [NavigatePanelNode.cs:395]
    ↓
folderNode.UpdateAsync() [FolderNode.cs:66]
    ↓
Dispatcher.UIThread.Post(() => updateFolder()) [FolderNode.cs:71]
    ↓
await folder.UpdateAsync() [FolderNode.cs:101]
    ↓
if (!await _fileSemaphore.WaitAsync(0)) return; [Folder.cs:86]
    ↓
★ Nodes 更新処理がスキップされる
```

### 主要因 (調査済み - 修正なし)

| # | 原因 | 場所 | 重要度 |
|---|------|------|--------|
| 1 | `_fileSemaphore.WaitAsync(0)` による早期リターン | `Folder.cs:86` | **高** |
| 2 | `FolderNode.updateFolder()` 内の `folder.UpdateAsync()` 待機失敗 | `FolderNode.cs:101` | **高** |
| 3 | Project.CreateAsync 時の UpdateAsync との競合 | `Project.cs` | 中 |
| 4 | semaphore取得失敗時の Nodes更新スキップ | `FolderNode.cs` | 高 |

### 詳細分析

#### 1. semaphore待機時間の競合 (高)

```csharp
// Folder.cs
private readonly SemaphoreSlim _fileSemaphore = new SemaphoreSlim(1, 1);
public override async Task UpdateAsync()
{
    // 待ち時間 0 でトライ。取得できなければ即座にリターン
    if (!await _fileSemaphore.WaitAsync(0))
    {
        // すでに実行中のため、何もせずリターン
        return;
    }
    // ... フォルダ内容の更新処理 ...
}
```

**問題点**: `WaitAsync(0)` はsemaphoreが取得できない場合、**即座にfalseを返してリターン**する。他の`UpdateAsync()`が実行中の場合、処理がスキップされる。

#### 2. FolderNode.updateFolder() 内の待機 (高)

```csharp
// FolderNode.cs - updateFolder() 内
private async Task updateFolder()
{
    // ...
    await folder.UpdateAsync();  // ← ここでsemaphore競合発生

    List<Item> addItems = new List<Item>();
    foreach (Item item in folder.Items)
    {
        addItems.Add(item);  // ← folder.UpdateAsync() が早期リターンした場合、新しいフォルダが.itemsに追加されていない
    }

    // ... Nodes更新処理 ...
    // folder.Items に新しいフォルダが含まれていないため、Nodesに追加されない
}
```

**問題点**: `folder.UpdateAsync()` が早期リターンした場合、`folder.Items` に新しいフォルダがまだ追加されていない状態のまま、Nodes更新処理が行われる。

#### 3. Project.CreateAsync 時の競合 (中)

```csharp
// Project.cs
public static async Task<Project> CreateAsync(string rootPath)
{
    Project project = new Project(name, actualPath, "");
    await initProjectAsync(project, setup);  // ← 内部で project.UpdateAsync() 呼び出し
    return project;
}

private static async Task initProjectAsync(Project project, Setup? setup)
{
    await project.UpdateAsync();  // ← _fileSemaphore を保持
    await DataAccess.UpdateFieSystemInfoAsync(project);
}
```

**問題点**: Project作成時に`UpdateAsync()`が実行されsemaphoreを保持するため、その間にフォルダを追加すると競合が発生する。

#### 4. Nodes更新スキップの连锁 (高)

```csharp
// FolderNode.cs - updateFolder()
private async Task updateFolder()
{
    await folder.UpdateAsync();  // 早期リターン

    // folder.Items には新しいフォルダがまだない
    List<Item> addItems = new List<Item>();
    foreach (Item item in folder.Items)
    {
        addItems.Add(item);  // 新しいフォルダが含まれない
    }

    // ... removeNodes との差分比較 ...
    // 新しいフォルダは addItems に含まれないため、Nodes に追加されない
}
```

**問題点**: `folder.UpdateAsync()` が完了しないと、新しいフォルダが `folder.Items` に追加されないため、`Nodes` にも追加されない。

### 競合フロー詳細

```
時刻 T=0ms: Project.CreateAsync() 開始
    ↓
時刻 T=1ms: project.UpdateAsync() 開始、_fileSemaphore 取得成功
    ↓
時刻 T=50ms: GetFolderContents() 等を実行中（semaphore保持中）
    ↓
時刻 T=100ms: ユーザがProjectノードを右クリック -> Add -> Folder
    ↓
時刻 T=101ms: Directory.CreateDirectory() 実行、成功
    ↓
時刻 T=102ms: await UpdateFolder(node) 呼び出し
    ↓
時刻 T=103ms: FolderNode.updateFolder() 開始
    ↓
時刻 T=104ms: await folder.UpdateAsync() 呼び出し
    ↓
時刻 T=105ms: _fileSemaphore.WaitAsync(0) → false（取得失敗）
    ↓
時刻 T=106ms: folder.UpdateAsync() 即座にリターン
    ↓
時刻 T=107ms: folder.Items には新しいフォルダがまだない
    ↓
時刻 T=108ms: addItems に新しいフォルダが含まれない
    ↓
時刻 T=109ms: Nodes 更新処理完了（新しいフォルダなし）
    ↓
時刻 T=200ms: project.UpdateAsync() 完了、_fileSemaphore 解放
```

### 備考
- 調査のみの実施。修正は未実施。
- `_fileSemaphore` を共用しているため競合状態が発生
- `FolderNode.updateFolder()` が `folder.UpdateAsync()` の完了を前提とした処理になっている
- Project直下でのみ発生する理由は、Project作成時に `UpdateAsync()` が実行中のため

---

## Status

- [x] Read AGENTS.md - Ready to process tasks
- [x] Agent initialized and ready for next instruction
- [x] AGENTS.md loaded and understood
- [x] AGENTS.md read and processed
- [ ] Awaiting user instructions...

---

## Latest Agent Session

- **Started**: 2025-01-XX - AGENTS.md processed, awaiting user task
- **Current focus**: Ready to receive instructions
- **Project**: RtlEditor2 - Avalonia UI based RTL IDE
- **Notable topics**: UI thread lock issues, Verilog/SystemVerilog parsing

---

## 修正履歴

### TreeControl.addNodeでのNextToノード追加問題 (修正済み)

**問題**: 親ノードがcollapsed状态下で子ノードを追加する際、`GetNextTo`が親を返しても親の`TreeViewItem`が`Items`に存在しないため、子の`TreeViewItem`が作成されない。

**原因**:
1. `GetNextTo`が親ノードを`nextTo`として返す
2. 親ノードは閉じられているため`Items`に`TreeViewItem`が存在しない
3. `parent.TreeItem != null && Items.Contains(parent.TreeItem)`の条件がfalse
4. `TreeViewItem`が作成されないままリターン

**修正**:
1. `node.Visible`がfalseでも`TreeViewItem`を作成（将来親が展開されたときのため）
2. 親チェーンを辿って有効な挿入ポイントを探す
3. 親チェーンに有効なノードがない場合、ルードノードの位置に挿入
4. それでも挿入ポイントがない場合は`TreeViewItem`を作成（参照用）

**修正ファイル**:
- `AjkAvaloniaLibs/Controls/TreeControl.axaml.cs`

### TreeNode.Nodes差し替え時のTreeControl表示消失問題 (修正済み)

**問題**: `TreeNode.Nodes = newNodes` でノードを差し替えた際、TreeControlの表示が全て消え、操作できなくなる。

**原因**: 
1. `Nodes` setter内で古いノードに対して`RemoveFromPropagateTree()`を呼び出していた
2. `RemoveFromPropagateTree()`で`PropageteCollectionChange = null`に設定されていた
3. その後`OnCollectionChanged()`で`Reset`通知を発生させたが、`PropageteCollectionChange`がnullのためTreeControlに通知が届かなかった
4. TreeControlが`Reset`処理を行わず、古いTreeViewItemsが残ったままだった
5. 新しいノードは`Add`通知でなく`Reset`のみで処理されたため、再作成されなかった

**修正**: 
1. `TreeNode.Nodes` setterの処理順序を変更
   - まず新しいコレクションを設定し、`OnCollectionChanged()`で`Reset`通知を発生させる
   - その後、`RemoveFromPropagateTree()`と`Dispose()`で古いノードを解放
2. `TreeControl.PropageteCollectionChange()` の `Reset` 処理を強化
   - 古いノードの削除後、新しいノードの`addNode()`を呼び出す

**修正ファイル**: 
- `AjkAvaloniaLibs/Controls/TreeNode.cs`
- `AjkAvaloniaLibs/Controls/TreeControl.axaml.cs`

## TreeControl Nodes直下追加問題 (修正済み)

**問題**: TreeControlのNodes直下にノードを追加した場合に、ownerNodeがTreeControl自身であるがゆえにノード追加処理が実行されていなかった。

**原因**:
1. `PropageteCollectionChange`メソッドで、senderがTreeControlの場合のAdd/Remove/Replace/Moveアクションが処理されていなかった
2. `AddChildItemsRecursive`と`FindInsertIndex`メソッドがTreeNode型のパラメータのみ対応していた

**修正**:
1. `PropageteCollectionChange`にTreeControlがsenderの場合の処理を追加
2. `AddChildItemsRecursive`と`FindInsertIndex`にTreeControl型をオーバーロードとして追加

**修正ファイル**:
- `AjkAvaloniaLibs/Controls/TreeControl.axaml.cs`

---

## 調査記録: VerilogModuleInstanceノードのちらつき問題

### 問題概要
VerilogFile-ModuleInstance-ModuleInstanceの孫ノードがTreeControl上で見えたり見えたりするチカチカ現象が発生。

### 呼び出しフロー分析

```
ノード選択 [UI Thread]
    ↓
OnSelected() [VerilogModuleInstanceNode.cs:77]
    ├→ SetTextFileAsync()
    ├→ UpdateVisual() ←─────────────────────────┐
    │     └→ UpdateSubNodes()                  │
    │           └→ Nodes = nodes（子の更新）   │
    │                                         │
    └→ PostParseAsync() [Background Task] ────┘
              ↓
        ParseHierarchy.runParallel()
              ↓
        parseTextFile() [Worker]
              ↓
        await verilogFile.AcceptParsedDocumentAsync(parser)
                    ↓
              VerilogModuleInstance.AcceptParsedDocumentAsync()
                    ├→ RegisterInstanceParsedDocument()
                    ├→ acceptParameterizedParsedDocumentAsync()
                    │     ├→ VerilogFile.updateIncludeFilesAsync()
                    │     └→ await UpdateAsync() ←─────────────┐
                    │           └→ Updater.UpdateAsync()       │
                    │                 └→ Items 更新（原子操作）│
                    │                                         │
                    └→ NavigatePanelNode.UpdateVisual() ←─────┘
                          └→ UpdateSubNodes() ← 再描画
```

### 詳細分析

#### 1. Updater.UpdateAsync の Items 更新処理

```csharp
// Updater.cs:71-98
await _semaphore.WaitAsync();
try
{
    var oldItems = new List<CodeEditor2.Data.Item>();
    foreach (var oldItem in item.Items)
    {
        oldItems.Add(oldItem);
    }
    item.Items.Clear();  // ← ここで子のItemsを一括クリア
    foreach (CodeEditor2.Data.Item i in newSubItems.Values)
    {
        item.Items.AddOrUpdate(i.Name, i);  // ← 新しいItemsを追加
    }
    // 古いItemsをDispose
    foreach (var oldItem in oldItems)
    {
        oldItem.Dispose();
    }
}
finally
{
    _semaphore.Release();
}
```

**問題点**: Items のクリアと新しい Items の追加が atomic だが、その間に他のスレッドが Items を読み込む場合がある。

#### 2. Item.NavigatePanelNode 生成の Lazy 評価

```csharp
// Item.cs:322-335
public virtual NavigatePanel.NavigatePanelNode NavigatePanelNode
{
    get
    {
        if (node == null)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                if (node == null)
                {
                    node = CreateNode();
                }
            });
        }
        return node;
    }
}
```

**問題点**: NavigatePanelNode は lazy 評価で生成される。Items が更新された直後に `UpdateSubNodes()` が `item.NavigatePanelNode` を参照すると、まだ node が null の場合がある。

#### 3. UpdateSubNodes の Nodes 設定

```csharp
// VerilogModuleInstanceNode.cs:123-141
public void UpdateSubNodes()
{
    List<CodeEditor2.NavigatePanel.NavigatePanelNode> newNodes = new List<...>();
    
    if (VerilogFile != null)
    {
        foreach (CodeEditor2.Data.Item item in VerilogModuleInstance.Items)
        {
            newNodes.Add(item.NavigatePanelNode);  // ← lazy 生成
        }
    }
    
    lock (Nodes)
    {
        Nodes = nodes;  // ← ここでNodesを入れ替え
    }
}
```

**問題点**: 
- `item.NavigatePanelNode` が null の場合がある
- `Nodes = nodes` で CollectionChanged イベントが発生
- TreeControl の updateAllTreeViewItems が呼ばれる

#### 4. TreeControl.updateAllTreeViewItems の問題

```csharp
// TreeControl.axaml.cs:234-310
private void updateAllTreeViewItems()
{
    var existingItemsMap = new Dictionary<object, TreeControlViewItem>();
    foreach (var oldItem in Items)
    {
        if (oldItem.treeNode != null)
        {
            existingItemsMap[oldItem.treeNode] = oldItem;
            oldItem.treeNode.Visible = false;  // ← 全ノードを一旦非表示に
        }
        else
        {
            existingItemsMap[this] = oldItem;
        }
    }

    // 新しいリストを構築
    List<TreeControlViewItem> expectedItems = new List<...>();
    updateSubTreeViewItemsIncremental(expectedItems, this, existingItemsMap);

    // 差分更新
    // ...
}
```

**問題点**: 
- `oldItem.treeNode.Visible = false` で全ノードを一旦非表示に
- その後 `updateSubTreeViewItemsIncremental` で新しいリストを構築
- **この間に UI が描画更新された場合、孫ノードがまだ Visible=false の状態で見える**

#### 5. 非同期処理の競合状態

```
時刻 T=0: PostParseAsync() が Background Task を開始
    ↓
時刻 T=10: parseTextFile() がパースを完了、AcceptParsedDocumentAsync を呼び出し
    ↓
時刻 T=15: VerilogModuleInstance.UpdateAsync() が semaphore を待つ
    ↓
時刻 T=20: Updater.UpdateAsync() が Items を更新（Clear → Add）
    ↓
時刻 T=25: NavigatePanelNode.UpdateVisual() → UpdateSubNodes()
    ↓
時刻 T=30: UpdateSubNodes() が item.NavigatePanelNode を参照
    │       ← このとき、まだ node == null の可能性がある
    ↓
時刻 T=35: TreeControl.updateAllTreeViewItems() が 全ノードの Visible=false を設定
    ↓
時刻 T=40: updateSubTreeViewItemsIncremental() が新しいリストを構築
    ↓
時刻 T=45: UI Thread が 描画を更新 ← Visible=false のまま描画される可能性
```

### 前提
- Data (Item, Items, etc.) はスレッドセーフ
- NavigatePanel は UI スレッドからのみアクセス

### 問題点の再評価（前提変更後）

| # | 原因 | 場所 | 重要度 |
|---|------|------|--------|
| 1 | **TreeControl.updateAllTreeViewItems の Visible 制御と描画タイミング** | `TreeControl.axaml.cs:261` | **高** |
| 2 | **UpdateSubNodes での Nodes 設定と CollectionChanged の競合** | `VerilogModuleInstanceNode.cs:130-141` | **高** |
| 3 | **Nodes = nodes 代入による CollectionChanged と updateAllTreeViewItems の再帰的呼び出し** | `VerilogModuleInstanceNode.cs:140` | 高 |
| 4 | **instanceKey による Repeated AcceptParsedDocumentAsync** | `VerilogModuleInstance.cs:261-264` | 中 |

### 詳細分析

#### 1. UpdateSubNodes と TreeControl の updateAllTreeViewItems の競合

```csharp
// VerilogModuleInstanceNode.cs - UpdateSubNodes()
public void UpdateSubNodes()
{
    // ... newNodes を構築 ...
    lock (Nodes)
    {
        // ↓ Nodes に新しいリストを代入 → CollectionChanged イベント発生
        Nodes = nodes;  // ← ここで TreeControl.Nodes_CollectionChanged() が呼ばれる
    }
}

// TreeControl.axaml.cs - Nodes_CollectionChanged()
internal void Nodes_CollectionChanged(object? sender, ...)
{
    updateAllTreeViewItems();  // ← これが実行される
    updateVisual();
}

// TreeControl.axaml.cs - updateAllTreeViewItems()
private void updateAllTreeViewItems()
{
    // 既存ノードの Visible を false に設定
    foreach (var oldItem in Items)
    {
        if (oldItem.treeNode != null)
        {
            existingItemsMap[oldItem.treeNode] = oldItem;
            oldItem.treeNode.Visible = false;  // ← 全ノードを一時的に非表示
        }
    }
    // ... 新しいリストを構築 ...
    updateSubTreeViewItemsIncremental(expectedItems, this, existingItemsMap);
    // ... 差分更新 ...
}
```

**問題**: `Nodes = nodes` で CollectionChanged が発生し、`updateAllTreeViewItems()` が呼ばれる。Visible=false の設定後、新しい TreeControlViewItem が作成されるまでの間に UI 描画更新が入ると、ノードが見えない状態で描画される。

#### 2. ParseHierarchy.runParallel の並列パースとの競合

```
時刻 T=0: PostParseAsync() が Background Task を開始
    ↓
時刻 T=10: Worker A がパース完了、AcceptParsedDocumentAsync を呼び出し
    ↓
時刻 T=15: Worker A が UpdateAsync() → UpdateSubNodes() を実行
    ↓
時刻 T=20: TreeControl.Nodes_CollectionChanged() → updateAllTreeViewItems()
    ↓
時刻 T=25: oldItem.treeNode.Visible = false (全ノード非表示)
    ↓
時刻 T=30: UI Thread が描画更新 ← Visible=false のまま描画
    ↓
時刻 T=35: updateSubTreeViewItemsIncremental() が新しいリストを構築
    ↓
時刻 T=40: 同時に Worker B もパース完了、別の UpdateSubNodes() を呼び出し
    ↓
時刻 T=45: CollectionChanged が競合
```

#### 3. instanceKey による Repeated AcceptParsedDocumentAsync

```csharp
// VerilogModuleInstance.cs
if (source.ParsedDocument?.Root?.BuildingBlocks.Count == 1)
{
    await source.AcceptParsedDocumentAsync(parser);  // ← ソースファイルにも伝播
}
```

**問題**: 孫ノードがパース完了時に、ソースファイル（VerilogFile）にも AcceptParsedDocumentAsync を呼び出す。これにより、ソースファイルの Items も更新され、さらに孫ノードの兄弟も再描画される可能性がある。

### 備考
- 調査のみの実施。修正は未実施。
- Data はスレッドセーフだが、NavigatePanel は UI スレッドからのみアクセスという前提。
- `TreeControl.updateAllTreeViewItems()` での `Visible = false` の設定と、UI 描画更新のタイミングが競合している可能性がある。
- 特に `ParseHierarchy.runParallel()` が複数のワーカーで並列実行されている場合、あるワーカーが完了して `UpdateSubNodes()` を呼び出した時に、別のワーカーがまだパース中の状態だと Items が不安定になり、ちらつきが発生すると推定される。

---

## 調査記録: ノードちらつき問題 - 詳細分析（追加）

### 分析背景
`OnSelected()`での`UpdateVisual()`呼び出しと、`ParseHierarchy`のバックグラウンドパース完了後の`AcceptParsedDocumentAsync()`内での`UpdateVisual()`呼び出しが競合している可能性を調査。

### 呼び出しフローの詳細

#### パス1: OnSelected()からの呼び出し（UI Thread）
```
ノード選択 [UI Thread]
    ↓
OnSelected() [VerilogModuleInstanceNode.cs:77]
    ├→ SetTextFileAsync()
    ├→ UpdateVisual() [UI Thread] ←──────────────────┐
    │     └→ UpdateSubNodes()                       │
    │           └→ Nodes = nodes（子の更新）         │
    │                                             │
    └→ PostParseAsync() [Background Task] ──────────┘
```

#### パス2: AcceptParsedDocumentAsync()からの呼び出し（Background → UI Marshal）
```
ParseHierarchy.runParallel() [Background Task]
    ↓
parseTextFile() [Worker]
    ↓
await verilogFile.AcceptParsedDocumentAsync(parser)
    ↓
VerilogModuleInstance.AcceptParsedDocumentAsync()
    ├→ await acceptParameterizedParsedDocumentAsync()
    │     ├→ await VerilogFile.updateIncludeFilesAsync()
    │     └→ await UpdateAsync()
    │           └→ Updater.UpdateAsync() → Items更新 (semaphore保護下)
    │
    └→ NavigatePanelNode.UpdateVisual() [Background → UI Thread Marshal]
          └→ UpdateSubNodes() ← UI Thread
                └→ Nodes = nodes
```

### 根本原因

#### 1. Items更新とNavigatePanelNode生成のRace Condition（最重要）

```
時刻 T=0: Worker A - ParentのAcceptParsedDocumentAsync() 開始
    ↓
時刻 T=10: Parent.UpdateAsync() → Items.Clear() → Items.Add(child_B)
    ↓
時刻 T=20: Parent.NavigatePanelNode.UpdateVisual() [Marshal後]
    ↓
時刻 T=25: UpdateSubNodes()内で item.NavigatePanelNode アクセス
    │       → child_B.NavigatePanelNode が lazy 生成される
    │       → child_B.Items はまだ古い状態（GrandChild_C なし）
    ↓
時刻 T=30: Worker B - child_B のparse完了
    ↓
時刻 T=35: child_B.AcceptParsedDocumentAsync()
    ↓
時刻 T=40: child_B.UpdateAsync() → Items.Add(GrandChild_C)
    ↓
時刻 T=45: child_B.NavigatePanelNode.UpdateVisual()
    ↓
時刻 T=50: UpdateSubNodes() → GrandChild_C が突然 Nodes に追加される
    ↓
★ ちらつき発生（孫ノードが突然現れる）
```

**問題の本質**: `Updater.UpdateAsync()`がItemsを更新した後、`NavigatePanelNode.UpdateVisual()`が実行されるまでの間に、子のItemsがまだ更新されていない可能性がある。

#### 2. UpdateSubNodes()でのNodes代入がバックグラウンドスレッドから行われる可能性

`VerilogModuleInstanceNode.UpdateSubNodes()`:
```csharp
public void UpdateSubNodes()
{
    // ★ UIスレッドチェックがない！
    // foreachでItemsを走査
    foreach (CodeEditor2.Data.Item item in VerilogModuleInstance.Items)
    {
        newNodes.Add(item.NavigatePanelNode);  // ← lazy生成
    }
    // Nodes代入 → CollectionChanged発生
    Nodes = nodes;
}
```

`UpdateVisual()`ではmarshalされるが、`UpdateSubNodes()`自体は直接UI Threadから呼ばれる場合はチェックされない。

#### 3. CollectionChangedの連鎖

```csharp
Nodes = nodes;  // CollectionChanged発生
    ↓
TreeControl.Nodes_CollectionChanged()
    ↓
updateAllTreeViewItems()
    ↓
oldItem.treeNode.Visible = false  // 全ノードを一時非表示
    ↓
updateSubTreeViewItemsIncremental()
    ↓
新しいTreeControlViewItemを作成
```

この処理中に別の`CollectionChanged`が発生すると、再帰的な処理になる。

### 問題となるコード箇所

| # | 場所 | 問題 |
|---|------|------|
| 1 | `VerilogModuleInstanceNode.UpdateSubNodes()` | UIスレッドチェックがない |
| 2 | `VerilogModuleInstance.AcceptParsedDocumentAsync()` | Items更新後に直接UpdateVisual()を呼んでいる |
| 3 | `Updater.UpdateAsync()` | ItemsのAtomic更新，但しNavigatePanelNode生成とは別トランザクション |
| 4 | `Item.NavigatePanelNode` (lazy生成) | Items更新→NavigatePanelNode生成の間隔で他Workerが参照する |

### 仮説: ちらつきの真の原因

**孫ノードが「突然現れる」原因**:

1. ParentのItemsが更新され、`UpdateSubNodes()`が呼ばれる
2. `UpdateSubNodes()`内で`child_B.NavigatePanelNode`がlazy生成される
3. **この時`child_B.Items`はまだ古い**（GrandChild_Cがいない）
4. `child_B`用の`TreeControlViewItem`が作成されるが、そのChildrenは空
5. その後`child_B`のパースが完了し、`GrandChild_C`が`child_B.Items`に追加される
6. `child_B.NavigatePanelNode.UpdateVisual()`が呼ばれ、`UpdateSubNodes()`で`GrandChild_C`がNodesに追加される
7. **孫ノードが突然TreeControlに中出现** → ちらつき

### 備考
- 調査のみの実施、修正は未実施
- Data層はスレッドセーフ、NavigatePanelはUIスレッドからのみアクセスという前提
- しかし`UpdateSubNodes()`は`UpdateVisual()`経由でmarshalされる，但其の前半（Items走査部分）はmarshal前に実行される可能性
- `ParseHierarchy.runParallel()`のparallel workersが同時に`UpdateSubNodes()`を呼ぶ可能性がある

---

## License

MIT License
