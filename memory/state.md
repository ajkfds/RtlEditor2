## Latest Agent Session
- **Last Updated**: 2026-07-09 - Agent session active

- **Current focus**: Waiting for user instructions
- **Project**: RtlEditor2 - Avalonia UI based RTL IDE
- **Notable topics**: UI thread lock issues, Verilog/SystemVerilog parsing, TreeControl, NavigatePanel
- **Session Summary**:
  - Project structure: Avalonia UI RTL IDE with modular plugin architecture
  - Main issue categories: UI thread locks, SystemVerilog parse errors (11+ known bugs), TreeControl issues
  - Fixed issues: TreeControl flickering, ChatControl font, ParseHierarchy cancel, ShowDialog position, Struct undriven warning, Port.BitWidth double calculation
- **Agent initialized**: 2025-02-14 - AGENTS.md loaded, ready to process tasks

**UIスレッドロック問題 確認結果 (2025-02-12)**:
- `TextFile.UpdateAsync`: `InvokeAsync()` → `Post()` に変更済み ✅
- `Updater.UpdateAsync`: UI thread check追加済み (Task.Runにディスパッチ) ✅
- `VerilogHeaderInstance.UpdateAsync`: 親ファイルへのUpdate伝搬がまだ存在する ❌
- `AcceptParsedDocumentAsync`: `UpdateAsync`呼び出しチェーンがまだ存在する ❌
- `semaphore競合`: Background Task間の競合がまだ発生する ❌

---

## 修正履歴: @scope VirtualScopeNameSpaceの動的解決 (2025-02-14)

**問題**:
`@scope` による参照は autoComplete 時には機能しているが、実 RTL parse 時で "unbound object" / "unfound object" エラーが出る。

**原因**:
1. `VirtualScopeNameSpace` の `VirtualScopeTarget` が `init`-only プロパティで後から更新不可
2. `CommentAnnotationItem.parseScopeAnnotation` で初回 parse 時にターゲット BuildingBlock が未パースの場合、`VirtualScopeTarget == null` の `VirtualScopeNameSpace` を `module.NamedElements` に登録
3. 再 parse 時、`alreadyRegistered` チェックで既存 VirtualScopeNameSpace が保護され、`VirtualScopeTarget` が更新されない
4. `VirtualScopeNameSpace.NamedElements` getter は `target==null` のとき `base.NamedElements`(空) を返すため、`DATA_I` などの子要素が見つからず "unfound object" エラー

**修正内容**:
1. `NameSpace.VirtualScopeTarget` を `init` から `set` 可能に変更 (NameSpace.cs)
2. `VirtualScopeNameSpace.NamedElements` getter を動的解決に変更:
   - `target==null` の場合、`SourceCommentScopeReference` から `BuildingBlockName` を取得し、`ProjectProperty.GetBuildingBlock()` で lookup
   - 解決できたら `VirtualScopeTarget` を更新して委譲
   - 親 `BuildingBlock.File.ProjectProperty` または `Project.ProjectProperties` から ProjectProperty を取得
   - `pluginVerilog.ProjectProperty` にキャストして `GetBuildingBlock` 呼び出し
3. `VirtualScopeNameSpace.UpdateTarget(BuildingBlock)` メソッド追加 (NameSpace.BuildingBlock の protected setter を回避)
4. `CommentAnnotationItem.parseScopeAnnotation` の登録処理を見直し:
   - `alreadyRegistered` チェックの代わりに毎回 `nameSpace.NamedElements` を確認
   - 既存 VirtualScopeNameSpace があれば `UpdateTarget()` で `VirtualScopeTarget` を更新
   - これで再 parse 時に正しくターゲットが bind される

**対応する構文**:
```verilog
// @scope MY_MODULE u_inst
wire [7:0] aa = u_inst.SIG;  // 初回 parse、再 parse ともにエラーなく解決
```

