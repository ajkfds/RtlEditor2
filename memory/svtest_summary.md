# sv-tests 関連 実装項目まとめ

`memory/state.md` に点在している sv-tests (SystemVerilog テストスイート) 関連の
実装・修正項目をカテゴリ別に整理したリファレンス。

---

## 1. 現状ステータス

- 対象: `CodeEditor2VerilogPlugin/verilog_sample/sv_tests` (全 1034 ファイル)
- 計測コマンド:
  `dotnet test SystemVerilogLanguageServer.Tests/SystemVerilogLanguageServer.Tests.csproj --no-build --filter "FullyQualifiedName~ParseSvTestFile"`
- 現状: **1032 合格 / 2 失敗** (regression なし)
- 着手前 (最古の集計): 673 合格 / 361 失敗 / diagnostic 343 Error
- 残る失敗 2 件 (いずれも parser 実装課題ではない):
  - `5.6.4--compiler-directives-include`: `include "/dev/null"` (Linux 絶対パス、Windows 環境依存)
  - `24.3--program`: トークナイザ offset drift (WordPointer.fetchNext のコメント/トークン長計算と上位 parser オフセット計算の相互作用。修正はリスク高のため保留)

---

## 2. テストハーネス (基盤)

- `SystemVerilogLanguageServer.Tests/SvTestsParserHarness.cs` / `SvTestsParserTests.cs` を新設
  (commit: メイン `8da2492`)
- 共有 `TestProject` に sv_tests 配下の全 .sv/.svh を相対パス (ディレクトリ構造保持) で登録
  → include 相対パス解決 (file not found 107 → 3 件、+8 合格、commit: メイン `f831a2e`)
- UVM 依存テスト除外: `IsUvmTest` helper (`uvm_pkg` / `uvm_macros.svh` / `sv_tests/uvm/` / ファイル名 "uvm")
  → `[UVM-SKIPPED]` 記録 (Error diagnostic 343 → 243)
- `:defines:` メタデータのマクロ登録: `ParseDefinesMetadata` helper (commit: メイン `5880d52`)
  → `5.6.4--compiler-directives-preprocessor-macro_1.sv` 解消
- 名前空間リセット: `ResetNameSpaces` 新設 (commit: メイン `b3f1377`)
  各テスト parse 前に ProjectProperty (DefinitionNameSpace / PackageNameSpace / UnitNameSpace / Macros) を作り直し
  → 26.2--package-decl / 26.3--package-ref ほか解消
- diagnostic 集計レポート (`sv_tests_report.txt`、種別別件数 + メッセージ別 top40)
- loose モード: positive は parse 完了のみ検証、negative は diagnostic 記録のみ

---

## 3. プリプロセッサ / マクロ系 (最多カテゴリ)

### 3.1 マクロ展開
- 引数付きマクロ呼び出しの閉じ括弧 `)` 消費漏れ修正 (commit: `1362d6d`)
- formal argument の default 値 (`a=5`) / 空実引数 (`MACRO1( , 2, )`) 対応 (commit: `bbc3b8a`)
- token pasting ``` `` ``` / stringification ``` `" ``` 対応 (22.5.1--define-expansion_26)
- 実引数が仮引数より少なく残り全仮引数にデフォルト値がある場合の補完 (IEEE 1800-2017 22.5.1)
- マクロ呼び出しの `)` 二重消費削除 (returnHierarchy 復帰後の MoveNext 1 トークン進行)
- word-boundary 置換 (`Macro.ReplaceArgument`) による誤置換防止

### 3.2 事前定義マクロ
- `SV_COV_*` 13 種 (`SV_COV_START`/`STOP`/`RESET`/`CHECK`/`MODULE`/`HIER`/`ASSERTION`/`FSM_STATE`/`STATEMENT`/`TOGGLE` 等) を `predefinedMacros` テーブルで展開 (commit: `7134fc3`、20.14--coverage の unsupported macro call 消滅)
- `` `__FILE__ `` / `` `__LINE__ `` 事前定義マクロ対応 (IEEE 1800-2017 22.5.3、5.6.4--compiler-directives-debug)

