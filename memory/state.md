# 作業状態 (State)

## 進行中タスク

- README修正案 (CodeEditor2VerilogPlugin/README.md 機能1〜4) の実装 → 実装完了 (ビルド成功、コミット済み)
  - 機能1 (引数位置 hint): `Verilog/Expressions/ListOfArguments.cs` の `ParseListOfArguments` に `word.Eof` 分岐を3箇所追加
    - 括弧 `(` 直後 EOF: 最初の引数 (`PortsList[0]`) の `Port.GetLabel()` を `CarletPopupItems` へ
    - 引数 expression parse 後 EOF (`func(arg1` 直後): 現在の引数 (`PortsList[i]`) の label を hint 表示して早期 return
    - カンマ直後 EOF (`func(arg1, ` 直後): 次の引数 (`PortsList[i]`) の label
  - 機能2 (named argument): `.` 直後 EOF で未接続引数名を `AutoCompleteItems` に列挙 (`appendNamedArgumentCandidates`, positional 接続済み除外)、`.name(` 直後 EOF でその引数の label hint、named 引数 expression へ `Expression.ParseCreate(word, (NameSpace)portNameSpace, completionContext)` で伝播
  - `AppendArgumentPopupItems(completionContext, portNameSpace, index)` を `internal static` helper として新設 (index >= PortsList.Count なら何も追加しない)
  - 機能3 (function 名入力位置): `Expressions/FunctionCall.cs` の `ParseCreate(word, nameSpace, functionDefinedNameSpace, completionContext)` 冒頭で `completionContext.AppendExpression()` 呼び出し (ModuleInstantiation.ParseAsync と同じパターン)
  - statement 経路の completionContext 伝播 (README 機能3の注記対応: `assign x = func(|` 等):
    - `Items/ContinuousAssign.cs` → `DataObjects/VariableAssignment.cs` → `Expression.ParseCreate(word, nameSpace, completionContext)`
    - `Items/ModuleCommonItem.cs` / `Items/NonPortModuleItem.cs` / `Items/ModuleOrGenerateItem.cs` / `Items/ModuleItem.cs` に `completionContext` optional 引数を追加し always/initial/assign/module_instantiation へ伝播
    - `Items/AlwaysConstruct.cs` / `Items/InitialConstruct.cs`: `ParseCreate` に `completionContext` 追加 → `Statements.ParseCreateStatement(word, nameSpace, null, null, completionContext)`
    - `Statements/Statements.cs`: `ParseCreateFunctionStatement` に `completionContext` 追加、attribute 継続・block label 継続・TaskEnable 呼び出しに伝播
    - `Verilog/Function.cs` / `Verilog/Task_.cs`: `Parse` に `completionContext` optional 引数追加、本体内 statement parse へ伝播
    - `Statements/TaskEnable.cs`: `ParseCreate` / `parseCreate` に `completionContext` 追加、引数位置ごとの EOF 分岐 (括弧直後 / expression 後 / カンマ直後) で `AppendArgumentPopupItems` により task 引数 hint 表示
    - `Verilog/Items/ModuleInstantiation.cs`: `parseOrderedPortConnections` に `completionContext` 引数追加、ordered port 接続入力中 (`inst0(clk, ` 等) の EOF 分岐で port label hint、expression へ伝播
    - `Verilog/BuildingBlocks/Class.cs`: extends constructor の `ParseListOfArguments` 呼び出しに `null` 明示 (既存動作を変えない範囲での整合)
    - `Verilog/CompletionContext.cs`: 部分parse分岐に `Verilog.Items.AlwaysConstruct` を追加 (always 文内 caret でも statement 系 hint が動作)
  - 対応外と判断したもの: `BuiltinMethodCall.ParseCreate` (アクティブな呼び出し元が存在しないことを確認、コメントアウトのみ)、`UdpInstantiation` / GenerateBlock 内 statement 経路 (横展開候補として残す)
  - ビルド成功 (CodeEditor2VerilogPlugin.csproj, 0 errors / 既存672 warningsは無関係)
  - コミット: CodeEditor2VerilogPlugin サブモジュール内 (本ターンで作成)