**修正ファイル**:
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/NameSpace.cs`
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/VirtualScopeNameSpace.cs`
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/Items/CommentAnnotationItem.cs`

**ビルド結果**:
- `CodeEditor2VerilogPlugin.sln` ビルド成功 (657 警告, 0 エラー)

---

## 修正履歴: let 宣言の引数なし呼び出し parse 修正 (2025-02-14)

**問題**:
`let` で定数を設定した宣言 (引数なし) および引数あり `let` を引数なしで呼び出したときに、正しくparseされない。

**原因**:
1. `LetDeclaration` が `IPortNameSpace` を実装していないため、`FunctionCall.ParseCreate` から `ListOfArguments.ParseListOfArguments` に port 情報を渡せず、let呼び出し時のport チェックが完全に抜けていた。
2. `FunctionCall.ParseCreate` の `if (word.Text != "(")` 節では `function` の port チェックはあるが `letDecl` の port チェックがなく、デフォルト引数なし `let` を括弧なしで呼び出してもエラーにならなかった。

**修正内容**:
1. `LetDeclaration` に `IPortNameSpace` を実装させ、`Ports` / `PortsList` プロパティを追加 (`EnsurePortsBuilt` で `LetPortItem` から `Port` に変換)
2. `FunctionCall.ParseCreate` の `if (word.Text != "(")` 節に `letDecl.Ports.Count != 0` 時のデフォルト引数チェックを追加
3. `FunctionCall.ParseCreate` の `(` ありのケースで `portNameSpace` に `letDecl` も渡す

**修正ファイル**:
- `CodeEditor2VerilogPlugin/.../Verilog/DataObjects/LetDeclaration.cs`
- `CodeEditor2VerilogPlugin/.../Verilog/Expressions/FunctionCall.cs`

**ビルド結果**:
- `CodeEditor2VerilogPlugin.sln` ビルド成功 (657 警告, 0 エラー)

---

## 修正履歴: Primary 内で letDeclaration を引数なしで呼んだときの parse 修正 (2026-07-09)

**問題**:
`let` で定義した定数 (引数なし) を三項演算子などで使用すると parse エラーが出る。
例:
```verilog
let CONST_ONE = 8'h01;
let CONST_ZERO = 8'h00;

always_comb begin
    result = flag ? CONST_ONE : CONST_ZERO;
end
```
「Primary 内で CONST_ONE を parse したときに namespace が letDeclaration になるが、引数がない場合に正しく parse できない」

**原因**:
`Primary.searchNameSpace` 関数が NameSpace 要素 (letDeclaration を含む) を見つけたとき、
その識別子名が `.` で続かない場合 (`word.Text != "."`) でも `word.MoveNext()` で識別子を消費し、
return newNameSpace していた。
このため呼び出し元 (Primary.parseCreate) では:
- 識別子 (`CONST_ONE`) は消費済みで `word.Text == ":"` (次のトークン)
- `targetNameSpace.NamedElements.ContainsKey(word.Text)` は `false`
- 結果として `Primary.parseCreate` は null を返し、parse 失敗

**修正内容**:
`Primary.searchNameSpace` 関数で、見つかった NameSpace 要素が `Function` または `LetDeclaration` で、
次のトークンが `.` でない場合 (つまり呼び出しや単独参照の場合) は、
word を進めることなく元の nameSpace を返すように変更。
呼び出し元の `Primary.parseCreate` の `!lValue && (element is Function || element is LetDeclaration)` 判定が
正しくトリガされ、`FunctionCall.ParseCreate` で let 呼び出しとして処理される。

**修正ファイル**:
- `CodeEditor2VerilogPlugin/.../Verilog/Expressions/Primary.cs`

**ビルド結果**:
- `CodeEditor2VerilogPlugin.csproj` ビルド成功 (657 警告, 0 エラー)
- コミット: `24e533d Fix Primary namespace search for Function/LetDeclaration without parentheses`

---

## 修正履歴: package import 時の dataobject Defined フラグ設定 (2026-07-09)

**問題**:
`package` を `import` したとき、dataobject (Variable, Net, Parameter 等) を import しても `Defined` フラグが `false` のままで、参照時に「not defined here」エラー (Undefined access) 扱いになる。

**原因**:
`PackageImportDeclaration.Parse` で package の `NamedElements` から取得した dataobject を `nameSpace.BuildingBlock.NamedElements` に登録する際、`Defined = true` を設定していなかった。
特に prototype parse 段階で、まだ package 内の要素が `Defined = false` である状態で import されると、import 先でも `Defined = false` のまま参照される。

**修正内容**:
`PackageImportDeclaration.Parse` で dataobject を import する際に明示的に `Defined = true` を設定:
1. Wildcard import (`import PKG::*;`) の `foreach` ループ内で `if (namedElement is DataObjects.DataObject dataObject) dataObject.Defined = true;` を追加
2. 個別 import (`import PKG::ITEM;`) でも `targetElement` が `DataObject` 型の場合に `Defined = true` を設定

**対応する構文**:
```verilog
package TEST_PKG;
typedef struct packed {
    logic        VALD;
    logic [7:0]  DATA;
} GBUS;
endpackage

module TEST_MODULE
import TEST_PKG::*;  // ここで GBUS (DataObject) が Defined = true で import される
;
    GBUS g;  // 参照時 "not defined here" エラーが出なくなる
endmodule
```

**修正ファイル**:
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/Items/PackageImportDeclaration.cs`

**ビルド結果**:
- `CodeEditor2VerilogPlugin.sln` ビルド成功 (657 警告, 0 エラー)

---

## 修正履歴: ParseHierarchy での @scope 参照先の自動 parse (2026-07-10)

**問題**:
`@scope` で参照された BuildingBlock が他ファイルにある場合、ParseHierarchy による下向き parse ではその参照先 file が parse 対象に入らず、下層 instance まで自動的に parse されない。
結果として、`VirtualScopeNameSpace` がその BuildingBlock 配下の instance をたどれず、`u_inst.SIG` のような下層参照が parse できない。

**原因**:
`ParseHierarchy.parseDownwardAsync` は `verilogFile.Items` を再帰的に下って `VerilogModuleInstance` を enqueue する仕組みだが、
`@scope` で参照される BuildingBlock は現在のファイルには存在しないので、この chain には載らない。
そのため、参照先 file の parse 自体がトリガされない。

**修正内容**:
`ParseHierarchy.parseDownwardAsync` に `@scope` 参照先 file を parse queue に enqueue する処理を追加。
1. 新しいヘルパー `EnqueueScopeReferencedFiles` を追加。
   - 各 BuildingBlock の `CommentScopeReferences` を walk
   - `ProjectProperty.GetFileOfBuildingBlock(scopeRef.BuildingBlockName)` で参照先 file を lookup
   - 重複 enqueue 防止のため `HashSet<TextFile>` で per-call 追跡
   - `completeIds` (ConcurrentDictionary) により同一 file の多重 enqueue も防止
2. `parseDownwardAsync` で、verilogFile が `Data.VerilogFile` の場合のみ、
   自身 parse 後に `vParsedDocument.Root.BuildingBlocks.Values` を
   `EnqueueScopeReferencedFiles` に渡す
3. `parseMode == ThisFileOnly` の場合はスキップ
   (`@scope` は本質的に cross-file 参照のため)
4. `targetFile is CodeEditor2.Data.TextFile` ガードで
   `VerilogFile` 以外 (header instance 等) は除外
5. `GetBuildingBlock` ではなく `GetFileOfBuildingBlock` を使うことで、
   参照先 file が未 parse でも file 自体は enqueue できる
   (file parse の中で `RegisterBuildingBlock` が呼ばれ、
    `VirtualScopeNameSpace` の late binding がその後に解決可能になる)

**対応するシナリオ**:
```verilog
// File: TEST_RTL_MODULE_INSTANCE.sv
module TEST_RTL_MODULE_INSTANCE;
    // @scope TEST_RTL_MODULE  ← TEST_RTL_MODULE.sv 内を参照
    wire [7:0] aa = u_inst.DATA_I;  // u_inst 配下も parse される
endmodule
```

**修正ファイル**:
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Tool/ParseHierarchy.cs`

**ビルド結果**:
- `CodeEditor2VerilogPlugin.sln` ビルド成功 (657 警告, 0 エラー)
- コミット: `379182c ParseHierarchy: enqueue @scope-referenced files into parse queue`
  (旧: `e58256c` -- コミットメッセージ末尾の `</body></replace>` ゴミを amend で除去)

---

## 修正履歴: @scope 参照先 file の EditParse 時自動 parse (2026-07-10)

**問題**:
エディタでソースを編集中 (EditParse モード)、`@scope` 参照先の BuildingBlock が未 parse の場合、
参照先 file の module 内で使う identifier (`u_inst.SIG` など) が parse できずエラーになる。
`ModuleInstantiation.Parse` には EditParse 時に instance 対象を 1 階層下まで parse する処理があるが、
`@scope` には同様の処理がなかった。

**原因**:
`CommentAnnotationItem.parseScopeAnnotation` (現 `parseScopeAnnotationAsync`) は、
`@scope` 参照時に `word.ProjectProperty.GetBuildingBlock(...)` を呼んで参照先を解決しようとするが、
EditParse モード時に参照先 file が未 parse だと null が返り、`VirtualScopeNameSpace` の `target` が
バインドされないまま登録されてしまっていた。

**修正内容**:
`ModuleInstantiation.Parse` の EditParse 時処理と同様の仕組みを `@scope` にも適用:
1. `CommentAnnotationItem.Parse` を `CommentAnnotationItem.ParseAsync` に非同期化
2. `parseScopeAnnotation` を `parseScopeAnnotationAsync` に非同期化
3. `parseScopeAnnotationAsync` 内で `EditParse` モードかつ参照先 BuildingBlock が null のとき、
   `ProjectProperty.GetFileOfBuildingBlock` で参照先 file を取得し、
   `SingleHierParse` モードで parser を作成、`ParseAsync` → `AcceptParsedDocumentAsync`
4. その後、参照先 file が `ProjectProperty` に登録されたので `GetBuildingBlock` が解決可能になる
5. `Module.cs` で `CommentAnnotationItem.Parse` の呼び出しを `await CommentAnnotationItem.ParseAsync` に変更

**注意**:
- `Task` 型の名前衝突を避けるため `System.Threading.Tasks.Task` を完全修飾で指定
  (`Microsoft.VisualStudio.Threading` パッケージの `Task` が優先されるため)
- `string.IsNullOrEmpty(scopeRef.InstanceName)` ガードは撤廃
  -- InstanceName 指定時も参照先 file を parse すれば、その file 内の instance も parse されるため

**修正ファイル**:
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/Items/CommentAnnotationItem.cs`
- `CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/Verilog/BuildingBlocks/Module.cs`

**ビルド結果**:
- `CodeEditor2VerilogPlugin.sln` ビルド成功 (657 警告, 0 エラー)
- コミット: `f7dd5be CommentAnnotationItem: parse @scope target file on EditParse (like ModuleInstantiation)`

---

## Status
- [x] Read AGENTS.md - Ready to process tasks
- [x] **@scope VirtualScopeNameSpace 動的解決 修正完了 (2025-02-14)**
  - ビルド成功、未コミット
- [x] **let 宣言の引数なし呼び出し parse 修正 (2025-02-14)**
  - ビルド成功、未コミット
- [x] **Primary 内 letDeclaration を引数なしで呼んだときの parse 修正 (2026-07-09)**
  - ビルド成功、コミット済み (`24e533d`)
- [x] **package import 時の dataobject Defined フラグ設定 (2026-07-09)**
  - ビルド成功、未コミット
- [x] **ParseHierarchy での @scope 参照先の自動 parse (2026-07-10)**
  - ビルド成功、コミット済み (`379182c`)
- [x] **@scope 参照先 file の EditParse 時自動 parse (2026-07-10)**
  - ビルド成功、コミット済み (`f7dd5be`)
- **Next Steps**:
  - 待機中
