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
RtlEditor2/                                # Main repository (git add/commit here)
├── RtlEditor2.Desktop/                    # Main desktop application
├── AjkAvaloniaLibs/                        # Utility library (submodule)
├── AjkLibs/                                # Utility library (submodule)
├── AvaloniaEdit/                           # Text editor control (submodule)
├── CodeEditor2/                            # Core editor component (submodule)
│   └── CodeEditor2/CodeEditor2/           # CodeDocument, Parser, Views, etc.
├── CodeEditor2Plugin/                      # Base plugin interface (submodule)
├── CodeEditor2VerilogPlugin/               # Verilog/SystemVerilog language support (submodule)
├── CodeEditor2AiPlugin/                    # AI integration plugin (submodule)
├── CodeEditor2AiAssistant/                 # AI Assistant (submodule)
├── CodeEditor2DrawIoPlugin/                # Draw.io diagram support (submodule)
├── CodeEditor2IcarusVerilogPlugin/         # Icarus Verilog simulation integration (submodule)
├── CodeEditor2LiveMarkdownPlugin/          # Live Markdown support (submodule)
├── CodeEditor2MarkdownPlugin/              # Markdown support (submodule)
├── CodeEditor2VivadoPlugin/                # Xilinx Vivado integration (submodule)
├── ajkCefGlue/                             # CEF support (submodule)
└── TemplateEngineHost/                     # Template engine
```

### サブモジュール一覧

| サブモジュール | URL | 用途 |
|---------------|-----|------|
| `CodeEditor2` | git@github.com:ajkfds/CodeEditor2.git | コアエディタコンポーネント |
| `CodeEditor2Plugin` | git@github.com:ajkfds/CodeEditor2Plugin.git | ベースプラグインインターフェース |
| `CodeEditor2VerilogPlugin` | git@github.com:ajkfds/CodeEditor2VerilogPlugin.git | Verilog/SystemVerilog言語サポート |
| `CodeEditor2AiPlugin` | git@github.com:ajkfds/CodeEditor2AiPlugin.git | AI統合プラグイン |
| `CodeEditor2AiAssistant` | git@github.com:ajkfds/CodeEditor2AiAssistant.git | AIアシスタント |
| `CodeEditor2MarkdownPlugin` | git@github.com:ajkfds/CodeEditor2MarkdownPlugin.git | Markdownサポート |
| `CodeEditor2LiveMarkdownPlugin` | git@github.com:ajkfds/CodeEditor2LiveMarkdownPlugin.git | ライブMarkdownサポート |
| `CodeEditor2DrawIoPlugin` | git@github.com:ajkfds/CodeEditor2DrawIoPlugin.git | Draw.io図形サポート |
| `CodeEditor2IcarusVerilogPlugin` | git@github.com:ajkfds/CodeEditor2IcarusVerilogPlugin.git | Icarus Verilogシミュレーション統合 |
| `CodeEditor2VivadoPlugin` | git@github.com:ajkfds/CodeEditor2VivadoPlugin.git | Xilinx Vivado統合 |
| `AjkAvaloniaLibs` | git@github.com:ajkfds/AjkAvaloniaLibs.git | Avalonia UIユーティリティライブラリ |
| `AjkLibs` | https://github.com/ajkfds/AjkLibs.git | 汎用ユーティリティライブラリ |
| `AvaloniaEdit` | git@github.com:ajkfds/AvaloniaEdit.git | テキストエディタコントロール |
| `ajkCefGlue` | git@github.com:ajkfds/ajkCefGlue.git | CEF (Chromium Embedded Framework) サポート |

### コミット時の注意

**サブモジュール内のファイルを変更した場合**:
```bash
git -C <サブモジュール名>/<サブモジュール名> add <変更ファイルのパス>
git -C <サブモジュール名>/<サブモジュール名> commit -m "<メッセージ>"
```

**メインディレクトリのファイルを変更した場合**:
```bash
git add <パス>
git commit -m "<メッセージ>"
```

## build方法

ワーニングが大量にあるので、build時にはerrorのみ参照してください。

```
dotnet build -clp:ErrorsOnly
```

全体build
```
dotnet build "RtlEditor2.Desktop.csproj" -clp:ErrorsOnly
```

plugin単位のbuild
```xml
<execute_command>
<command>dotnet build CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin.sln -clp:ErrorsOnly</command>
</execute_command>
```

---

## Git Commit手順 (重要)

このプロジェクトは**サブモジュール構造**を使用している。コミット前にパスを確認すること。

### 変更のあるファイルの場所を特定

```xml
<execute_command>
<command>git status</command>
</execute_command>
```

### 出力例:
```
modified:   CodeEditor2VerilogPlugin (new commits, modified content)
```

`(new commits, modified content)`はサブモジュール内の変更を示す。

### サブモジュール内の変更をコミットする場合

**正しい方法** (`git -C` を使用):
```xml
<execute_command>
<command>git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin add <変更ファイルのパス> && git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin commit -m "コミットメッセージ"</command>
</execute_command>
```

**例**: PortInvertSnippet.cs をコミットする場合:
```xml
<execute_command>
<command>git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin add CodeEditor2VerilogPlugin/Verilog/Snippets/PortInvertSnippet.cs && git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin commit -m "Fix PortInvertSnippet..."</command>
</execute_command>
```

### よくある間違い

❌ `git add CodeEditor2VerilogPlugin/...` - メインディレクトリからサブモジュールのファイルへアクセスできない
❌ `cd CodeEditor2VerilogPlugin && git add ...` - `cd` コマンドは許可されていない
❌ `git -C CodeEditor2VerilogPlugin add CodeEditor2VerilogPlugin/...` - パスが二重になる

✅ `git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin add CodeEditor2VerilogPlugin/...`

### サブモジュールの構造

```
RtlEditor2/                          # メインディレクトリ (git add/commitはここに)
└── CodeEditor2VerilogPlugin/        # サブモジュール
    └── CodeEditor2VerilogPlugin/    # 実際のコード (git -C で这里を指す)
        └── CodeEditor2VerilogPlugin/
            └── Verilog/
                └── Snippets/
                    └── PortInvertSnippet.cs
