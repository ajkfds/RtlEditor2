# VS Code 拡張アーキテクチャ方針: 実装を C# 側へ寄せる

（未実装・検討段階。コードは未修正）

## 目的

- 実装の実態を可能な限り TypeScript 側に乗せず、C# 側（SystemVerilogLanguageServer）で実装する。
- TypeScript 側は「情報のパイプ（配管）」に徹する。
- C# 側のコードの見通しを良くする（プロトコル層 / ドメイン層 / ビュー層の分離）。

## 最上位の制約: LSP 標準を破らない

- **LSP 標準メソッド（initialize / textDocument/hover / definition / references / documentSymbol / completion / semanticTokens / publishDiagnostics 等）は LSP 仕様のまま維持する。** 標準メソッドの params / result の形を独自拡張で歪めない。
- **標準で足りない機能は「独自拡張」として追加する。** 独自メソッドはベンダープレフィックス `rtl/` を付ける（LSP が実装定義メソッドを許容する枠組み。既存の `rtl/hierarchy` / `rtl/reparse` と同じ）。
- 独自拡張は「追加」のみ。標準メソッドの意味を変えたり、標準 result に独自フィールドを混ぜてクライアントを標準非準拠にしない。
- 標準メソッドの薄いマッピング（LSP 型 → vscode 型）は TypeScript に残す（これは LSP 仕様の範囲であり「実装ロジック」ではない）。
- 非標準の判断・計算・知識は、標準メソッドを汚さず **`rtl/*` 拡張メソッド / 通知として C# 側に実装する**。

## 境界の原則（TypeScript 側の憲法）

1. サーバが返した値を解釈しない。`expandToken` は不透明文字列として保持・返送するだけ。
2. パス計算（`dirname` / `join` / `concat`）をしない。サーバが `fsPath` / `dirPath` / `instancePath` を返す。
3. 「いつ再走査するか」を判断しない。サーバの `status` フラグ / 通知に従う。
4. 色・legend・kind の表を持たない。サーバから受け取る。
5. 標準 LSP メソッドは LSP 仕様どおりに中継する（形を変えない）。非標準機能は `rtl/*` 拡張経由でのみ扱う。

分担の線引き:
- 「判断・計算・知識」= C#（非標準分は `rtl/*` 拡張として実装）
- 「VS Code API 型への変換」= TypeScript（標準 LSP の薄いマッピング）
- 「ファイル I/O の実行」= TypeScript（`vscode.workspace.fs`。remote / 仮想 FS / ウォッチャ連携のため）

## 現状の責務分析

### extension.js

| 分類 | 現状の実装 | 性質 |
|---|---|---|
| トランスポート | `RawLspClient`（Content-Length フレーミング、pending map、dispatch） | TS 必須 |
| プロセス起動 | `resolveServerCommand` / `spawn` | TS 必須 |
| ログ | `initLog` / `logLine` / `showLog` | TS 必須 |
| ルート推定 | `inferRootFromOpenDocuments` / `workspaceRootUri` / `workspaceFolderParams` | TS 必須（VS Code API のみが知る情報） |
| LSP→vscode 変換 | `toLocations` / `toSymbol` / `provideHover` / `provideCompletion` / `provideDocumentSemanticTokens` | 薄いマッピング（LSP 仕様） |
| 重複知識 | `tokenTypesByIndex`（legend をハードコード） | C# へ移設可（initialize の legend を読む） |
| 重複知識 | `completionKinds`（3/6/9/14 のマップ） | C# へ寄せられる |
| 実装ロジック | `semanticTokenColorCustomizations` の色テーブル（pluginVerilog CodeDrawStyle の写し） | C# へ移設すべき（エディタと二重管理・ドリフト源） |

### hierarchy.js