### 3.3 コンパイラ指令
- `` `line ``: `parseLineDirective` 新設 (number=正整数 / filename=文字列リテラル / level=0,1,2 必須。22.12--line-illegal-1〜5)
- `` `timescale ``: `validateTimescaleDirective` 新設 (magnitude=1/10/100、precision は unit 以上に精細。22.7--timescale-basic-3/4)
- `` `unconnected_drive ``: `validateUnconnectedDriveArgument` 新設 (pull0/pull1 のみ。22.9--unconnected_drive-invalid-1/2)
- `` `nounconnected_drive ``: 同一行にトークンがあれば引数エラー (22.9--unconnected_drive-invalid-3)
- `` `pragma ``: pragma_name が同一行にあるか検証 (22.11--pragma-invalid)
- `` `default_nettype ``: "not supported" エラー削除 + tri1/uwire 認識追加
- `` `line `` / `` `unconnected_drive `` を MoveNextUntilEol に変更、`` `nounconnected_drive `` は消費のみ
- `` `pragma `` / `` `line `` 指令後の行ずれ (illegal module item) 修正 (commit: `d7c542e`)
- `` `line `` / `` `unconnected_drive `` / `` `nounconnected_drive `` の負例検証 (NEG-NOT-REJECTED 10 件解消)
- `` `resetall `` 負例未検出群 (残課題)

### 3.4 include
- `parseInclude` の project-relative path を `/` 区切りに統一 + SearchFile フォールバック
- `VerilogHeaderInstance.Create` の include 対象を `VerilogHeaderFile` 限定 → `CodeEditor2.Data.TextFile` に緩和 (.sv/.v の include 解決)
- `Item.GetItem` の区切り検出を `IndexOfAny(['/', Path.DirectorySeparatorChar])` に変更 (commit: `ae172ee`、illegal file 6→0)
- `` `include `macro(args) `` 対応 (マクロ展開テキスト `` `"the_file`" `` → "the_file")
- 残課題: `` `include `foo(dummy_include.sv) `` の `" expected` (二重引用符内マクロ展開)

### 3.5 `ifdef` / マクロ展開連鎖
- `` `else `` 側 gate instantiation (`and a1 (a,b,c);`) の `22.6--ifdef-behavioral` 解消 (GateInstantiation NInputGate で implicit net 許容)
- 残課題: 22.5.1--define-expansion_1〜14 の `initial $display(...)` が "illegal initial construct" + "endmodule expected" (parseMacro の macro document 積み上げ後の上位 parser 受け渡し経路)
- 残課題: 22.5.1--define-expansion_24 の文字列内マクロ展開
- 残課題: `22.6--ifdef-behavioral` の一部 (illegal port identifier)

### 3.6 module 外 (ファイルスコープ) の `` `define `` 重複
- `WordScanner.recheckWord()` の副作用による複数回処理が原因
- `Clone` 用 `suppressInitialDirective` 引数付き internal コンストラクタ新設
- `Macro.DefineIndex` 追加 + 重複判定を「処理中の指令が既登録より厳密に後の位置」に変更 (commit: `30ef362`)

---

## 4. class / interface class 系

### 4.1 virtual class / class メソッド修飾子
- `virtual class base_cls;` の virtual interface 誤解釈修正 (commit: `58d3b9c`)
  - `PackageOrGenerateItemDeclaration.Parse` 冒頭に `virtual` + `class` 早期分岐
  - `ClassItem.Parse` に `virtual class` 分岐
- `virtual function` / `virtual task` (method_qualifier) 対応 (commit: `58d3b9c`、virtual interface type required 15件解消)
  - `ClassItem.Parse` 冒頭で `virtual` + `function`/`task` 分岐
- `extern` 直後の method_qualifier (`virtual`/`static`/`protected`/`local`/`pure virtual` 組合せ) 消費 (commit: `fdb340f`)
- 括弧なし function prototype 対応 (`function data_type_or_void function_identifier ;`)
- implements の interface 継承メソッド再宣言 (override) の duplicate 誤発火修正 (commit: `7d5515f`、8.26.7--partial_implementation)

### 4.2 interface class
- interface class 本体内 typedef の NamedElements 登録 (8.26.3系)
- `ihello::int_t` (interface class 型アクセス / scope resolution) 対応 (parseInterfaceClassScopeType)
- `interface class base_ic #(int N = 8, type T = string)` の type parameter 登録
- `ihello iobj;` (interface class 型変数宣言) → `InterfaceClassVariable` 新設
- Root level の `interface class` 宣言 dispatch 修正
- InterfaceClass → UnitNameSpace.Register (commit: `fa2818a`)
- 8.26.5--cast_between_interface_classes / implemented_class_handle

