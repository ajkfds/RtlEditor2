# 作業状態 (State)

## 進行中タスク

- SystemVerilogCore への抽象化移動 → Phase 6 (Verilog/* 残ファイル移動 / テスト拡張) が次のステップ
  - Phase 1: interface 群 + first-cut adapter 完了
  - Phase 2A: BuildingBlock / NamedElement adapter 実装完了 (TopLevelBlocks, Root, FindElementAt)
  - Phase 2B: FindDefinitionAsync / FindReferencesAsync を `Root.GetHierarchyNameSpace` + `NameSpace.GetNamedElementUpward` + `DataObject.UsedReferences/AssignedReferences` 経由で実装完了
  - Phase 3: クロスファイル参照を `ProjectProperty.DefinitionNameSpace` / `PackageNameSpace.GetFile` 経由で実装完了 (definition は取れる、cross-file references は declaration のみ)
  - Phase 4: Hover 強化を `HoverContent` + `IHoverContentProvider` + `PluginHoverInstaller` で実装完了
  - Phase 5: Diagnostic code map + 13 xUnit テスト追加
  - Phase 6: 残りの Verilog/* ファイル (Statement系, AutoComplete系) の SystemVerilogCore 移動 + テスト拡張 (parser-backed adapter の結合テスト)

## 完了済みタスク

- `.agents/overview.md`に`TemplateEngineHost`をProject Structureとサブモジュール一覧に追加
- `memory/state.md`を作成 (rules.mdで参照されているが実在しなかった)
- verilogpluginにおいて`typedef struct packed`の要素を参照、代入した時の`undriven`/`unused` noticeを修正
  - `DataObjectReference.ParseCreate`で`owner`が`Struct`または`UserDefinedType(wrapping StructType)`の場合に`StructParentObject`と`StructMemberName`をセット
  - `AssertAssigned()`で親Structの`AssignedMap`にmember rangeを反映して`undriven` noticeを抑制
  - member access時に親Structの`UsedReferences`に`val.Reference`を追加して`unused` noticeを抑制
  - `AssertAssigned()`の冗長な`AbsoluteRangeExpression`構築を削除し、シンプルな`Assert(long, long)`を使うように修正
  - コミット: CodeEditor2VerilogPlugin のサブモジュール内 commit `499149a`
- ImportedPackage を `Data/ImportedPackage.cs` に追加し、VerilogModuleInstanceに準じるクラスとして実装
  - `Updater.cs` の `UpdateAsync` で各item (VerilogFile/VerilogModuleInstance/InterfaceInstance) の `parsedDocument.ImportedPackages` を `addImportedPackage` で階層生成
  - `NavigatePanel/ImportedPackageNode.cs` を新規作成 (UpdateVisual, OnSelected, GetIcon など)
  - `Tool/ParseHierarchy.cs` の `parseDownwardAsync` / `parseUpwardAsync` で `ImportedPackage` を `IVerilogRelatedFile` として処理
  - 同じ `parseDownwardAsync` 内の `ImportedPackages` に対する参照元ファイルもparseキューにenqueue

## Next Steps

- Phase 6: テスト拡張
  - parser-backed adapter (CodeEditor2VerilogPlugin.CoreBridge) をテストプロジェクトから呼べるようにし、cross-file definition jump / hover / find-references の結合テストを追加
  - 既存テストは in-memory core のみを覆っている (CodeEditor2VerilogPlugin の symbol 解決ロジックは未カバー)
- VerilogSystemVerilogCore.Wrap の呼び出し点を Plugin 側 (例: Plugin.cs や
  ParseHierarchy) から呼び、editor 上で CodeEditor2VerilogPlugin + LSP が
  同じ adapter を共有するシナリオを検証
- Phase 6: Verilog/* の Statement系 / AutoComplete系を SystemVerilogCore に移動 (Avalonia 依存の分離)
- Phase 6: LspHandler の documentSymbol を `Root.BuildingBlocks` + 各 `BuildingBlock.Members` を列挙する実装に置き換える (現在は root のみ返している)

## メモ

- `.agents/rules.md`と`.agents/overview.md`を参照すること
- サブモジュール構造のため、コミット時は`git -C`を使用すること
- ビルド確認は`dotnet build "RtlEditor2.Desktop.csproj" -clp:ErrorsOnly`

## 完了済みタスク (続き)

- SystemVerilogCore 抽象化 Phase 2A: BuildingBlock / NamedElement adapter 実装
  - `CoreBridge/BuildingBlockAdapter.cs` 新規: `ISystemVerilogBuildingBlock` のラッパ。`BuildingBlock.Name`, `NamedElements`, `BuildingBlocks` を橋渡し
  - `CoreBridge/NamedElementAdapter.cs` 新規: `INamedElement` を `ISystemVerilogNamedElement` に変換する factory + 具象 adapter (DataObject / Typedef / Function / Task / Generic)
  - `SystemVerilogFileAdapter.TopLevelBlocks` を `ParsedDocument.Root.BuildingBlocks` から構築
  - `SystemVerilogDocumentAdapter.Root` を `ParsedDocument.Root` から構築、`FindElementAt` を `Root.GetHierarchyNameSpace` + `NameSpace.Items` 探索で実装
  - `SystemVerilogNamedElementKind` enum に `Checker` を追加
  - コミット:
    - SystemVerilogCore: `40dc9e7` "Add Checker kind to SystemVerilogNamedElementKind enum"
    - CodeEditor2VerilogPlugin: `64cff6d` "Wire SystemVerilogCore building block / named element adapters"

- SystemVerilogCore 抽象化 Phase 2B: Definition / References lookup 実装
  - `CoreBridge/SymbolResolver.cs` 新規:
    - `FindDefinition`: `CodeDocument.GetWord` で word 取得 → `Root.GetHierarchyNameSpace` で ns 取得 → `ns.GetNamedElementUpward(text)` で element 解決 → `NamedElementAdapter.TryCreate` で ISystemVerilogNamedElement に変換
    - `FindReferences`: 上記で element 解決後、`DataObject` なら `UsedReferences + AssignedReferences` + `DefinedReference` を全件 (重複除去) で返す。`DataObject` 以外なら definition のみ返す
    - 内部に軽量 `ReferenceAdapter` (ISystemVerilogNamedElement 実装) を持ち、use site の WordReference を LSP 互換の `DefinitionRange` として提供
  - `SystemVerilogProjectAdapter.FindDefinitionAsync` / `FindReferencesAsync` が `SymbolResolver` を呼び出すように更新
  - コミット: CodeEditor2VerilogPlugin `8258780` "Wire SystemVerilogCore definition / references lookups"

- SystemVerilogCore 抽象化 Phase 3: クロスファイル参照 実装
  - `CoreBridge/SymbolResolver.cs` 更新:
    - `Resolve` を 2 段階化: (1) ローカル namespace ツリー (2) `ProjectProperty.DefinitionNameSpace` / `PackageNameSpace` の file registry を `GetFile(name)` で検索
    - クロスファイルで見つかった場合は `SystemVerilogFileAdapter(declaredFile)` を返し、`ISystemVerilogNamedElement.File` が declaration の file を指すようにする
    - `FindReferences` は cross-file declaration に対して declaration のみを返す (parser は cross-file reference site を集積しないため)
  - コミット: CodeEditor2VerilogPlugin `c2fada4` "Resolve cross-file symbols in SystemVerilogCore bridge"

- SystemVerilogCore 抽象化 Phase 4: Hover 強化
  - SystemVerilogCore 側:
    - `Documents/HoverContent.cs` 新規: `ISystemVerilogNamedElement` から markdown ベースのホバー文字列を生成する static ヘルパー。kind ごとに `module {name}` / `parameter {name}` などの署名を生成し、`_Defined in: {path}_` を末尾に付与
    - `Documents/IHoverContentProvider.cs` 新規: plugin 側が richer な情報を追加できる extension point。`HoverContent.RegisterProvider(provider)` で process-wide にインストール
  - CodeEditor2VerilogPlugin 側:
    - `CoreBridge/PluginHoverContentProvider.cs` 新規: `IHoverContentProvider` 実装。`DataObject` なら `DataType` / `BitWidth`、`Port` なら `Direction` / `BitWidth`、`BuildingBlock` なら port list を hover に splice
    - `PluginHoverInstaller.Install()`: `Plugin.Register()` から呼ばれ、`HoverContent.RegisterProvider(...)` で process-wide に登録
    - `Plugin.cs`: `Register()` の先頭で `PluginHoverInstaller.Install()` を呼ぶ
  - SystemVerilogLanguageServer 側:
    - `LspHandler.HandleHover`: `HoverContent.Build(def)` を呼ぶよう更新 (custom provider は process-wide に効く)。MarkupContent.Kind を `"markdown"` に変更
  - コミット:
    - SystemVerilogCore: `96c14b4` "Add HoverContent helper and IHoverContentProvider extension point for LSP hover"
    - CodeEditor2VerilogPlugin: `93b6d89` "Register plugin-side hover content provider with SystemVerilogCore"
    - SystemVerilogLanguageServer: `17fb89c` "Use HoverContent helper for markdown hover response"

- SystemVerilogCore 抽象化 Phase 5: Diagnostic code + テスト追加
  - `CoreBridge/DiagnosticCodeMap.cs` 新規: 既知のメッセージ substring (undriven / unused / not defined here / duplicate / implicit net / bitwidth / ...) を `verilog/xxx` 形式の code にマップ。フォールバックは message text の slug
  - `SystemVerilogDocumentAdapter.DiagnosticAdapter.Code` を `DiagnosticCodeMap.FromMessage(Message)` から取得
  - テストプロジェクト `SystemVerilogLanguageServer.Tests` 新規 (xUnit, net10.0)
    - SystemVerilogCore / SystemVerilogLanguageServer を ProjectReference
    - `InMemorySystemVerilogCore` / `InMemoryProject` / `InMemoryFile` を `InternalsVisibleTo` でテストから参照
    - 13 テスト追加 (definition / references / hover / LspHandler / CodeDocument / custom provider)
  - `SystemVerilogLanguageServer.csproj` に `InternalsVisibleTo` 追加
  - `InMemorySystemVerilogCore.GetOrCreateProjectPublic(string)` を public テストヘルパーとして追加

## .agents/* の調査メモ (2025-XX-XX)

- `.agents/rules.md`: 振舞い・規約
- `.agents/overview.md`: プロジェクト概要 (RtlEditor2の説明、build方法、Git Commit手順)
- `AGENTS.md`: 詳細な調査記録・修正履歴・既知の問題・ステータス情報
- `memory/state.md`: 作業状態 (本ファイル)

### .agents/overview.mdの不足項目として発見・修正した内容
1. `TemplateEngineHost`がProject Structureとサブモジュール一覧に含まれていなかった → 追加済み
2. `memory/state.md`がrules.mdで参照されているが実在しなかった → 作成済み

### AGENTS.mdに集約されているが、overview.mdにも転記を検討すべき内容
- SystemVerilog Parse Errorリスト (11+ bugs)
- 修正履歴 (過去の修正サマリ)
- 調査記録 (UIスレッドロック、NavigatePanel、HierarchyConnection等)
- 既知の問題のサマリ

## SystemVerilogCore 抽象化タスク 事前調査 (2025-XX-XX)

### 目的
- `SystemVerilogLanguageServer` (LSP) を新設
- `CodeEditor2VerilogPlugin` の解析ロジックを `SystemVerilogCore` に抽象化して移動
- 両方から `SystemVerilogCore` を呼び出せるようにする

### 現状の主要依存関係 (CodeEditor2VerilogPlugin/Verilog 配下)
- `CodeEditor2.CodeEditor.CodeComplete.AutocompleteItem` (Avalonia Media 依存)
- `CodeEditor2.CodeEditor.Parser.DocumentParser.ParseModeEnum`
- `CodeEditor2.CodeEditor.PopupHint.PopupItem` (Avalonia 依存)
- `CodeEditor2.CodeEditor.ParsedDocument` (基底クラス)
- `pluginVerilog.CodeEditor.CodeDocument` (CodeEditor2 依存)
- `pluginVerilog.FileTypes.SystemVerilogFile` etc.
- `Avalonia.Media.Color` / `Avalonia.Media.Colors`
- `AjkAvaloniaLibs.Libs.Icons.GetSvgBitmap`
- `Plugin.StaticID` (= "Verilog")

### UI非依存化のために必要となる抽象化
1. **CodeDocument**: `AvaloniaEdit.Document.TextDocument` 依存 → 文字列/行配列での ICodeDocument 再定義
2. **AutocompleteItem**: Avalonia PopupMenu 依存 → Core 側ではデータ構造のみ
3. **CodeDrawStyle**: Avalonia.Media.Color 依存 → 整数インデックス or System.Drawing.Color で再定義
4. **PopupItem**: Avalonia 依存 → 文字列リスト + Icon パスで表現
5. **Plugin.StaticID**: ハードコードされた文字列 → 抽象的なプラグイン ID (enum or interface) で参照
6. **MarkHandler / TextColors**: Avalonia 依存 → Core 側では index/length のリストで管理

### 推定される作業規模
- 移動対象ファイル: ~200 ファイル (Verilog/, Verilog/BuildingBlocks/, Verilog/DataObjects/, Verilog/Expressions/, Verilog/Items/, Verilog/AutoComplete/, etc.)
- 依存修正: 各ファイルの `using` 文と `namespace` 宣言
- 新規 interface 定義: `ICodeDocument`, `IColorPalette`, `IAutocompleteItem` 等
- ビルド/コミット: 各フェーズごとに実施

### ファイル数の概算
- BuildingBlocks: 18 ファイル
- DataObjects: 数サブディレクトリ含めて 30+ ファイル
- Expressions: 10+ ファイル
- Items: 10+ ファイル
- AutoComplete: 10 ファイル
- Verilog 直下: 30+ ファイル (Attribute, BuiltInMethod, Comment, ...)

### 推奨アプローチ
- **フェーズ1**: 解析コアの最小単位 (BuildingBlock, NameSpace, IndexReference, WordReference, ParsedDocument 骨格) のみ移動
- **フェーズ2**: DataObjects (Nets, Variables, DataTypes, Arrays, Constants) を移動
- **フェーズ3**: Expressions, Items を移動
- **フェーズ4**: 残りの Verilog/* ファイル (Statement系, AutoComplete系) を移動
- 各フェーズでビルドエラー → 依存interface導入 → 再ビルドのループ