| 分類 | 現状の実装 | 性質 |
|---|---|---|
| TreeDataProvider | `HierarchyProvider` / `getTreeItem` / `getChildren` | TS 必須（VS Code API） |
| 実装ロジック | `_toItems` の `nodePath = parentPath.concat([index])` | C# へ移設可（サーバが token を返せる） |
| 実装ロジック | `instancePath = parent + '.' + label` の組み立て | C# へ移設可 |
| 実装ロジック | `resolveFsPath` / `resolveDirPath`（`path.dirname` 等） | C# へ移設可（サーバが fsPath/dirPath を返せる） |
| 実装ロジック | `getTreeItem` の icon / contextValue / collapsible 選択 | C# へ移設可（サーバが提示メタを返せる） |
| 状態機械 | `pendingRescan` / `initialRescanDone` / `retryScheduled` / `refresh` vs `refreshTree` / `refreshIfEmpty` | C# へ移設すべき（「いつ再走査するか」はサーバが判断できる） |
| 空状態 | `info` ノード生成・ログ reveal | サーバが `emptyReason` を返せば TS は定型表示のみ |
| ファイル操作 | `addFolder` / `addBlankFile` / `deleteNode` / `copyPath` / `openInExplorer` / `showProperty` | パス解決は C#、fs 実行は TS |
| クリック | `openAt` / `reparseAndRefresh` | TS 必須（API 呼び出し） |

結論: TS に乗っているのは「(a) トランスポート、(b) VS Code API 呼び出し、(c) 本来 C# が持つべき判断・計算・重複知識」の3種。(c) が大きいのが現状。

## 移設項目

### 1. 階層ノードを「完全な記述子」にする（最重要）

サーバが提示情報まで含めて返す。TS の `getTreeItem` は純粋な switch になり、`_toItems` の計算が全消しできる。

```jsonc
// rtl/hierarchy レスポンス（案）
{
  "nodes": [{
    "label": "u_mid",
    "description": "mid",              // TreeItem.description
    "tooltip": "u_mid.u_leaf  (mid)",
    "icon": "symbol-class",            // ThemeIcon 名（"folder"/"file"/"symbol-class"/"warning"）
    "contextValue": "rtlInstance",     // メニューの when 条件に直結
    "collapsible": "collapsed",        // "none" | "collapsed"
    "expandToken": "eyJ1cmkiOi...",    // 不透明。TS は保持して返送するだけ
    "openUri": "file:///...",          // クリックで開く
    "openLine": 12, "openCharacter": 8,
    "instancePath": "u_mid.u_leaf",    // Copy Instance Path 用
    "fsPath": "D:/proj/src/mid.sv",    // ファイル操作用
    "dirPath": "D:/proj/src"           // Add 用
  }],
  "status": { "emptyReason": null, "needsRetry": false }
}
```

### 2. `nodePath` → 不透明 `expandToken`

- 現状: `nodePath` はインデックス列で、TS が `parentPath.concat([index])` で組み立てる。ツリーが途中で変わるとインデックスがずれる脆弱性もある。
- 提案: サーバが `expandToken`（`fileUri + パス` をエンコードした不透明文字列、または安定 ID）を返し、TS は `getChildren` でそのまま返送する。TS は中身を一切知らない。堅牢性も上がる。

### 3. 再走査・再描画の状態機械を C# へ

- TS の `refresh()` は「ツリー変更を fire するだけ」。`rescan` フラグの判断はサーバへ。
- サーバはトップレベル要求時に自分で `EnsureWorkspaceRootFromKnownFiles` / `RescanWorkspaceFiles` を判断（既に一部実装済み）。
- 空応答時は `status.emptyReason`（`"no-workspace-root"` / `"no-files"`）を返し、TS は定型 info ノードを出すだけ。
- ルート確定をサーバが検知したら `rtl/hierarchyInvalidated` 通知を push → TS は `refreshTree()` するだけ。「2秒後リトライ」のようなタイマーはサーバ側の責務に。
- これで TS の `initialRescanDone` / `retryScheduled` / `refreshIfEmpty` が消える。

### 4. 色・legend・kind の単一情報源化（LSP 標準の範囲内で）