### 4.3 class 前方宣言 / 相互参照
- `Class.DeclaringNameSpace` 追加 + DataTypeFactory fallback 強化 (commit: `b239721`、8.27--forward_declaration)
- `typedef class A;` 前方宣言の偽エラー ("data type expected") 修正 (commit: `f7cc9d0`、Typedef.cs + InterfaceClass.cs)
- class 宣言を含むファイルの SimulationSetup unfound (searchHier の Class 解決) 修正 (commit: `9986751`)
- class から object 生成時の循環参照 StackOverflow (`Object.Create` の recursive `defineElements` 削除、commit: `bb82a8c`)
- `typedef class` 前方宣言経由 UserDefinedType 循環 → 対応不要と確認

### 4.4 class parameter / type generics / extends / implements
- parameter port list の `type` keyword 分岐 (`#(type T = real)`、commit: `e917129`、6.20.3)
- `ParseCreateTypeAssignmentsForPort` (type list_of_type_assignments) 新設 (type parameter を Typedef 登録)
- `#(int N, int P)` 形式 (`data_type list_of_param_assignments`) を Class/Interface/InterfaceClass/Program に横展開 (commit: `15bfd49`)
- prototype 分岐での `constants.Defined = true` (commit: `aa6e941`、8.8--typed_constructor_param / 8.25.1 / class_test_53)
- extends の package scope 解決 (Pkg::B / $unit.B)
- extends の parameter_value_assignment を `ParameterValueAssignment.ParseCreate` に置換
- implements の package scope (`Pkg::Bar#(1,2)`) + parameter_value_assignment 消費 (commit: `319149e`、class_test_27)
- `ClassName#(...)::member` の `#(...)` 消費 (NameReference)
- class の parameter_value_assignment (`C #(int) obj`) 対応 (commit: `3ade0b1`)

### 4.5 class スコープ / member 解決
- class 内 typedef (type_declaration) 対応 (commit: `aa15b53`、ClassItem.cs)
- extern constraint クラス内宣言 / 外部 constraint 宣言 (`constraint Class::name`)
- out-of-block method 定義 (`function void test_cls::test_method(...)`、commit: `ddf2d6f`)
  - Function.Parse / Task_.Parse に class_scope 分岐
- class スコープの `this.<built-in method>` (`this.srandom(seed)`) 対応 (commit: `dde0e68`)
- class item の parameter/localparam type (commit: `156abbd`、class_test_53)
- `super` 暗黙オブジェクトの解決 (`super.method()`、commit: `2aa89e7`)
- object 内 function call (`obj.myFunc(...)`、commit: `4aa1c74`) / task call (commit: `55b97b0`)
- chained method call (`obj.getObj().method()`、commit: `fb98130`)
- object の built-in method call (commit: `52e22eb`)

### 4.6 その他 class 系
- class 内 variable の prototype Defined 設定 (commit: `a4c45b2`、8.7--constructor)
- class 内 enum 定数の class object 経由参照 (`8.5--properties_enum`、commit: `a1b4761`)
- class 内 virtual interface 型 / interface class の変数宣言対応
- 残課題: 18.5.2--pure-constraint / 8.21--abstract_class / 8.22--dynamic_method_lookup / 8.26.6.x

---

## 5. constraint / randomize 系

- `randomize() with { constraint_block }` 未対応修正 (commit: `9315fd9`、skipConstraintBlock helper)
  - `with ( expression )` (配列 locator) と `with { ... }` / `with (identifier_list) { ... }` の両形式
- foreach constraint loop 変数 (`foreach ( B[i] ) B[i] == 5;`) の bindLoopVariable 追加
- dist 制約 (`b dist { 3 := 1, ... }`) の skipDistList
- extern constraint のクラススコープ解決 (`constraint ClassName::c { ... }`、commit: `a7759b1`)
- 外部 constraint 宣言 (extern_constraint_declaration)
- 18.5.8.2--array-reduction-iterative-constraints (固定長 unpacked array の配列メソッド登録)
- 残課題: 18.5.2--pure-constraint_2 / 18.5.8.1 (foreach constraint の `item` 参照) / 18.5.14.1 (一部) / constraint 内 cross-object member access (`aObj.v < v;`)