```

### ワークフロー

1. **ビルド**: エラーがないか確認
2. **git status**: 変更のあるファイルを確認
3. **サブモジュール内のファイルを変更した場合**:
   - `git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin add <パス>`
   - `git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin commit -m "<メッセージ>"`
4. **メインディレクトリのファイルを変更した場合**:
   - `git add <パス>`
   - `git commit -m "<メッセージ>"`


---

## 調査記録: triregとtranif対応状況 (2025-02-14)

### 概要
Verilogの`trireg`宣言と`tranif*`gate instantiationへの対応状況を調査。

### 調査結果

#### trireg宣言への対応 ✅ 対応済み

| 項目 | 場所 | 対応状況 |
|------|------|----------|
| `trireg`キーワード登録 | `General.cs:164,266` | ✅ 対応済み |
| `NetTypeEnum.Trireg` | `Net.cs:62` | ✅ 対応済み |
| `parseNetType()`でのパース | `Net.cs:208-211` | ✅ 対応済み |
| `DriveStrength`対応 | `Net.cs` + `Strength.cs` | ✅ 対応済み |
| `ChargeStrength`対応 | `Net.cs:326` + `Strength.cs:64-126` | ✅ 対応済み |

```verilog
// trireg宣言の例 - 対応済み
trireg (strong0) data;           // drive_strength
trireg (small) memory;            // charge_strength
trireg (pull1) #1 bus;           // drive_strength + delay
```

**備考**: `trireg`は`Net.ParseDeclaration()`で正しくパースされる。デフォルトネットタイプとしての`trireg`使用も`WordScanner.cs:649-651`で認識されている（ただし"Not supported"エラーは出る）。

#### tranif*/tran gate instantiationへの対応 ❌ 未実装

| 項目 | 場所 | 対応状況 |
|------|------|----------|
| `tranif0`, `tranif1` | `ModuleOrGenerateItem.cs:42-43` | ⚠️ キーワード登録済み |
| `rtranif0`, `rtranif1` | `ModuleOrGenerateItem.cs:44-45` | ⚠️ キーワード登録済み |
| `tran`, `rtran` | `ModuleOrGenerateItem.cs:46-47` | ⚠️ キーワード登録済み |
| `PassEnableSwitch`クラス | `GateInstantiation.cs` | ❌ **未実装** |
| `PassSwitch`クラス | `GateInstantiation.cs` | ❌ **未実装** |

**問題点**: `GateInstantiation.cs:126-132`で`tranif*`と`tran*`が`break;`のみで処理されており、実際のインスタンス生成が行われていない。

```csharp
// GateInstantiation.cs の問題箇所
case "tranif0":
case "tranif1":
case "rtranif0":
case "rtranif1":
    break;  // ← 何も処理されない
case "tran":
case "rtran":
    break;  // ← 何も処理されない
```

**対応が必要な構文**:
```verilog
// pass_en_switchtype
tranif0 inst (inout_terminal, inout_terminal, enable_terminal);
tranif1 inst (inout_terminal, inout_terminal, enable_terminal);
rtranif0 inst (inout_terminal, inout_terminal, enable_terminal);
rtranif1 inst (inout_terminal, inout_terminal, enable_terminal);

// pass_switchtype
tran inst (inout_terminal, inout_terminal);
rtran inst (inout_terminal, inout_terminal);
```

### 関連ファイル

| ファイル | 役割 |
|----------|------|
| `Verilog/ModuleItems/GateInstantiation.cs` | gate instantiation解析 |
| `Verilog/DataObjects/Nets/Net.cs` | net宣言解析 |
| `Verilog/Strength.cs` | drive/charge strength解析 |

### 修正優先度

| 優先度 | 問題 | 難易度 | 影響範囲 |
|--------|------|--------|----------|
| 低 | tranif*/tran gate instantiation | 中 | 小（gate instanceのみ） |

### 備考
- triregはnet_typeとして完全にサポート済み
- tranif*/tranはプリミティブゲートインスタンシエーションの一部で、未対応

---

## 修正履歴: let宣言のFunctionCall対応 (2025-02-14)

**問題**: `let`宣言をパースして`NamedElements`に登録しているが、`FunctionCall`が`let`を見つけられず`undefined`エラーになっていた

**原因**: `FunctionCall.ParseCreate()`が`Function`型のみを探しており、`LetDeclaration`型のチェックがなかった

**修正内容**:
1. `FunctionCall`クラスに`LetDeclaration`プロパティを追加
2. `FunctionCall.ParseCreate()`を修正して`LetDeclaration`も検索・認識するように変更
3. `BitWidth`設定時に`LetDeclaration.Expression.BitWidth`も考慮

**対応する構文**:
```verilog
// let宣言
let op(x, y) = x | y;

// let呼び出し（function_callとしてパース）
result = op(a, b);
```

**修正ファイル**:
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/Expressions/FunctionCall.cs`

**備考**:
- `LetDeclaration`は`INamedElement`として`NamedElements`に登録済み
- 既存の`PackageOrGenerateItemDeclaration.cs`と`Checker.cs`での`let`登録処理は正常

---

## 修正履歴: tranif*/tran gate instantiation実装 (2025-02-14)

**問題**: `tranif0`, `tranif1`, `rtranif0`, `rtranif1`, `tran`, `rtran`のgate instantiationがパースされていなかった

**原因**: `GateInstantiation.cs`の`ParseCreate`メソッドで該当caseが`break;`のみで実装されていなかった

**修正内容**:
1. `PassSwitch`クラスを追加（`tran`/`rtran`用）
2. `PassEnableSwitch`クラスを追加（`tranif0`/`tranif1`/`rtranif0`/`rtranif1`用）
3. 各クラスの`ParseCreate`メソッドで正しいport parsingを実装

**対応構文**:
```verilog
// Pass switch
tran inst (data, bus);
rtran inst (a, b);

// Pass enable switch
tranif0 enable_switch (data, bus, enable);
tranif1 enable_switch (data, bus, enable);
rtranif0 enable_switch (data, bus, enable);
rtranif1 enable_switch (data, bus, enable);

// With delay
tranif0 #1ns enable_switch (data, bus, enable);
```

**修正ファイル**:
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/ModuleItems/GateInstantiation.cs`

**備考**:
- `PassSwitch.IsReversible`プロパティで`tran`/`rtran`を区別
- `PassEnableSwitch.IsInverting`と`IsReversible`プロパティで各タイプを区別
- `Delay2`サポート済み

---

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

### stream concat (bug 11.4.14) ✅ 既に実装済み

	c = {>> 8 {a, b}};

`StreamingConcatenation`クラスと`ParseCreate`メソッドが既に実装済み
### case判定の一部未対応 (bug 12.5.4) ✅ 修正済み
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
[5:6]の位置でエラーが出ていたが修正済み (2025-02-14)

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

## clocking event (bug 14.3) ✅ 修正済み

```
default clocking @(posedge clk);
	default input #10ns output #5ns;
endclocking

```
posedge 箇所でエラーが出ていたが修正済み (2025-02-14)


---

## 調査記録: UIスレッドロック問題

### 問題概要
パース中にBackground Task間のsemaphore競合により、パース処理全体が遅延する。
**※注意**: UIスレッド自体はブロックされません。問題は処理遅延です。

### 主要因 (調査済み: 2025-02-12 更新)

| # | 原因 | 場所 | 重要度 | 備考 |
|---|------|------|--------|------|
| 1 | **Background Task間のsemaphore競合** | `Updater.cs:71-98` | **高** | 複数のワーカーがsemaphoreを奪い合う |
| 2 | `AcceptParsedDocumentAsync`内での`UpdateAsync`呼び出しチェーン | `VerilogFile.cs:180`, `VerilogModuleInstance.cs` | 高 | パース完了ごとに呼び出し |
| 3 | `VerilogHeaderInstance.UpdateAsync`での親ファイルへのUpdate伝搬 | `VerilogHeaderInstance.cs:336-368` | 高 | 不要な処理が発生 |
| 4 | Includeファイルの再帰的処理 | `VerilogFile.updateIncludeFilesAsync` | 中 | 深くネストすると遅延増大 |

### 呼び出しフロー (現在)

```
ParseHierarchy.runParallel() [Background Task - 複数ワーカー並列実行]
    ↓