| 項目 | 現状 | 提案 | 標準/拡張 |
|---|---|---|---|
| semantic token legend | TS にハードコード（`tokenTypesByIndex`） | `initialize` の `semanticTokensProvider.legend` を読む（既にサーバが返している） | **標準**（legend は LSP 仕様の一部。TS はサーバ申告値をそのまま使う） |
| completion kind | TS にマップ表 | LSP 仕様の固定値なので TS 維持可（標準の CompletionItemKind 数値） | **標準**（変更しない） |
| 色パレット | TS に手調整テーブル | サーバに `rtl/theme` リクエスト新設 → pluginVerilog `CodeDrawStyle` から生成して返す。TS は settings に書くだけ | **拡張**（`rtl/theme`。標準の semanticTokens は色を運ばないため、色は非標準情報として拡張で取得） |

色は特にエディタ（pluginVerilog）と二重管理でドリフトするので、C# を単一情報源にする価値が大きい。ただし **semanticTokens の標準 result（data 配列）に色を混ぜない**。色は `rtl/theme` 拡張で別途取得する。

### 5. ファイル操作のパス解決を C# へ

- 現状: `resolveFsPath` / `resolveDirPath` が TS で `path.dirname` 等を計算。
- 提案: ノード記述子に `fsPath` / `dirPath` を含める（上記1）。TS は `vscode.workspace.fs` を叩くだけ。
- fs 実行自体は TS に残す理由: `workspace.fs` はリモート/仮想 FS・ファイルウォッチャ連携を扱う。サーバの `System.IO` で書くと VS Code のウォッチャや remote を迂回してしまう。「パスは C#、I/O は TS」の分担が正解。

## snippet と右クリックメニューの位置づけ

### snippet（現状: C# にあるが VS Code 未接続）

- 実体は `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/Snippets/*`（`AlwaysFFSnippet` / `AutoConnectSnippet` / `AutoFormatSnippet` / `ModuleInstanceMenuSnippet` / `ModuleInstanceSnippet` / `PortConnectionCreateSnippet` / `PortInvertSnippet` / `ModPortSnippet` / `ConnectionCheckSnippet`）。登録は `Data/VerilogCommon/AutoCompleteHandler.cs` の `GetToolItems`。
- **現状 VS Code 拡張には一切露出していない**（`vscode-extension` に snippet 実装なし）。エディタ専用機能。
- 3 層に分解できる:
  | 層 | 内容 | 置き場所 |
  |---|---|---|
  | (a) snippet ロジック | 何を挿入するか（module instance の port 列挙、autoConnect の接続生成、autoFormat の整形、portInvert の反転など） | **C#**（既に C#。`rtl/snippet` 拡張で結果を返す） |
  | (b) 挿入の実行 | caret 移動・プレースホルダ選択・ハイライト・キー操作 | **TS**（VS Code API: `TextEditor.insertSnippet(new vscode.SnippetString(...))` / `Selection`）。エディタの `InteractiveSnippet` は VS Code では再現できないため、`SnippetString` のプレースホルダ（`$1`/`${1:...}`）に変換して渡す |
  | (c) トリガ登録 | コマンド / メニュー / キーバインド | **TS**（`package.json` の `contributes.commands` / `menus` / `keybindings`） |
- 方針: **snippet の「何を挿入するか」は C# に集約**し、`rtl/snippet`（request）で `{ label, snippetText, placeholders, kind }` を返す。TS は `insertSnippet` を呼ぶだけ。エディタの `InteractiveSnippet`（対話的キー操作）は VS Code では `SnippetString` のタブストップに置き換える（対話ロジックの完全移植はしない）。
- 注意: 既存 snippet は `CodeEditor2.Controller.CodeEditor` / `PopupMenuView` / `KeyEventArgs` に強く依存（UI 密結合）。C# 側で「挿入テキスト生成」と「UI 対話」を分離するリファクタが必要（`InteractiveSnippet` から純粋なテキスト生成部を抽出）。