---

## 6. sequence / property / assertion 系

### 6.1 sequence repetition range
- `[*m:n]` / `[=m:n]` / `[->m:n]` の range 形式対応 (commit: `0d9a4f8`、16.9 系3件解消)
  - `SequenceExpr.TryParseRepetitionCountRange` helper / goto 単一トークン `->` 対応 / `DataObjectReference` の sequence repetition 非消費
- `##1` / `##2` 形式 (`## constant_primary`) を SingleValue として数値を消費

### 6.2 event / property
- event control の `iff` 対応 (`always @(posedge clk iff en == 1)`、commit: `8174034`)
- property の `iff` 対応 (`assert property (@(posedge clk) a iff b)`)
  - `SequenceExpr.ParseCreate` ループ冒頭で property 専用二項演算子 (iff/implies/until/s_until/until_with/s_until_with) を break
  - `PropertyExpr.ParseCreate` で `ParseBinaryOperator` を SequenceExpr より先に判定
- `@seq` (sequence identifier への event control) 対応 (commit: `c853a17`、SequenceReference 新設)
- 16.14--assume-property / 16.2--assert0/assume0/cover0/assert-final/assume-final/cover-final
- module 直下 concurrent / deferred immediate assertion の dispatch (commit: `39e1e23`)

### 6.3 event trigger
- 非ブロッキングイベントトリガ `->>` のトークナイザ / dispatcher 対応 (commit: `e0904c1`)
- `repeat ( expr ) event_control` の代入 RHS 対応 (commit: `88df34f`、9.4.5 系4件)
- 代入 RHS の `#` delay / `@` event control (BlockingAssignment / NonBlockingAssignment の ParseDelayOrEventControl)
- 残課題: 16.10--sequence-local-var / 16.15--property-disable-iff / 16.12--property-iff (一部)

---

## 7. array / index / 組み込みメソッド系

- 固定長 unpacked array の配列メソッド登録 (sum/product/find/min/max/... + size)
- associative array delete (`map.delete("sad")` / `map.delete`、commit: `479c1de`)
- queue/array range slice (`q[0:$-1]` 等) → RangeExpression.ParseCreate に置換 (commit: `194f4f2`)
- array locator method の `with` 句 (`s.find with (item == "sad")`、commit: `d96a522`、find系6件)
- iterator index querying method (`item.index`/`first`/`last`/`next`/`prev`、commit: `ab70a6d`)
- 配列 reduction method のキーワード名 (`b.or` / `b.and` / `b.xor`、commit: `7c22966`)
- 組み込みメソッドのデフォルト引数 (`enum.next()` / `enum.prev()`、commit: `76819a2`)
- associative/dynamic/queue array の index select (commit: `c84fcac`)
- dynamic array の size/delete を ArrayMethods に統一 (commit: `af6a3f7`)
- NameReference の `namedElement is DataObject` 分岐追加 (queue/enum 等の built-in method 解決)
- `string.atoreal()` 等 built-in method call の対象を `DataObject` に緩和 (commit: `980d171`)
- mailbox member method (parameterized `mailbox #(string)`)
- semaphore / wait_order 対応 (commit: `37fb2e8`)
- process class member method (array 要素経由の await/kill/suspend/resume/status)

---

## 8. nettype / interconnect / net

- module 直下 nettype 宣言 dispatcher 接続 (commit: `20150ed`、6.6.7--nettype)
- interconnect 登録の prototype/real 2パス (二重登録) 対応 (commit: `5507bf6`)
- module 直下 interconnect 宣言の dispatcher 接続 (commit: `c85ab9e`)
- 残課題: 6.6.8--interconnect の module instance port 接続での interconnect 解決

---

## 9. system task / system function

- `$test$plusargs` / `$value$plusargs` の式内使用 (commit: `97b82bb`、21.6)
- `$bits(int)` / `$typename(type)` 等 data_type 引数 (commit: `83de74e`、class_test_51 / 20.6)
- system task 登録追加 ($fread/$fgets/$fscanf/$fpos/$sscanf/$dumpports/$cast/$coverage_control 等)
- `$root.a.b` top-level scope 参照 (RootReference 新設) + coverage 系 task (commit: `3e2f76e`、20.14)
- `std::randomize( [vars] ) [ with { ... } ]` (StdRandomizeCall 新設、18.12 系)
- 空実引数 (`$display(a,,b,,c)`) 対応
- 6.23--type_op / type_op_compare の `type(...)` operator は対応済み (残課題なし)