各Worker: parseTextFile() → parser.ParseAsync()
    ↓
await verilogFile.AcceptParsedDocumentAsync(parser) [Background]
    ├→ ParsedDocument設定
    ├→ await VerilogFile.updateIncludeFilesAsync() [再帰処理]
    └→ await UpdateAsync()
              ↓
        VerilogFile.UpdateAsync()
              ├→ await base.UpdateAsync()
              └→ await VerilogCommon.Updater.UpdateAsync()
                    ↓
                    // UI thread check → false (Background Taskなので)
                    // ↓ そのまま処理続行
                    await _semaphore.WaitAsync() ←── ★ Worker Aがsemaphore保持中
                          ↓
                          // → Worker B, C, D...がここでブロック!
                    Items更新処理
```

### semaphore競合の具体例

```
Worker A: UpdateAsync() → semaphore取得成功 → Items更新中...
Worker B: UpdateAsync() → semaphore.WaitAsync()で待機... ★ ブロック
Worker C: UpdateAsync() → semaphore.WaitAsync()で待機... ★ ブロック
Worker D: UpdateAsync() → semaphore.WaitAsync()で待機... ★ ブロック

Worker A: Items更新完了 → semaphore解放
Worker B: semaphore取得成功 → Items更新開始...
(Worker C, Dは引き続き待機)
```

### 既知の修正済み箇所 ✅

| 箇所 | 修正内容 |
|------|----------|
| `Updater.UpdateAsync` | UI thread check追加 (line 22-30): UIスレッドから呼ばれたらTask.Runにディスパッチ |
| `TextFile.UpdateAsync` | `InvokeAsync()` → `Post()` (line 507) |

### 備考
- **UIスレッドはブロックされない** - 既に`Updater.UpdateAsync`で対策済み
- 問題は **Background Task間のsemaphore競合** による処理遅延
- `ParseHierarchy.runParallel()` は `Environment.ProcessorCount` 個のワーカーを並列実行
- ワーカー数が多いほどsemaphore競合が激しくなる

---

---

## 調査記録: includeファイル編集時の処理遅延問題

### 問題概要
includeファイルを編集したときに、パース処理全体が遅延する。
**※注意**: UIスレッドはブロックされません。問題は処理遅延です。

### 呼び出しフロー

```
編集操作 → ParseHierarchy.PostParseAsync() [UI Thread - Post only]
    ↓
ProcessQueueAsync() → ParseInternalAsync() → runParallel() [Background Task]
    ↓
Worker: parseTextFile() → parser.ParseAsync()
    ↓
VerilogHeaderInstance.AcceptParsedDocumentAsync()
    ├→ textFile.CodeDocument?.CopyColorMarkFrom()
    ├→ Controller.CodeEditor.PostRefresh()
    └→ await UpdateAsync()
              ↓
        VerilogHeaderInstance.UpdateAsync()
              ├→ await base.UpdateAsync() [TextFile.UpdateAsync]
              │     └→ Post() で UI 更新をスケジュール ★ 修正済み
              │
              └→ // 親ファイル階層を辿る
              if (parentItem is VerilogFile)
              {
                  await VerilogCommon.Updater.UpdateAsync(vFile, ...)
                        ↓
                        // Background Taskから呼ばれるのでsemaphore競合発生
                        await _semaphore.WaitAsync() ★ ブロック!
                        Items更新
              }
```

### 主要因 (2025-02-12 更新)

| # | 原因 | 場所 | 重要度 | 備考 |
|---|------|------|--------|------|
| 1 | **Background Task間のsemaphore競合** | `Updater.cs:71-98` | **高** | 複数のワーカーがsemaphoreを奪い合う |
| 2 | **VerilogHeaderInstance.UpdateAsyncでの親ファイルUpdate** | `VerilogHeaderInstance.cs:336-368` | 高 | 不要な処理も実行 |
| 3 | **Includeファイルのネスト処理** | `VerilogFile.updateIncludeFilesAsync` | 中 | 再帰呼び出しで処理増大 |

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

## 調査記録: NavigatePanel ノードクリック時の処理遅延問題

### 問題概要
NavigatePanel でノードをクリックした場合に、大規模プロジェクトや深い階層構造を持つファイルでパース処理全体が遅延する。
**※注意**: UIスレッドはブロックされません。問題は処理遅延です。

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
              ├→ await UpdateAsync() [Background Task]
              │     └→ await Updater.UpdateAsync()
              │           └→ await _semaphore.WaitAsync() ★ ここでブロック!
              │     └→ VerilogCommon.Updater.UpdateAsync()
              │           └→ item.Items のアトミック更新
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

### 主要因 (2025-02-12 更新)

| # | 原因 | 場所 | 重要度 | 備考 |
|---|------|------|--------|------|
| 1 | **AcceptParsedDocumentAsync内でのUpdateAsync呼び出し** | `VerilogFile.cs:180`, `VerilogModuleInstance.cs` | **高** | 修正が必要 |
| 2 | **Updater.UpdateAsync内のsemaphore待機によるブロック** | `Updater.cs:71-98` | 高 | semaphore競合がBackground Taskをブロック |
| 3 | **UpdateVisualの呼び出しチェーン** | 各NodeクラスのUpdateSubNodes() | 中 | 深くネストすると遅延増大 |
| 4 | **InstanceTextFileでのソースファイルへのAcceptParsedDocumentAsync伝搬** | `VerilogModuleInstance.cs:260-264` | 中 | 単一モジュールファイルのみ |

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
- [x] **Agent session active - Ready for user instructions** (2025-02-14)
- **Last AGENTS.md check**: 2025-02-14
- **AGENTS.md loaded**: Ready to process user instructions

### 修正履歴

#### PortInvertSnippet.cs修正 (2025-02-14)

**問題**: port inversion snippetがuser-defined types、カンマ区切り、行内コメントに対応していなかった

**修正内容**:
1. 正規表現に`type`キャプチャグループを追加（任意の型名をサポート）
2. bitwidth正規表現パターンを修正: `\[[^\[\]]*\]`（ネストBracket処理の修正）
3. 行末カンマのサポートを追加（それまではセミコロンのみ対応）
4. 行内コメント`//...`のサポートを追加

**対応可能となったケース**:
```verilog
input [7:0] data;              // 通常
input [7:0] data,              // カンマ区切り
input logic [7:0] data;        // user-defined type (logic)
input my_type_t addr,          // カスタム型 + カンマ
input [7:0] data; // comment    // 行内コメント
```

