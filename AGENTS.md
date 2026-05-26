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
dotnet build -clp:ErrorsOnly RtlEditor2.sln
```

pulgin単位のbuild
```
dotnet build -clp:ErrorsOnly CodeEditor2VerilogPlugin\CodeEditor2VerilogPlugin\CodeEditor2VerilogPlugin.sln
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

---

## 修正履歴

### TreeNode.Nodes差し替え時のTreeControl表示消失問題 (修正済み)

**問題**: `TreeNode.Nodes = newNodes` でノードを差し替えた際、TreeControlの表示が全て消え、操作できなくなる。

**原因**: 
1. `Nodes` setter内で古いノードを即座に`Dispose()`していた
2. `Dispose()`で`TreeItem = null`に設定されていた
3. `Reset`通知の処理時に`ownerItem.TreeItem`がnullになっていた
4. `removeAllTreeItem`が呼ばれず、古いTreeViewItemsが残ったままだった
5. 新しいノードは`Add`通知でなく`Reset`のみで処理されたため、再作成されなかった

**修正**: `Nodes` setterの処理順序を変更
1. まず`RemoveFromPropagateTree()`で古いノードの传播链だけを解除（TreeItemは維持）
2. `OnCollectionChanged()`で`Reset`通知を発生させる
3. その後、`Dispose()`で古いノードを完全解放

**修正ファイル**: `AjkAvaloniaLibs/Controls/TreeNode.cs`

## License

MIT License