- ChatControl.axaml.cs `completeWork` の System.ObjectDisposedException ("The CancellationTokenSource has been disposed") の原因解析 → 解析完了・未修正
  - 発生箇所: timer ループ内 `await Task.Delay(100, timerCancellationTokenSource.Token)` (行881)。ループ条件の `.Token` アクセス (行873) でも同様に発生し得る
  - 原因: `using var timerCancellationTokenSource` (行870) のスコープが try ブロック内。正常系は 行930-938 の `Cancel()` → `await displayTimerTask` → ブロック抜けて Dispose の順で安全だが、例外系の脱出path (catch (OperationCanceledException) 行952 の return null / catch (Exception) 行957 の retry continue) では timer を Cancel も await もせず try を抜けるため、using が実行中の timer タスクを置き去りにして CTS を Dispose する
  - メカニズム: timer ループが行879 `await resultItem.SetText(...)` (Dispatcher.UIThread.InvokeAsync 完了待ち) で中断中に例外pathで CTS が Dispose → SetText 完了継続が AwaitTaskContinuation でインライン再開し行881に到達 → Dispose済み CTS への Token 取得 / Task.Delay 内部の Register で ObjectDisposedException。内側 catch は TaskCanceledException のみで未捕捉 → displayTimerTask が faulted (例外pathでは await されない未観測例外、デバッガは初回例外で表示)
  - リトライpathでは旧 CTS が Cancel されないまま Dispose されるため、旧 timer タスクが次イテレーションの新 timer と同一 resultItem への二重書き込み競合も起こし得る
  - 対応案: (A) using をやめ finally で `Cancel()` → `await displayTimerTask` → `Dispose()` の順を保証 (推奨・競合を原理的に排除) / (B) token をループ外で1回取得し、timer ラムダ全体を try/catch (OperationCanceledException / ObjectDisposedException) で囲む防御的修正

- 入力時 hint popup を ToolTip から独立した Popup 制御に分離 → 実装完了、popup 非表示問題も修正済み (ビルド成功、コミット済み)
  - **popup 非表示の原因と修正**: `CodeView` コンストラクタで生成した孤立 `Popup` はビジュアル/論理ツリーに属さないため、Avalonia がホスト先 TopLevel を解決できず `IsOpen = true` しても何も表示されない。`HintPopupHandler.OpenPopup()` 内で `((ISetLogicalParent)hintPopup).SetParent(TopLevel.GetTopLevel(codeView.Editor) as ILogical)` により論理親を接続するよう修正 (AvaloniaEdit `CompletionWindowBase.AttachEvents` と同じ方式)
  - コミット: CodeEditor2 `f09882f` "Fix hint popup not showing: attach logical parent to TopLevel"、メイン `9dac646` (submodule pointer 更新)
  - 背景: CodeCompleteHandler (入力時 hint) と PopupHandler (mouse-over) が同一 ToolTip (PopupTextBlock) を共有しており同時表示できなかった
  - `CodeEditor2/CodeEditor2/CodeEditor/PopupHint/HintPopupHandler.cs` を新規作成
    - caret 直下にアンカーした Avalonia `Popup` で hint popup を表示
    - `PlacementMode.AnchorAndGravity` + `PopupAnchor.TopLeft` / `PopupGravity.BottomRight` (AvaloniaEdit CompletionWindowBase と同じ方式)
    - offset 計算は `CalculateCaretRectangle() - TextView.ScrollOffset` → `TranslatePoint(Editor)` (CompletionWindowBase と同じ document→viewport 変換)
    - `CombinePopupItems` (複数 PopupItem を改行で結合) を PopupHandler から移管
    - `OpenPopup` / `ClosePopup` は UI thread 以外からの呼び出しに `Dispatcher.UIThread.Post` で対応
  - `CodeView.axaml.cs`:
    - TextEditor は XAML で子要素を持てない (TemplatedControl) ため、コンストラクタで `Popup` + `Border` + `TextBlock` (hintPopupTextBlock) をコード生成
    - `hintPopup` / `hintPopupTextBlock` フィールドと `HintPopup` / `HintPopupTextBlock` プロパティを追加
    - using に `Avalonia.Controls.Primitives` を追加
    - ホイールズーム時に `hintPopupTextBlock.FontSize` を追従
  - `Controller_CodeEditor.OpenPopup/ClosePopup` の routing 先を `codeViewPopup` (PopupHandler) から `codeViewHintPopup` (HintPopupHandler) に変更
  - `PopupHandler` を mouse-over 専用化: `OpenPopup` / `ClosePopup` / `CombinePopupItems` を削除、ToolTip pointer placement 復元コメントを整理
  - `CodeCompleteHandler.OnCaretPositionChanged()` を追加: caret 移動時に stale になった hint popup を閉じる (`CodeView.Caret_PositionChangedAsync` から呼ぶ)
  - ビルド成功 (CodeEditor2.csproj, 0 errors / VerilogPlugin 側の既存エラーは別修正の影響のため無視)