**修正ファイル**:
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/Snippets/PortInvertSnippet.cs`

### Agent Session Log
- **2025-02-14**: AGENTS.md read and understood. Project structure grasped.
- **2025-02-14**: Agent session restarted, AGENTS.md re-read and ready for next task.
  - Main focus areas: Verilog/SystemVerilog parser, TreeControl issues, UI thread locks
  - Known bugs: 11+ SystemVerilog parse errors, UI thread lock issues
  - Fixed bugs: TreeControl flickering, ChatControl font, ParseHierarchy cancel, case inside, clocking event
  - Build command: `dotnet build "RtlEditor2.Desktop.csproj" -clp:ErrorsOnly`

- **Agent re-initialized**: AGENTS.md read and ready for next task (2025-02-14)
  - Project: RtlEditor2 (Avalonia UI RTL IDE)
  - Key components: CodeEditor2, CodeEditor2VerilogPlugin, various integration plugins
  - Build: `dotnet build "RtlEditor2.Desktop.csproj" -clp:ErrorsOnly`

### Agent Session Log
- **2025-02-14**: AGENTS.md read and understood. Project structure grasped.
- **2025-02-14**: Agent session restarted, AGENTS.md re-read and ready for next task.
  - Main focus areas: Verilog/SystemVerilog parser, TreeControl issues, UI thread locks
  - Known bugs: 11+ SystemVerilog parse errors, UI thread lock issues
  - Fixed bugs: TreeControl flickering, ChatControl font, ParseHierarchy cancel, case inside, clocking event
  - Build command: `dotnet build "RtlEditor2.Desktop.csproj" -clp:ErrorsOnly`

### Current Session Info
- **Session Started**: 2025-02-12
- **Last Updated**: 2025-02-14
- **Project**: RtlEditor2 (Avalonia UI RTL IDE)
- **Language**: C# (.NET 8), Verilog/SystemVerilog parser
- **Known Issues**: UI thread locks, Parse errors, TreeControl issues
- **Build Command**: `dotnet build "RtlEditor2.Desktop.csproj" -clp:ErrorsOnly`

---

## Latest Agent Session
- **Last Updated**: 2025-02-14 - Agent initialized successfully

- **Current focus**: Waiting for user instructions
- **Project**: RtlEditor2 - Avalonia UI based RTL IDE
- **Notable topics**: UI thread lock issues, Verilog/SystemVerilog parsing, TreeControl, NavigatePanel
- **Session Summary**: 
  - Project structure: Avalonia UI RTL IDE with modular plugin architecture
  - Main issue categories: UI thread locks, SystemVerilog parse errors (11+ known bugs), TreeControl issues
  - Fixed issues: TreeControl flickering, ChatControl font, ParseHierarchy cancel, ShowDialog position, Struct undriven warning
- **Agent initialized**: 2025-02-14 - AGENTS.md loaded, ready to process tasks

**UIスレッドロック問題 確認結果 (2025-02-12)**:
- `TextFile.UpdateAsync`: `InvokeAsync()` → `Post()` に変更済み ✅
- `Updater.UpdateAsync`: UI thread check追加済み (Task.Runにディスパッチ) ✅
- `VerilogHeaderInstance.UpdateAsync`: 親ファイルへのUpdate伝搬がまだ存在する ❌
- `AcceptParsedDocumentAsync`: `UpdateAsync`呼び出しチェーンがまだ存在する ❌
- `semaphore競合`: Background Task間の競合がまだ発生する ❌

---

## 修正履歴: ParseHierarchy - ノードクリック時のParse停止制御 (2025-02-12)

**問題**: ノードクリック時にbackgroundで階層parseが走るが、別のノードをクリックしたときに前のparseを停止させたかった。ForceAllFilesでのparseだけは停止したくない。

**修正内容**:
1. `_currentParseMode`変数を追加し、実行中のパースモードを追跡
2. `CancelCurrentParseIfNotForceAll()`メソッドを追加し、ForceAllFiles以外のパースのみをキャンセル可能に
3. `ProcessQueueAsync()`を修正:
   - ForceAllFiles実行中にSearchReparseRequestedTreeが来た場合 → リクエストをキューに戻して終了（ForceAllFiles完了後に処理）
   - SearchReparseRequestedTree実行中にForceAllFilesが来た場合 → 実行中パースをキャンセル、キューをクリアしてForceAllFilesを処理
   - SearchReparseRequestedTree実行中に別のSearchReparseRequestedTreeが来た場合 → 実行中パースをキャンセル、新しい方を処理

**修正ファイル**:
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Tool/ParseHierarchy.cs`

**動作確認**:
| シナリオ | 動作 |
|----------|------|
| SearchReparseRequestedTree実行中に別のノードクリック | 実行中をキャンセル、新しい方を処理 |
| SearchReparseRequestedTree実行中にForceAllFiles開始 | 実行中をキャンセル、キューをクリアしてForceAllFilesを処理 |
| ForceAllFiles実行中に別のノードクリック | ForceAllFiles完了後にキュー内のリクエストを処理 |
| ForceAllFiles実行中にForceAllFiles開始 | 最初のForceAllFilesを続行（キューに溜まる） |

---

## 修正履歴: Controller.ShowDialog (2025-02-12)

**問題**: Linux+X11環境下で`ShowDialog(mainWindow)`を呼び出すと、ダイアログがmainWindowとは全く異なる場所に表示される。

**修正内容**:
- `Controller.cs`に`ShowDialog(Window dialog)`メソッドを追加
- ダイアログをmainWindowの中央に配置してから`ShowDialog()`を呼び出す
- `dialog.Position`を設定することで位置問題を回避

**修正ファイル**:
- `CodeEditor2/CodeEditor2/CodeEditor2/Controller.cs`

**使用方法**:
```csharp
await Controller.ShowDialog(dialogWindow);
```

**備考**:
- `ShowDialog()`呼び出し前に`Position`を設定することで、Linux+X11環境でも正常動作

---

## 修正履歴: SystemVerilog Parse Bug修正 (2025-02-14)

### case判定の一部未対応 (bug 12.5.4) ✅ 修正済み

**問題**: `case(a) inside [..., [5:6]: b = 2;]` で `[5:6]` の位置でエラー

**原因**: `ParseOpenRangeList`メソッド内で`[5:6]`をパースする際、`word.Text == "["`を検出した後の処理が不正だった

**修正ファイル**:
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/Statements/CaseStatement.cs`
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/Expressions/RangeExpression.cs`

**修正内容**:
1. `ParseOpenRangeList`を修正して`[5:6]`のような範囲式を正しくパース
2. `RangeExpression`クラスを`Expression`のサブクラスに変更して型互換性を解決

### clocking event (bug 14.3) ✅ 修正済み

**問題**: `default clocking @(posedge clk);` で`posedge`箇所でエラー

**原因**: `Clocking.ParseCreate`で`@(posedge clk)`のパース時に`posedge`がキーワードとして認識されていた

**修正ファイル**:
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/BuildingBlocks/Clocking.cs`

**修正内容**:
- `word.Text == "posedge"`などのエッジキーワードをチェックしてから式をパースするように修正

### stream concat (bug 11.4.14) ✅ 既に実装済み

`StreamingConcatenation`クラスと`ParseCreate`メソッドが既に実装済み

---

## 修正履歴: Struct変数のundrivenワーニング問題 (2025-02-14)

**問題**: `typedef struct packed` で定義した型を使用して変数を作成し、そのメンバーにassignすると、変数定義箇所で `"undriven"` ワーニングが出る。

**原因**: `Struct.Create` で `DataType` に元の `IDataType` (多くの場合 `UserDefinedType`) を設定していたため、`DataObjectReference.AssertAssigned()` での `StructParentObject.DataType is StructType` チェックが失敗し、Structメンバーのビット位置計算が行われていなかった。

```verilog
module TEST_RTL();
    typedef struct packed{
        logic [7:0] AA;
        logic [7:0] BB;
    } STRUCT_BUS;
    STRUCT_BUS aaa;  // ここでundrivenワーニング
    assign aaa.AA = 0;
    assign aaa.BB = 0;