### RTL Hierarchy の右クリックメニュー（現状: TS）

- 実体は `vscode-extension/package.json` の `contributes.menus.view/item/context`（`when: viewItem == rtlFolder/rtlFile/rtlInstance` で出し分け）+ `hierarchy.js` の各コマンド実装（`addFolder` / `addBlankFile` / `deleteNode` / `copyPath` / `openInExplorer` / `showProperty` / `revealInEditor` / `copyHierarchyPath`）。
- 3 層に分解できる:
  | 層 | 内容 | 置き場所 |
  |---|---|---|
  | (a) メニュー定義 | どのノード種別にどの項目を出すか（`when` 条件）、ラベル、グループ順 | **TS**（VS Code の宣言的 `contributes.menus`。C# から動的生成は不可） |
  | (b) 出し分けの判断材料 | ノードが folder/file/instance のどれか、操作可能か | **C#**（ノード記述子の `contextValue` として返す。TS は `when` で参照するだけ） |
  | (c) アクション実装 | パス解決（fsPath/dirPath）、何をすべきかの判断 | **C#**（記述子に `fsPath`/`dirPath` を含める）。実際の fs 操作・VS Code API 呼び出しは **TS** |
- 方針: **メニューの「定義」（contributes.menus / when / ラベル）は VS Code 宣言なので TS に残す**。ただし「どのノードにどのメニューを出すか」を決める `contextValue` と、各アクションが必要とするパス・判断は C# のノード記述子に寄せる。TS のコマンド実装は「記述子の値を使って `vscode.workspace.fs` / `revealFileInOS` を呼ぶだけ」に痩せさせる。
- 補足: メニュー項目のラベルを C# から動的に変えたい場合は `contributes.menus` では不可。必要なら `TreeItem.contextValue` を細分化するか、`vscode.commands` を TS 側で動的登録する（この場合もラベル文字列は C# が `rtl/hierarchy` のノード記述子で返せる）。

### まとめ（どちらも「定義は TS・中身は C#」）

| 機能 | TS に残るもの | C# に寄せるもの |
|---|---|---|
| snippet | トリガ登録 / `insertSnippet` 実行 / タブストップ操作 | 挿入テキスト生成・port 列挙・接続生成などのロジック（`rtl/snippet`） |
| 右クリックメニュー | `contributes.menus` の定義 / `when` / ラベル / fs 実行 | `contextValue`（出し分け材料）/ `fsPath`・`dirPath`（パス解決）/ 操作可否の判断 |

## 独自拡張（`rtl/*`）の一覧

標準 LSP で足りない機能は、標準を汚さず `rtl/*` 拡張として C# 側に実装する。

| 種別 | 名前 | 用途 | 状態 |
|---|---|---|---|
| request | `rtl/hierarchy` | RTL Hierarchy ツリー（トップ/フォルダ/ファイル/インスタンス） | 既存（記述子を厚くする） |
| request | `rtl/reparse` | 指定ファイルの再 parse（クリック時の最新化） | 既存 |
| request | `rtl/theme` | pluginVerilog `CodeDrawStyle` 由来の色パレット（semantic token 色） | 新設予定 |
| request | `rtl/snippet` | snippet の挿入テキスト生成（module instance / autoConnect / autoFormat / portInvert 等）。TS は `insertSnippet` するだけ | 新設予定 |
| notification | `rtl/hierarchyInvalidated` | サーバがルート確定/再走査必要を検知したら push → TS は再描画のみ | 新設予定 |

- 既存の `rtl/hierarchy` / `rtl/reparse` はこの方針に沿って拡張する（標準メソッドは触らない）。
- 新規の非標準機能は必ず `rtl/` プレフィックスの request / notification として追加する。
- 標準メソッド（hover / definition / references / documentSymbol / completion / semanticTokens / publishDiagnostics）は LSP 仕様のまま。TS は薄いマッピングのみ。

## C# 側の構造改善（見通しの良さ）