---

## 10. data type / struct / union / enum / typedef

- union 未対応 → UnionType 新設 (StructType 派生、commit: `35058c3`)
  - StructType.parseCommon 抽出 (struct/union 共通)、DataTypeEnum.Union
- typedef struct/union packed 構文 (commit: `c43ed69`、失敗15件)
  - 前方宣言 (`typedef identifier ;` / `typedef struct/union/enum identifier ;`)
  - `type_identifier { variable_dimension }` (`typedef reg quartet[3:0];`)
  - package scope type (`pkg::type`)
  - StructType.parseCommon のメンバーループ停止条件修正 (reg/logic 等の型キーワードで誤終端しない)
- tagged union 対応
  - void member (`void Invalid;`) の StructType 対応 (commit: `6ce3de1`)
  - tagged_union_expression (`tagged Invalid;` / `tagged Valid(42);`、TaggedUnionExpression 新設)
  - case ... matches の case_pattern_item (`tagged a '{.v, 0}`、CasePattern.cs 新設)
- enum
  - enum_name_declaration の単一 index 形式 (`enum { a=10, b[10] }`、commit: `a30dc71`)
  - 2-state 基底型 / x/z 値の検証 (`6.19--enum_xx_inv` / `enum_xx_inv_order`、NEG)
  - class 内 enum 定数の class object 経由参照 (commit: `a1b4761`)
- struct member の unpacked dimension (`int a, b[4];`)
- struct 初期化 `'{"` (AssignmentPatternWithKey / WithoutKey)
  - 位置指定 / 名前指定 / default / repetition / 型付き (`int'{1,2,3}`)
  - bracket index array_pattern_key (`'{[2]:1, [0]:0}`、commit: `1bff9fb`)
  - parameter/localparam 初期化の assignment pattern (commit: `7ade089`)
- parameter の unpacked_dimension (`parameter aggregate`、commit: `a521b11`)

---

## 11. statement / 制御構文

- case_generate_construct の dispatcher 接続 (ModuleCommonItem に case/casex/casez 分岐)
- `foreach(test[i])` の string 配列 (DataObjectReference の acceptRange 条件、commit: `b6a63eb`)
- for_initialization の複数 for_variable_declaration (各宣言が独自の型、commit: `3421a5f`)
- `wait (enable) #10 a = b;` (procedural_timing_control_statement) の "; expected" 修正 (commit: `ba65ece`)
- randsequence 残課題 (randcase 重み / break/return / repeat production、commit: `94656f0`)
  - production 登録 (registerProduction / preRegisterProductions)
  - 仮引数 (`Port.ParseTfPortItems`)
  - production body `{ ... }` block
  - 無限ループ修正 (endcase 停止 / `{}` block 後 break)
- randsequence production 引数 (18.17.5 / 18.17.7、commit: `cf4fcec`)
- pattern matching 演算子 `matches` (MatchesOperator 新設、12.6.2 / 12.6.3)
- 前方参照 function call (`localparam a = fun(3);`、Primary.parseUndefinedFunction 委譲)
- `if (expr matches pattern)` 構文 (12.6.2--if_pattern / 12.6.3--conditional_pattern 対応済み)

---

## 12. interface / program / clocking

- `endinterface : name` 付き interface の body item parse (commit: `e178e31`)
- interface class / class type access 系 (8.26.3 / 8.26.6.2 / 8.26.5)
- clocking_block / clocking_skew (`default input #10ns output #5ns;`、commit: `e411f50`)
- `default clocking @(posedge clk);` の無名 clocking 宣言 (commit: `9d07eb2`)
- program item (24.3--program、残課題: トークナイザ offset drift)
- 残課題: 25.3-interface

---

## 13. エラー復帰 / トークナイザ / 内部機構

- `WordScanner.IsPartialParse` の論理反転修正 (commit: `933dcaf`)
  - `return CompletionContext != null;` (partial parse のときだけエラー抑制)
  - この修正で表面化した既存 parser バグ 4 件 (5.6.1 / 16.12 / 8.8 / other.sv) を個別対応