endmodule
```

**問題の流れ**:
1. `DataTypeFactory` が `STRUCT_BUS` を `UserDefinedType` として返す（`Typedef` をラップ）
2. `Struct.Create()` で `DataType = dataType` により `UserDefinedType` が設定される
3. `AssertAssigned()` で `StructParentObject.DataType is StructType` チェックが `false` を返す
4. Structメンバーのビット位置計算がスキップされ、`AssignedMap` が更新されない
5. `IsFullMapped()` が `false` を返し、undrivenワーニングが発生

**修正**: `Struct.Create` で `DataType` に underlying の `StructType` を設定

```csharp
// Struct.cs - 修正後
public static new Struct Create(string name, IDataType dataType)
{
    StructType structType = (StructType)dataType;

    // Set DataType to the underlying StructType so that checks like
    // "DataType is StructType" work correctly when the variable is
    // declared with a typedef (UserDefinedType wrapping StructType)
    Struct ret = new Struct() { StructType = structType, Name = name, DataType = structType };
    return ret;
}
```

**修正ファイル**:
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/DataObjects/Variables/Struct.cs`

**備考**:
- `DataType` を `StructType` に設定することで、`AssertAssigned()` での型チェックが正しく動作する
- 既存の `Struct.BitWidth` プロパティは `StructType.BitWidth` を正しく返すため、変更後も正常に動作

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

## ChatControl 日本語フォント問題 (修正済み: 2025-01-09)

**問題**: ChatControlで日本語表示おかしくなる（中国フォントがデフォルトになっている）

**原因**: フォント指定がないため、システムデフォルト（中国フォント）が使用される

**修正**:
1. `ChatControl.axaml` に `FontFamily="Yu Gothic UI, Meiryo, MS Gothic, sans-serif"` を追加
2. `InputItem.cs` の `TextBox` に同上 FontFamily を設定
3. `LiveMarkdownControl.axaml` に同上 FontFamily を設定

**修正ファイル**:
- `CodeEditor2/CodeEditor2/CodeEditor2/LLM/ChatControl.axaml`
- `CodeEditor2/CodeEditor2/CodeEditor2/LLM/InputItem.cs`
- `AjkAvaloniaLibs/AjkAvaloniaLibs/Controls/LiveMarkdownControl.axaml`

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

## 修正履歴: MeaiStatefulChatAgent.cs (2025-02-12)

**問題**: Microsoft.Extensions.AI APIの変更によりコンパイルエラー

**エラー内容**:
1. `ChatResponse.Message` が存在しない（API変更）
2. `InvokeAsync` の引数に `IDictionary<string, object?>` を渡せない（`AIFunctionArguments`が必要）

**修正ファイル**:
- `CodeEditor2AiPlugin/CodeEditor2AiPlugin/MeaiStatefulChatAgent.cs`

**備考**:
- `OpenRouterChat.cs`の`ToChatResponse()`拡張メソッドを参考に修正
- `IStatefulChatAgent`インターフェースとの互換性を維持

---

## 調査記録: VerilogModuleInstanceノードのちらつき問題 ✅ 修正済み

### 問題概要
VerilogFile-ModuleInstance-ModuleInstanceの孫ノードがTreeControl上で見えたり見えたりするチカチカ現象が発生。（**2025-01-24 修正確認済み**）

### 呼び出しフロー分析（参考）

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
- **2025-01-24 修正確認済み**
- TreeControlのちらつき問題は解消されています

---

## 調査記録: ノードちらつき問題 - 詳細分析（追加） ✅ 修正済み

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

## 調査記録: HierarchyConnection - concatenate信号のdrive判定問題

### 問題概要
信号をassignしたときにdrive状態を確認しているが、moduleの出力ピンにconcatenateした信号を直接書いたときに正しくdrive判定されていない。

### 具体例

```verilog
// parent module
module parent (
    output [3:0] out
);
    child u_child (
        .out({a, b})    // ★ Concatenationがport connection
    );
endmodule

// child module  
module child (
    output [3:0] out
);
    assign out = {x, y};
endmodule
```

この場合、`a`や`b`のdrive判定が正しく行われていない。

### 呼び出しフロー

```
HierarchyConnection.ConnectionNode() [初期化時]
    ↓
searchModulePort() [port connectionを処理]
    ↓
if (connextionExpression is DataObjectReference)  // ★ Concatenationは除外される
    ├→ connectionDataObject.AssignedReferences を辿る
    └→ foreach ループの中が空 → Driverリストに追加されない
else
    ★ 何も処理されない
```

### 主要因 (調査済み: 2025-02-12)

| # | 原因 | 場所 | 重要度 |
|---|------|------|--------|
| 1 | **Concatenation対応なし** | `HierarchyConnection.cs:59-72` | **高** |
| 2 | **searchModulePort内のforeachループが空** | `HierarchyConnection.cs:66-68` | **高** |
| 3 | **Output portの処理が空** | `HierarchyConnection.cs:73-75` | **高** |
| 4 | **Driver/Receiverリストへの登録処理なし** | `ConnectionNode`クラス全体 | 高 |

### 詳細分析

#### 1. Concatenation対応なし (高)

```csharp
// HierarchyConnection.cs - searchModulePort()
private void searchModulePort(Data.VerilogModuleInstance moduleInstance, DataObject dataObject)
{
    // ...
    if (!moduleInstantiation.PortConnection.ContainsKey(port.Name)) return;
    Expressions.Expression connextionExpression = moduleInstantiation.PortConnection[port.Name];

    if (port.Direction == Port.DirectionEnum.Input)
    {
        if (connextionExpression is DataObjectReference)  // ★ Concatenationはここで弾かれる
        {
            DataObjectReference variableReference = (DataObjectReference)connextionExpression;
            DataObject? connectionDataObject = variableReference.TargetDataObject;
            if (connectionDataObject is null) return;
            foreach (var assign in connectionDataObject.AssignedReferences)
            {
                // ★ ループの中が空！Driverに追加する処理がない
            }
        }
    }
    else if (port.Direction == Port.DirectionEnum.Output)
    {
        // ★ このブランチが空！
    }
}
```

**問題点**: `connextionExpression is DataObjectReference`のチェックで、Concatenation expressionが処理されない。

#### 2. foreachループが空 (高)

```csharp
foreach (var assign in connectionDataObject.AssignedReferences)
{
    // ★ 何もしていない
    // 本来は Driver.Add() でConnectionNodeを追加すべき
}
```

#### 3. Output port処理が空 (高)

```csharp
else if (port.Direction == Port.DirectionEnum.Output)
{
    // ★ 何もしていない
    // Output portのdriver/ receiver処理が必要
}
```

### 修正方針

1. **Concatenation対応**: `searchModulePort`で`connextionExpression is Concatenation`のケースも処理
   - Concatenationの`Expressions`プロパティを辿って各要素について処理
   - 各Expressionについて再帰的にdriver情報を取得

