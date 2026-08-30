# 作業状態 (State)

## 進行中タスク

- SystemVerilogCore への抽象化移動 → 規模が大きいため方針確認が必要 (see メモ)

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

- ユーザからの「SystemVerilogCore 抽象化」のタスク方針確認待ち
  - 現状把握: CodeEditor2VerilogPlugin のVerilog名前空間以下 (BuildingBlocks, DataObjects, Expressions, Items, etc.) は
    `CodeEditor2` (Avalonia 含む) / `Avalonia` / `AjkAvaloniaLibs` / `Plugin.StaticID` に強く依存
  - 1セッションで完全抽象化は不可能。段階的アプローチが必要。
  - 選択肢:
    a) Phase 1: 解析コアのみ (BuildingBlock, NameSpace, IndexReference, WordScanner) を最小依存で移動
    b) Phase 1: SystemVerilogCore には interface のみ置き、CodeEditor2VerilogPlugin の具象実装を参照
    c) Phase 1: SystemVerilogCore に全ファイルを namespace 変更してそのまま移動 (Avalonia依存を持つ型もそのまま、SystemVerilogLanguageServer側で利用しない前提)

## メモ

- `.agents/rules.md`と`.agents/overview.md`を参照すること
- サブモジュール構造のため、コミット時は`git -C`を使用すること
- ビルド確認は`dotnet build "RtlEditor2.Desktop.csproj" -clp:ErrorsOnly`

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