移設先を `LspHandler` に詰め込むと逆に見通しが悪くなるので、専用サービスに切り出す。

```
SystemVerilogLanguageServer/
├── Server/
│   ├── LspHandler.cs              ← 薄いディスパッチャのみ（params 解析 → service → serialize）
│   └── InMemorySystemVerilogCore.cs ← Updater 駆動（既存）
└── Hierarchy/                     ← 新設：階層ビューの責務を集約
    ├── HierarchyViewService.cs    ← トップ/フォルダ/ファイル/インスタンスの解決と再走査判断
    ├── HierarchyNodeViewModel.cs  ← 提示情報込みの記述子（icon/contextValue/token/paths）
    ├── ExpandToken.cs             ← 不透明トークンの encode/decode
    └── HierarchyThemeProvider.cs  ← CodeDrawStyle → 色パレット
```

- `LspHandler.HandleHierarchy` は「パラメータを読む → `HierarchyViewService.GetNodes(token, refresh)` → 返す」だけに。
- `HierarchyViewService` は UI フリーで単体テスト可能（既存の `WorkspaceHierarchyTests` をそのまま流用可能）。
- 記述子生成（`CreateDescriptor` / `CanExpand` / `DescribeItem` / `ResolveItemUri`）は `HierarchyNodeViewModel` 側へ移し、`InMemorySystemVerilogCore` は「Updater を回して Item を返す」純粋な役割に絞る。
- これで C# は「プロトコル層（LspHandler）／ドメイン層（Core）／ビュー層（Hierarchy）」に分かれ、各ファイルの責務が読んで分かるようになる。

## 移行フェーズ（段階的・各段でビルド/テスト可能）

| Phase | 内容 | TS から消えるもの | リスク |
|---|---|---|---|
| 1 | 記述子に提示メタ（icon/contextValue/collapsible/instancePath/fsPath/dirPath）を追加 | `_toItems` の計算、`getTreeItem` の分岐、`resolveFsPath`/`resolveDirPath` | 小（後方互換で追加のみ） |
| 2 | `nodePath` → `expandToken` 化 | `parentPath.concat` | 中（プロトコル変更、両側同時） |
| 3 | 再走査状態機械をサーバへ（`status` + `rtl/hierarchyInvalidated` 通知） | `pendingRescan`/`initialRescanDone`/`retryScheduled`/`refreshIfEmpty` | 中 |
| 4 | legend を initialize から読む / `rtl/theme` 新設 | `tokenTypesByIndex` ハードコード、色テーブル | 小 |
| 5 | C# を `Hierarchy/` サービスに再編 + テスト移設 | （TS 変化なし） | 小（内部リファクタ） |

Phase 1 と 4 は「追加のみ」で低リスクなので先行推奨。Phase 2/3 はプロトコル変更なのでまとめて実施。

## トレードオフ・注意点

- プロトコルが独自化する: `rtl/hierarchy` は既に独自拡張なので、記述子を厚くするのは自然。ただし **LSP 標準（hover/definition/references/documentSymbol/completion/semanticTokens/publishDiagnostics 等）は LSP 仕様のまま維持する**（TS の薄いマッピングは残す）。独自化は `rtl/*` 拡張の内側に閉じ込め、標準メソッドの params/result を歪めない。
- fs 実行は TS に残す: 上記の通り remote/仮想 FS のため。ここを C# に寄せると VS Code 連携が壊れる。
- `expandToken` の互換: トークン形式を変えるとクライアント/サーバのバージョン不整合で壊れる。`initialize` でサーバがプロトコル版を返し、TS が照合する仕組みを入れると安全。
- 過剰な記述子化の回避: すべてをサーバ記述子にすると「VS Code 固有の表現」が C# に漏れる。「判断・計算・知識」は C#、「API 型への変換」は TS の線を守る。

## 未決事項（着手前に決める）

- Phase 1 の記述子フィールド確定。
- `expandToken` を Phase 1 で入れるか Phase 2 まで待つか。