2. **foreachループの実装**: `connectionDataObject.AssignedReferences`の各参照について
   - 対応する`ConnectionNode`を生成して`Driver`リストに追加
   - 参照元のsignalへのconnectionを再帰的に追跡

3. **Output port対応**: Output portの場合
   - 子のmoduleが出力している信号(source)を取得
   - 子のsource信号がparentのどのsignalに接続されているか追跡

### 関連ファイル

| ファイル | 役割 |
|----------|------|
| `Verilog/HierarchyConnection.cs` | 階層間接続追跡のメインクラス |
| `Verilog/Expressions/Concatenation.cs` | Concatenation式の表現 |
| `Verilog/DataObjects/Arrays/ArraysBoolMap.cs` | assign時のdriverビットマップ管理 |
| `Verilog/DataObjects/DataObject.cs` | `AssignedReferences`の管理 |

### 備考
- 調査のみの実施。修正は未実施。
- assign時のdriver登録は`ContinuousAssign`→`VariableAssignment`→`NetLValue.AssertAssigned()`で既に実装済み
- 問題は`HierarchyConnection`でのport connection処理時にconcatenationを処理していない点

---

## 修正履歴: Port.BitWidth の二重計算問題 (2025-02-14)

**問題**: module instanceのport接続でbitwidth warningの出力がおかしい
- `input [7:0] DATA` に対して `wire [7:0] dat` を接続したとき
- `.DATA(dat)` で `bitwidth 64<-8` というwarningが出る（本来は `bitwidth 8<-8`）

**原因**: `Port.BitWidth` プロパティが以下のように計算されていた
1. `DataObject.BitWidth` (Net.BitWidth) = 8 を取得
2. `PackedDimensions` (Netのpacked dimensions) をループしてさらに乗算
3. 結果: 8 × 8 = 64

しかし `Net.BitWidth` は既に `DataType.BitWidth` (packed dimensionsを含む) を返しているため、
`PackedDimensions` を再加算すると二重計算になっていた。

**修正**: `Port.BitWidth` から PackedDimensions の計算を除外
- DataObject.BitWidth は Net の PackedDimensions を既に 含んでいるため、二重計算不要再
- UnPackedDimensions のみを追加で乗算（これは DataObject.BitWidth に含まれないため）

**修正ファイル**:
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/DataObjects/Port.cs`

**備考**:
- warning文は `ModuleInstantiation.cs` の `checkNetPortConnection()` と `checkVariablePortConnection()` で出力される
- 修正により正しいビット幅比較が行われるようになった

---

## 調査記録: SystemVerilog Parse Error 修正方針

### 前提
AGENTS.mdの「SystemVerilog Parse Errorリスト」に記載のエラーについて、修正方針を立案する。

---

### 1. module instance .* parse error (bug 10.6.2)

---

### 1. module instance .* parse error (bug 10.6.2)

**問題**: `flop u_flop (.*);` で `.*` がパースエラーになる

**原因**: `.*` はport接続において1つのwordとして認識されるが、パーサーが 이를 处理하지 않음

**対象ファイル**: `Verilog/ModuleItems/ModuleInstantiation.cs` または related

**修正方針**:
```verilog
// 対応する構文
module_instantiation ::= module_identifier [ parameter_value_assignment ] hierarchical_instance ;
hierarchical_instance ::= instance_name ( [ ordered_port_connection ] { , [ ordered_port_connection ] } )
ordered_port_connection ::= { attribute_instance } [ expression ]
interface_port_connection ::= { attribute_instance } .port_identifier ( [ expression ] )
                           | { attribute_instance } .* ( )
```

1. `ModuleInstantiation.cs` の port connection parsing を確認
2. `.*` を special token として認識하도록修正
3. テストケース: `flop u_flop (.*);`

---

### 2. let constructのparse error (bug 11.12)

**問題**: `let op(x, y, z) = |((x | y) & z);` でパースエラー

**調査済み**: `Verilog/DataObjects/LetDeclaration.cs` にパーサーは既に実装済み

**原因**: `Checker.cs` や `PackageOrGenerateItemDeclaration.cs` で `case "let"` を見ているが、module内のtop-levelで碰到了場合に обработка 되지 않음

**対象ファイル**:
- `Verilog/BuildingBlocks/Module.cs` - module item parsing
- `Verilog/Items/ModuleItem.cs` - module item dispatcher

**修正方針**:
1. module/item parsing で `let` をまだ対応していないか確認
2. module の `NonPortModuleItem` で `let` declaration を处理しているか確認
3. 必要に応じて `Items/ModuleItem.cs` に `let` case を追加

---

### 3. typedef union未対応 (bug 11.14)

**問題**: `typedef union {...} name_t;` がパースエラー

**対象ファイル**: `Verilog/DataObjects/DataTypeFactory.cs` または related

**修正方針**:
1. `data_type` parsing で `typedef` を检测
2. `typedef` 後の型 gener を parsing:
   ```verilog
   typedef union [{ ... }] type_identifier [ variable_dimension ] ;
   ```
3. `DataTypes/` ディレクトリに `TypedefUnion` クラスを作成することを検討

---

### 4. string arrayのforeachインデックスparseエラー (bug 12.7.3)

**問題**:
```verilog
string test [4] = '{"111", "222", "333", "444"};
initial begin
    foreach(test[i])
        $display(i, test[i]);
end
```

**原因**: `ForeachStatement.cs` の parsing logic が string array などの特殊型を対応していない

**対象ファイル**: `Verilog/Statements/LoopingStatememt.cs` の `ForeachStatement`

**修正方針**:
1. `ForeachStatement.ParseCreate` を修正して array identifier の parsing を改善
2. string type のような特殊型の array も対応
3. index variable が array の次元より多い場合の处理を追加

---

### 5. stream concat 未対応(bug 11.4.14)

**問題**: `c = {>> 8 {a, b}};` でパースエラー

**調査済み**: `Verilog/Expressions/Concatenation.cs` に `StreamingConcatenation` クラスは既に存在

**原因**: `MultipleConcatenation.ParseCreate` が streaming concat を正しく处理していない

**対象ファイル**: `Verilog/Expressions/Concatenation.cs`

**現在のコード** (行 66-76):
```csharp
// Check for streaming concatenation: { >> or << ... }
if (word.Text == ">>" || word.Text == "<<")
{
    return MultipleConcatenation.ParseCreate(word, nameSpace, exp1, reference);
}
```

**修正方針**:
1. `exp1` が null の场合 (例: `{>> 8 {a, b}}` の `>> 8` 部分) の处理を追加
2. streaming concatenation を检测したら `StreamingConcatenation.ParseCreate` を呼叫
3. slice_size (例: `8`) の parsing を実装

---

### 6. case判定の一部未対応 (bug 12.5.4)

**問題**:
```verilog
case(a) inside
    1, 3: b = 1;
    4'b01??, [5:6]: b = 2;  // [5:6] の位置でエラー
    default b = 3;