- SystemVerilogCore への抽象化移動 → Phase 10 (parser-backed adapter テスト / Verilog/* 残ファイル移動) が次のステップ
  - Phase 1: interface 群 + first-cut adapter 完了
  - Phase 2A: BuildingBlock / NamedElement adapter 実装完了 (TopLevelBlocks, Root, FindElementAt)
  - Phase 2B: FindDefinitionAsync / FindReferencesAsync を `Root.GetHierarchyNameSpace` + `NameSpace.GetNamedElementUpward` + `DataObject.UsedReferences/AssignedReferences` 経由で実装完了
  - Phase 3: クロスファイル参照を `ProjectProperty.DefinitionNameSpace` / `PackageNameSpace.GetFile` 経由で実装完了 (definition は取れる、cross-file references は declaration のみ)
  - Phase 4: Hover 強化を `HoverContent` + `IHoverContentProvider` + `PluginHoverInstaller` で実装完了
  - Phase 5: Diagnostic code map + 13 xUnit テスト追加
  - Phase 6: LSP エンドツーエンドテスト追加 (10 件, 合計 23/23 成功)
  - Phase 7: documentSymbol 強化 (Root.BuildingBlocks + Members ネスト) + テスト 3 件追加 (合計 26/26 成功)
  - Phase 8: adapter 振る舞いテスト 7 件追加 (合計 33/33 成功)
  - Phase 9: ドキュメント整備完了 (SystemVerilogCore / SystemVerilogLanguageServer README、.agents/overview.md の Project Structure 更新)
  - Phase 10: Verilog/* 残ファイル (Statement系, AutoComplete系) の SystemVerilogCore 移動 + parser-backed adapter テスト

## 完了済みタスク

- function call / let call 引数入力中の hint 表示の機能追加案を解析し `CodeEditor2VerilogPlugin/README.md` に修正案を追記
  - `ModuleInstantiation.cs` を参考に、function call 引数位置での hint 表示の不足を解析
  - コード確認で判明した現状の問題:
    - `ListOfArguments.ParseListOfArguments` は `completionContext` を引数で受けるが関数内で未使用 (`word.Eof` 分岐なし) → 引数入力中に hint が出ない
    - positional / named argument の `Expression.ParseCreate` に `completionContext` が未伝播
    - `FunctionCall.ParseCreate` 冒頭の `AppendExpression()` 相当がない (`ModuleInstantiation.ParseAsync` との非対称)
    - `ModuleInstantiation.parseOrderedPortConnections` (ordered接続) と `BuiltinMethodCall.ParseCreate` も `completionContext` 未対応
  - README に追記した修正案の構成:
    - 機能1: positional 引数位置で次の引数 `Port.GetLabel()` を `CarletPopupItems` に追加 (`word.Eof` パターン、括弧直後 + カンマ直後)
    - 機能2: named argument `.` 直後の未接続引数名補完 + `.name(` 直後の port hint
    - 機能3: function 名入力位置での `AppendExpression()` (statement parse 経路の伝播確認が必要と注記)
    - 機能4: 横展開表 (ordered port connection / BuiltinMethodCall / task call / Class constructor)
    - 設計上の注意 (EOF 早期 return の規律、constantConnected の扱い、検証方法)
  - コミット: CodeEditor2VerilogPlugin `d3b2c97` "Add enhancement proposal for function call argument hints in README"
- Verilog autocomplete / hint の部分parse + CompletionContext 仕組みを解析し `CodeEditor2VerilogPlugin/README.md` に追記
  - README.md に「Verilog autocomplete / hint 情報の生成仕組み (部分parse + CompletionContext)」セクションを新設
  - 全体フロー (TextEntered → ITextFile.GetAutoCompleteItems → Verilog.CompletionContext → 部分parse)
  - CompletionContext コンストラクタの3段階処理 (GetAutoCompleteTarget / GetDocumentRegionAt / 部分parse実行)
  - `completionContext != null && word.Eof` パターン (ModuleInstantiation の port label popup、Module.cs の keyword 絞り込み + instance snippet)
  - append 系メソッド一覧、CodeEditor2 側表示振り分け、mouse-over hint (GetPopupItem) との対比、注意点
  - 注意点の NameSpace 実登録挙動は GetNameSpace/GetHierarchyNameSpace の実装確認後に記述 (推定を排除)
  - ビルド成功 (CodeEditor2VerilogPlugin.csproj, 0 errors)
  - コミット: CodeEditor2VerilogPlugin `8bb102d` (作業ツリーに残っていた Expression parse 系への completionContext 伝播 + CodeDrawStyle 波線調整も同時コミット)
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
- ImportedPackage 階層parse で source file (pkg.sv) が再parseされない問題を修正
  - `Tool/ParseHierarchy.cs` の `parseDownwardAsync` で `verilogFile is ImportedPackage` のとき、その `SourceVerilogFile` をparseキューにenqueueするよう追加
  - これにより、 同じhierarchy parse cycle 内で package を定義するfileも再parseされ、 ImportedPackage 側のparse結果と整合する
  - コミット: CodeEditor2VerilogPlugin `02bd78a` "Enqueue ImportedPackage source file in hierarchy parse"
- Module / UDP instantiation parser を実装
  - `Verilog/Items/UdpInstantiation.cs` を新規作成
    - `NamedItem + IBuildingBlockInstantiation + INamedElement + IItem` を実装
    - BNF `udp_instantiation ::= udp_identifier [ drive_strength ] [ delay2 ] udp_instance { , udp_instance } ;` に従い、`udp_identifier` 解決 → `[ drive_strength ]` 任意 → `[ delay2 ]` 任意 → インスタンス並び を parse
    - `udp_instance ::= [ name_of_instance ] ( output_terminal , input_terminal { , input_terminal } )` を順序付き port connection として実装
    - `GetInstancedBuildingBlock()` で Primitive の parsedDocument を参照して input 候補 port 解決
    - `DriveStrength.CreateString()` の旧・新 `DriveStrength` 名前空間衝突を避け、`(strength)` プレースホルダで出力
  - `Verilog/Items/ModuleOrGenerateItem.cs` の `ParseAsync` を更新
    - `gate_instantiation` keywordで消費されない識別子が `Primitive` を指していた場合に `UdpInstantiation.ParseAsync` へ分岐
    - それ以外は従来どおり `ModuleInstantiation.ParseAsync`
  - ビルド成功 (CodeEditor2VerilogPlugin / RtlEditor2.Desktop どちらも 0 error)
  - コミット: CodeEditor2VerilogPlugin (このターンで作成予定)
- `.fileClassify` が Linux 環境で Navigate window で見えない問題を修正
  - 原因: `CodeEditor2/Data/DataAccess.cs` の `DirectoryInfo.EnumerateFileSystemInfos` で `EnumerationOptions` のデフォルト `AttributesToSkip` が `Hidden | System` になっており、Linux では `.` 始まりファイルが隠しファイル扱いされて列挙から除外される。Windows では `.` 始まりファイルに `Hidden` 属性が付かないため問題は顕在化しない
  - 内容: `DataAccess.GetFolderContents` (2 か所) と `DataAccess.UpdateFieSystemInfoAndSubItemAsync` の `EnumerationOptions` に `AttributesToSkip = FileAttributes.System` を明示的に指定し、`Hidden` のスキップを無効化
  - ビルド成功 (`RtlEditor2.Desktop.csproj`, 0 errors)
  - コミット: CodeEditor2 `87dbe51`

- Verilog コード表示時の Folding が fold マークを表示しない問題を修正
  - 原因:
    1. `CodeView.attachToCodeDocument()` で `_foldingManager = FoldingManager.Install(...)` した後、`UpdateFoldings()` が呼ばれていなかった。`SetTextFileAsync` 後のファイル表示時に fold データが空の folding manager が install されたままになっていた
    2. `Controller_CodeEditor.PostRefresh()` は `Redraw()` + `UpdateMarks()` のみで `UpdateFoldings()` を呼んでいなかった。parse 後に CodeDocument の Foldings が更新されても folding margin に反映されない
    3. `CodeDocument.CopyColorMarkFrom` の `UpdateFoldings()` 呼び出しが UI スレッドから呼ばれた場合のみ実行されるため、background parse thread からの呼び出し時に fold 反映が遅れていた
  - 内容:
    - `CodeEditor2/CodeEditor2/Views/CodeView.axaml.cs` の `attachToCodeDocument()` の末尾に `UpdateFoldings()` を追加
    - `CodeEditor2/CodeEditor2/Controller_CodeEditor.cs` の `PostRefresh()` に `Global.codeView.UpdateFoldings();` を追加
    - `CodeEditor2/CodeEditor2/CodeEditor/CodeDocument.cs` の `CopyColorMarkFrom` 内の `UpdateFoldings()` 呼び出しを、UI スレッドでない場合は `Dispatcher.UIThread.Post` で UI thread に post するよう更新
  - ビルド成功 (`RtlEditor2.Desktop.csproj`, 0 errors)
  - コミット: コードエディタ (このターンで作成予定)

- CompletionContext を活かした mouse-over / input-time hint popup を実装
  - `CodeEditor2/CodeEditor/CodeComplete/CompletionContext.cs` の `CarletPopupItems` / `MouseOverPopupItems` のコメントを修正 (input-time hint と mouse-over hint の役割を明記)
  - `CodeEditor2/Data/ITextFile.cs` に `PopupItem? GetPopupItem(ulong Version, int index)` を interface に戻し、mouse-over hint 用の軽量 lookup API としてコメントを追加 (UI thread から呼ばれるので heavy parsing は避けること)
  - `CodeEditor2/CodeEditor/CodeComplete/CodeCompleteHandler.TextEntered` で `CarletPopupItems` を popup 表示する際に `hintWorking = true` を立てる。また `CarletPopupItems` が空で auto-complete 動作中 (working == true) でない場合のみ `CloseHint()` を呼ぶよう修正
  - `CodeEditor2/CodeEditor/PopupHint/PopupHandler.cs` の `TextArea_PointerMoved` を軽量 API `TextFile.GetPopupItem(...)` 経由に戻し、`CombinePopupItems` helper を抽出して `OpenPopup` の重複ロジックも共通化
  - ビルド成功 (`RtlEditor2.Desktop.csproj`, 0 errors)
  - コミット: CodeEditor2 (このターンで作成予定)
  - メモ: 各プラグイン側 (例: Verilog) の `GetPopupItem` / `CarletPopupItems` / `MouseOverPopupItems` への出力は今後も必要だが、本ターンは CodeEditor2 (メインディレクトリ) 側の infrastructure のみ整えた

## Next Steps

- 動作確認: `func(` / `func(a, ` / `func(.p|` / `func(.p(` / `inst0(clk, ` / `task_call(` の各入力位置で hint popup・autocomplete dropdown が出ること (EditParse モードでのみ実効的に動作)
- 横展開候補: `UdpInstantiation` (ordered port connection) / GenerateBlock 内 statement 経路 / `BuiltinMethodCall` (呼び出し元が今後復活した場合) への completionContext 伝播
- 動作確認: 入力時 hint popup が caret 直下に表示されること (論理親接続修正の検証)、mouse-over popup との同時表示、caret 移動で hint popup が閉じること、auto-complete dropdown と衝突しないこと
- Phase 10: parser-backed adapter テスト
  - CodeEditor2VerilogPlugin の CoreBridge には Avalonia 依存があり、UI フリーなテストプロジェクトから直接参照できない
  - 代替: Plugin テスト用に Avalonia を headless でロードする別プロジェクトを作る (工数大)
  - または Phase 8 のように振る舞いを in-memory シナリオでテストする方針を維持
- Phase 10: Verilog/* 残ファイル (Statement系, AutoComplete系) の SystemVerilogCore 移動 (Avalonia 依存の分離)
- VerilogSystemVerilogCore.Wrap の呼び出し点を Plugin 側 (例: Plugin.cs や ParseHierarchy) から呼び、editor 上で CodeEditor2VerilogPlugin + LSP が同じ adapter を共有するシナリオを検証
- セッション全体のまとめ: 7 フェーズで SystemVerilogCore seam が完成し、LSP サーバは VSCode / Neovim / Helix から起動可能
- PopupHandler.OpenPopup を caret 位置 popup として実装
  - CodeEditor2/CodeEditor/PopupHint/PopupHandler.cs の OpenPopup(List<PopupItem>) を実装
  - 受け取った PopupItem 群を 1 つの PopupItem にまとめ (各 item 間は labelNewLine で区切り)、PopupTextBlock に描画
  - ToolTip.SetPlacement(Editor, BottomEdgeAlignedLeft) + VerticalOffset = caretRect.Height で caret 直下を anchor に ToolTip を表示
  - 一度 close してから reopen することで caret 移動に伴う再配置を確実にする
  - ビルド成功 (RtlEditor2.Desktop.csproj, 0 errors)
  - コミット: CodeEditor2 7ecd163 "Implement PopupHandler.OpenPopup at caret position"

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
  - コミット:
    - CodeEditor2VerilogPlugin: `97d12e6` "Map Verilog parser messages to LSP diagnostic codes"
    - SystemVerilogLanguageServer: `bc35ec6` "Expose in-memory project handle and InternalsVisibleTo for tests"
    - メイン: `8916199` "Add SystemVerilogLanguageServer.Tests with 13 LSP-bridge tests"

- SystemVerilogCore 抽象化 Phase 6: LSP エンドツーエンドテスト追加
  - `SystemVerilogLanguageServer.Tests/LspHandlerEndToEndTests.cs` 新規 (10 テスト):
    - didOpen / didChange / didClose の JSON ラウンドトリップ
    - didOpen → definition / hover / documentSymbol のフロー
    - hover off-symbol で null
    - 複数 file 管理
    - didOpen 再送で text 上書き
    - SymbolKind 数値仕様 (LSP spec) 確認
  - テスト合計: 23/23 成功
  - コミット:
    - メイン: `7476c1d` "Add end-to-end LspHandler tests covering didOpen / definition / hover"
    - メイン: `57b5258` "Update state.md after SystemVerilogCore Phase 6 (LSP e2e tests)"

- SystemVerilogCore 抽象化 Phase 7: documentSymbol 強化
  - `LspHandler.HandleDocumentSymbol` を `Root.BuildingBlocks` + 各 `BuildingBlock.Members` をネストした DocumentSymbol ツリーを返すよう更新
  - `DocumentSymbol.Children` プロパティを LSP 互換で追加
  - `InMemoryFile` に `AddBuildingBlock` / `AddMember` API を追加し、`RootBlock.BuildingBlocks` / `Members` が file テーブルを参照するように変更
  - テスト 3 件追加 (Module が root の子、Package の member が子の子、空のときに Children = null)
  - テスト合計: 26/26 成功
  - コミット:
    - SystemVerilogLanguageServer: `76e37f7` "Walk the building-block tree in documentSymbol"
    - メイン: `8b674e7` "Add documentSymbol tests covering nested building blocks and members"
    - メイン: `7f2ff1b` "Update state.md after SystemVerilogCore Phase 7 (documentSymbol)"

- SystemVerilogCore 抽象化 Phase 8: adapter 振る舞いテスト
  - `SystemVerilogLanguageServer.Tests/AdapterBehaviorTests.cs` 新規 (7 テスト):
    - Symbol range が half-open であることを検証
    - 異なる range の複数 symbol が個別に resolve できることを検証
    - file lookup が URI と AbsolutePath 両方で動作
    - project.Files が登録済み file を列挙し、再追加で重複しない
    - HoverContent が kind を保持して markdown を生成
    - diagnostics が空のとき空 list
    - documentSymbol が 3 階層の nested tree (top -> inner -> leaf) を生成
  - CodeEditor2VerilogPlugin の CoreBridge は Avalonia 依存があり直接参照できないため、振る舞いを in-memory シナリオで検証
  - テスト合計: 33/33 成功
  - コミット:
    - メイン: `d834feb` "Add adapter behavior tests for in-memory SystemVerilogCore"
    - メイン: `454d565` "Update state.md after SystemVerilogCore Phase 8 (adapter behavior tests)"

- SystemVerilogCore 抽象化 Phase 9: ドキュメント整備
  - `SystemVerilogCore/SystemVerilogCore/README.md` 更新:
    - Layout に `HoverContent` / `IHoverContentProvider` を追加
    - First-cut status を完了済み / 進行中の表に書き換え
    - Diagnostic codes セクションを追加 (verilog/undriven などのマッピング表)
    - Extending hover セクションを追加 (custom provider の使い方)
  - `SystemVerilogLanguageServer/SystemVerilogLanguageServer/README.md` 更新:
    - Status を capabilities 別の表に書き換え
    - Building にテスト追加
    - Hover / documentSymbol 出力例を追加
    - Architecture 図を更新
    - Editor integration セクション追加 (VSCode / Neovim / Helix のサンプル)
  - `.agents/overview.md` 更新:
    - Project Structure に SystemVerilogCore / SystemVerilogLanguageServer / SystemVerilogLanguageServer.Tests を追加
    - サブモジュール一覧に CodeEditor2VerilogPlugin の CoreBridge 役割を追記
    - 「関連プロジェクト (メインディレクトリ)」テーブルを新規追加

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