- module 本体の無意味な階層参照 (`AAAAAA.BBBB`) のエラー検出 (commit: `e9c3b2b`)
  - ModuleInstantiation / ProgramInstantiation / InterfaceInstance の直前トークン検証強化
- 健全なエラー復帰 (parameter port list 無限ループ防止 / item ループ SkipToKeyword 化 / 停止 keyword 拡張、commit: `8bc80e6`)
- 階層 named event 参照の前方参照対応
  - ParseEngine の 2パス機構 (ParseOnceAsync、エラー時のみ2パス目)
  - NameReference.GetElement の同一ファイル BuildingBlocks fallback (commit: `36ae0cf`)
- WordScanner の include macro 展開時 Substring 例外 (`wordType` が Eof に更新されない)

---

## 14. 残課題一覧 (NEG-NOT-REJECTED 40件 + その他)

- (A) プリプロセッサ / マクロ系: 22.5.1--define-expansion_1〜14 / 22.5.1--include-define-expansion / 22.5.1--define-expansion_24
- (B) 型チェック系: 6.19.3--enum_type_checking_inv / 6.19.4--enum_numerical_expr_no_cast / 6.19--enum_value_inv / default-value.sv / variable-slice-zero.sv / 5.10-structure-arrays-illegal / 6.12--real_bit_select_idx
- (C) class 抽象性・制約: class_member_test_5 / 8.21--abstract_class_inst / 18.5.2--pure-constraint_2 / 8.26.6.x
- (D) assertion: 16.10 / 16.15 / 16.12
- (E) その他: 24.3--program (トークナイザ offset drift) / 5.6.4--compiler-directives-include (環境依存)
- 試行して撤回: 10.3--proc-assignment--bad (net への手続き代入検出、非ANSIポート再宣言の設計見直しが必要)

---

## 15. コミット履歴 (主要)

### CodeEditor2VerilogPlugin
| commit | 内容 |
|--------|------|
| `933dcaf` | IsPartialParse 論理反転修正 |
| `e9c3b2b` | module 本体の無意味階層参照のエラー検出 |
| `30ef362` | module 外 `` `define `` 重複修正 |
| `3e0c73e` | POS-ERROR 4件 + プリプロセッサ検証 |
| `ca12ce4` | enum x/z + real edge |
| `7f6cd66` | real bit select |
| `df89ba8` | void function return |
| `36ae0cf` | 階層 named event 前方参照 (2パス) |
| `9315fd9` | constraint / randomize 系 |
| `94656f0` | randsequence 残課題 |
| `7134fc3` | SV_COV_* 事前定義マクロ |
| `c43ed69` | typedef struct/union packed |
| `af6a3f7` | dynamic array ArrayMethods 統一 |
| `ddf2d6f` | out-of-block method 定義 |
| `0d9a4f8` | sequence repetition range |
| `39e1e23` | module 直下 concurrent/deferred assertion |
| `b239721` | forward declaration (相互参照 class) |
| `fdb340f` | class member / interface class 解析系 |
| `7d5515f` | implemented interface class 再宣言 |
| `dde0e68` | this.<built-in method> |

### メイン (submodule pointer 更新)
| commit | 内容 |
|--------|------|
| `8da2492` | sv-tests harness + CodeEditor2 submodule pointer |
| `f831a2e` | include 相対パス解決 (共有 TestProject) |
| `b3f1377` | ハーネス名前空間リセット |
| `5880d52` | `:defines:` メタデータ処理 |
| `4672d4d` | state.md 更新 |

---

## 16. メモ

- 一時デバッグテスト (`DebugClassParamTests` / `DebugDirectiveTests` / `DebugPropertyIffTests` /
  `DebugMacroDuplicateTests` / `DebugConstructorParseTests` 等) は検証用のためコミットから除外し作業ツリーに残置
- レポート: `SystemVerilogLanguageServer.Tests/bin/Debug/net10.0/sv_tests_report.txt`
- ビルド: `dotnet build "RtlEditor2.Desktop.csproj" -clp:ErrorsOnly`
- サブモジュールコミット: `git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin ...`
- **教訓**: Debug テストは `Assert.NotNull(parsed)` のみでなく必ずエラー数を検証すること
  (エラー数を assert しないと「単独では pass」と誤認する)