endcase
```

**原因**: `CaseStatement.cs` の `CaseItem.ParseCreate` が `inside` mode の `open_range_list` を完全には対応していない

**対象ファイル**: `Verilog/Statements/CaseStatement.cs`

**現在の問題**:
- `CaseItem` class が `expression` のみを許可
- `inside` mode では `open_range_list` (範囲式を含む) が必要

**修正方針**:
1. `CaseItem` class に `IsInside` flag を追加
2. `inside` mode の场合、`open_range_list` を parsing:
   ```verilog
   case_inside_item ::= open_range_list : statement_or_null
                       | "default" [ : ] statement_or_null
   open_range_list ::= open_value_range { , open_value_range }
   open_value_range ::= value_range
   value_range ::= expression | expression : expression
   ```
3. `[5:6]` のような範囲式を許容するよう修正

---

### 7. named blocks (bug 9.3.5)

**問題**:
```verilog
name: begin
    a = 1;
    b = a;
    c = b;
end: name
```
`end:name` 位置でエラー

**対象ファイル**: `Verilog/Statements/SequentialBlock.cs`

**修正方針**:
1. `end` の後に `:` identifier pattern を許可
2. `BeginIndexReference` に `end:identifier` を登録
3. begin/end block name matching を実装

---

### 8. const function (bug 13.4.3)

**問題**: `localparam a = fun(3);` でパースエラー

**原因**: function call の定数評価が対応されていない

**対象ファイル**: `Verilog/Function.cs`

**修正方針**:
1. `Function` class に `IsConstant` flag を追加 (function が副作用-free の场合)
2. 定数畳込み (constant folding) で function call を評価
3. 定数 expression parsing での function call 处理を改善

---

### 9. associative arrayの参照エラー

**問題**:
```verilog
int arr [ int ];
$display(":assert: (%d == 0)", arr.size);
```
`arr` の位置でエラー

**原因**: `arr` が associative array として認識されていない

**対象ファイル**: `Verilog/DataObjects/Variables/Variable.cs`

**修正方針**:
1. Variable parsing で associative array declaration を正しく認識
2. associative array の场合、`size()` などの built-in method を登録
3. DataTypes で associative array type をサポート

---

### 10. dynamic arrayのシステムメソッド未対応

**問題**:
```verilog
bit [7:0] arr[];
arr = new [ 16 ];
$display(":assert: (%d == 16)", arr.size);
arr.delete;
```
`size`, `delete` 箇所でエラー

**原因**: dynamic array type の built-in methods が未対応

**対象ファイル**: `Verilog/DataObjects/DataTypes/`

**修正方針**:
1. `DynamicArray` type クラスを作成 또는 既存 class を拡張
2. `size()`, `delete()`, `new[]()` などの method を登録
3. array method call parsing を改善

---

### 11. clocking event (bug 14.3)

**問題**:
```verilog
default clocking @(posedge clk);
    default input #10ns output #5ns;
endclocking
```
`posedge` 箇所でエラー

**調査済み**: `Verilog/BuildingBlocks/Clocking.cs` に `ParseDefaultClocking` と `ParseCreate` が既に実装済み

**原因**: `Clocking` parsing が module/scoped item parsing に統合されていない

**対象ファイル**: `Verilog/BuildingBlocks/Module.cs`, `Verilog/Items/ModuleItem.cs`

**修正方針**:
1. module item parsing に `default clocking` を追加
2. `case "default"` を检测して `Clocking.ParseDefaultClocking` を呼叫
3. clocking block の parsing を module item として登録

---

### 12. パラメタライズドクラス未対応

**問題**:
```verilog
class par_cls #(int a = 25);
    parameter int b = 23;
endclass
```

**原因**: Class parsing で parameter が処理されていない

**対象ファイル**: `Verilog/BuildingBlocks/Class.cs`

**修正方針**:
1. `Module` class 同様、`Class` class に parameter port list parsing を追加
2. `parameter_override` を class instance に適用
3. parameterized class を building block registry に登録

---

### 修正優先度付け

| 優先度 | Bug ID | 問題 | 難易度 | 影響範囲 |
|--------|--------|------|--------|----------|
| 高 | 12.5.4 | case inside | 中 | 中 |
| 高 | 11.4.14 | stream concat | 中 | 小 |
| 高 | 14.3 | clocking event | 中 | 中 |
| 中 | 10.6.2 | module instance .* | 低 | 小 |
| 中 | 11.12 | let construct | 中 | 中 |
| 中 | 12.7.3 | foreach string array | 中 | 小 |
| 中 | 11.14 | typedef union | 高 | 中 |
| 中 | 9.3.5 | named blocks | 低 | 小 |
| 低 | 13.4.3 | const function | 高 | 中 |
| 低 | - | associative array | 高 | 中 |
| 低 | - | dynamic array methods | 高 | 中 |
| 低 | - | parameterized class | 高 | 中 |

---

### テスト方法

各修正後、以下のコマンドでビルドしてエラーがないことを確認:
```bash
dotnet build CodeEditor2VerilogPlugin\CodeEditor2VerilogPlugin\CodeEditor2VerilogPlugin.sln -clp:ErrorsOnly
```

また、各パーサー独立的テストを書くことを推奨。

---

## 調査記録: 現状の問題点まとめ（2025-01-24）

### 1. ビルドエラー（致命的）

**問題**: アプリ実行中にDLLファイルがロックされているためビルドが失敗する

**エラーメッセージ**:
```
ファイル "CodeEditor2AiPlugin.dll" を "bin\x64\Debug\net8.0\CodeEditor2AiPlugin.dll" にコピーできませんでした。
このファイルは "Microsoft Visual Studio (27592), RtlEditor2.Desktop (24544)" によってロックされています。
```

**影響**: 開発中にコードを変更しても即座にビルドして確認できない

**回避策**: ビルド前にRtlEditor2.Desktop.exeを終了する

---

### 2. throw new Exception() の濫用

**問題**: パース中に予期しない状況に出会った場合、`throw new Exception()`で例外をスローしている

**主な箇所**:
- `VerilogParser.cs`: nullチェックでの`throw new Exception()`
- `Updater.cs`: `throw new Exception()` が複数箇所
- `VerilogModuleInstance.cs`: `throw new Exception()` が複数箇所
- `Module.cs`, `Class.cs`, `Checker.cs` などパース処理全般

**問題点**:
1. デバッグ時に原因を特定しにくい（Exceptionに詳細なメッセージがない）
2. 予期しないパースパターンでアプリがクラッシュする可能性がある
3. SystemVerilogの新しい構文への対応が困難

**推奨される対応**:
```csharp
// 現在
if (textFile == null) throw new Exception();

// 推奨
if (textFile == null) throw new InvalidOperationException($"TextFile is null for {GetType().Name}");
```

---

### 3. System.Diagnostics.Debugger.Break() の残存

**問題**: デバッグ用のブレークポイントが残っている

**主な箇所**:
- `NameSpace.cs:164`: `if (space.Name == null) System.Diagnostics.Debugger.Break();`
- `ParsedDocument.cs:57`: `System.Diagnostics.Debugger.Break();`
- `Task.cs:106`: `System.Diagnostics.Debugger.Break();`
- `Module.cs`: `throw new Exception()` で置き換えられているが、条件文が不完全

**問題点**: リリースビルドで予期しないブレークが発生する可能性がある

---

### 4. 空のcatchブロック

**問題**: 例外を握り潰す空のcatchブロックが存在

**箇所**:
- `AlwaysFFSnippet.cs:153`: 空のcatch + Debugger.Break()
- `Number.cs:669`: 空のcatch

**問題点**: エラー処理が不完全で、問題の追跡が困難

---

### 5. AGENTS.mdに記載済みの既知の問題（未修正）

以下の問題はAGENTS.mdすることですで調査済みだが未修正:
- UIスレッドロック問題（複数存在）
- NavigatePanelフォルダ追加問題
- SystemVerilog Parse Error（複数存在）

以下の問題は調査済みで**修正済み**:
- TreeControlのちらつき問題 ✅ 修正済み

---

### 6. コード品質の問題

#### 6.1 ロックの冗長な使用
- `VerilogModuleInstance.cs`: `_getKey()`内でReadLockを使用しているが、`KeyGenerator`自体がスレッドセーフの可能性
- `Updater.cs`: `GetSnapShot()`と`ReplaceTo()`をatomicに扱おうとしているが、間に他のスレッドが介入する余地あり

#### 6.2 名前付きパラメータの不一致
- `Root.cs:196`: `Package.ParseCreate()`の引数順序が他のメソッドと不一致
```csharp
// 不一致な例
package = await Package.ParseCreate(word, null, parsedDocument.Root, file, ...);
// 他のメソッドでは
module = await Module.ParseCreate(word, null, null, parsedDocument.Root, file, ...);
```

#### 6.3 デバッグ出力の混在
- `Updater.cs`: `System.Diagnostics.Debug.Print()`が残っている
- 特定のファイル名（`scr1_top_tb_ahb.sv`、`i_top`）に対する条件付きデバッグ出力

---

### 優先度付けされた問題一覧

| 優先度 | 問題 | 影響 |
|--------|------|------|
| 高 | ビルドエラー（ロック） | 開発効率 |
| 高 | SystemVerilog Parse Error | 機能 |
| 中 | throw new Exception() | 安定性 |
| 中 | Debugger.Break() | 開発体験 |
| 低 | 空のcatchブロック | 安定性 |
| 低 | コード品質 | 保守性 |

---

## 調査記録: Linux環境でのファイル変更誤検知問題 (2025-02-14)

### 問題概要
Linux環境でファイルを保存した後、`FileCheckAsync` がファイル変更を検出し、コンフリクトダイアログが表示される。
しかし実際にはファイルは編集しておらず、セーブしただけの場合に誤検知が発生する。

### 症状
1. **保存直後にコンフリクトが検出される** (編集していないのに)
2. **一度コンフリクト_dialogで「破棄」を選択すると、次保存まで問題は発生しない**
3. **Windows環境では問題が発生しにくい**

### 関連ファイル

| ファイル | 役割 |
|----------|------|
| `CodeEditor2/CodeEditor2/CodeEditor2/Data/TextFile.cs` | `FileCheckAsync`, `SaveAsync` |
| `CodeEditor2/CodeEditor2/CodeEditor2/Data/DataAccess.cs` | ファイル読み込み/書き込み |

### セマフォによるファイル操作の競合防止

**同一ファイル内での Race Condition は防止されている**:
- `_fileSemaphore` (SemaphoreSlim 1,1) で SaveAsync と FileCheckAsync を排他制御
- 各 TextFile インスタンスごとに独立しているため、同一ファイル内での不整合は発生しない

```csharp
// SaveAsync - ブロックして待つ
await _fileSemaphore.WaitAsync();
try {
    // ファイル保存
    loadFileHash = newHash;  // 保存完了時にハッシュ更新
} finally {
    _fileSemaphore.Release();
}

// FileCheckAsync - 待たない、即座にリターン
if (!await _fileSemaphore.WaitAsync(0)) {
    return;  // 取得できなければ早期リターン
}
```

### 原因の特定 (調査中)

#### 仮説: Linuxカーネルのページキャッシュ遅延

**Linux のファイル I/O 動作**:
1. 書き込み: `WriteAsync` → カーネルのページキャッシュに溜まる
2. `FlushAsync`: ページキャッシュへの書き込みを完了させるが、**ディスクへの書き込みは保証しない**
3. 読み込み: ページキャッシュから返される

**問題シナリオ**:
```
時刻T1: SaveAsync が WriteAsync + FlushAsync を実行
        → ページキャッシュにデータが溜まる
        → loadFileHash = 新しいハッシュ に更新
        
時刻T2: FileCheckAsync が GetFileTextAsync を実行
        → ページキャッシュからファイルを読み込もうとする
        → まだ古いデータが残っている可能性
        
時刻T3: GetHash(text) != loadFileHash
        → コンフリクト dialog！
```

#### コード上の問題点

**保存時のファイル出力** (`DataAccess.saveFileAsync`):
```csharp
using (FileStream fs = new FileStream(
    project.GetAbsolutePath(relativePath),
    FileMode.Create, FileAccess.Write, FileShare.Read,
    bufferSize: 4096 * 32, useAsync: true))  // ← useAsync モード
{
    byte[] encodedText = Encoding.UTF8.GetBytes(text);
    await fs.WriteAsync(encodedText, 0, encodedText.Length);
    await fs.FlushAsync();  // 同期flushではない
}
```

**読み込み時のファイル入力** (`DataAccess.getFileTextAsync`):
```csharp
using (FileStream fs = new FileStream(
    project.GetAbsolutePath(relativePath),
    FileMode.Open,
    FileAccess.Read,
    FileShare.Read,
    bufferSize: 4096 * 32,
    useAsync: true))  // ← useAsync モード
{
    using var sr = new StreamReader(fs, Encoding.UTF8, true);
    text = await sr.ReadToEndAsync();
}
```

#### loadFileHash の二重設定問題

**初期ロード時** (`TextFile.FileCheckAsync`):
```csharp
string newHash = GetHash(text);
loadFileHash = newHash;  // ★ ここで設定 (UI Thread dispatch の前)

await Dispatcher.UIThread.InvokeAsync(async () =>
{
    var doc = CodeDocument;
    if (doc != null)
    {
        doc.TextDocument.Replace(0, doc.TextDocument.TextLength, text);
        doc.Clean();
    }
    loadFileHash = newHash;  // ★ ここで再度設定 (UI Thread の中)
    await FileChangedAsync();
});
```

**問題点**:
- `loadFileHash` が **2箇所**で設定されている
- `Dispatcher.UIThread.InvokeAsync()` で UI Thread にポストされた後、`FileCheckAsync` がリターンする
- この間に別の `FileCheckAsync` が発火する可能性がある

### 修正方針案 (調査中)

| # | 修正案 | 対象箇所 | 効果 |
|---|--------|----------|------|
| 1 | `useAsync: true` → `useAsync: false` | 読み込み側 | ページキャッシュの問題を回避？ |
| 2 | `FlushAsync()` → `Flush()` (同期flush) | 保存側 | 書き込み完了を保証 |
| 3 | `FileOptions.WriteThrough` の追加 | 保存側 | ディスクに直接書き込み |
| 4 | `loadFileHash` の設定位置を整理 | FileCheckAsync | 二重設定問題を解決 |

### 備考
- **調査のみの実施。修正は未実施。**
- キャッシュ使用時の問題は除外して調査中
- Linux での `useAsync: true` オプションの動作要注意

---

## License

MIT License
