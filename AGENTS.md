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

### Core Components

- **CodeEditor2/CodeEditor2/CodeEditor2/**:
  - `CodeEditor/`: Core editor components (CodeDocument, Parser, etc.)
  - `Data/`: Project, File, Folder management
  - `Parser/`: Parsing infrastructure
  - `Views/`, `ViewModels/`: UI layer
  - `Controller.cs`: Main controller

- **CodeEditor2Plugin/**: Plugin base system
  - `IPlugin`: Plugin interface
  - `PulginManager`: Plugin management

- **CodeEditor2VerilogPlugin/**: Verilog language support
  - `Parser/`: Verilog parser
  - `FileTypes/`: File type detection
  - `NavigatePanel/`: Tree navigation

## Recent Fixes

- Fixed CS0246 and CS1503 errors in TextFile.cs by adding `using CodeEditor2.CodeEditor.TextDecollation;` directive to resolve `LineInformation` type reference.
- Added restart resilience to FileBasedPipe receiver: `InitializeAsReceiver()` now reads existing `ack.dat` file to restore `_lastProcessedId` instead of always starting from 0, preventing duplicate message reception when receiver restarts while `data.dat` still contains unacknowledged messages.
- Fixed `SynchronizationLockException` in `Item.Dispose()`: Wrapped `itemLock?.Dispose()` in try-catch to handle exceptions that can occur when the lock is held during disposal or when other threads are still using the lock.
- ✅ **Parse Request Queue**: Implemented request queuing instead of immediate cancellation to prevent race conditions in `Tool/ParseHierarchy.cs`.
- ✅ **Atomic BuildingBlock Registration**: Added locks to `Root.BuildingBlocks` dictionary for thread-safe updates in `BuildingBlock.cs`.
- ✅ **Updater.UpdateAsync Atomicity**: Fixed clear-then-add pattern in `VerilogCommon/Updater.cs` to ensure atomic items update.
- ✅ **Composite Key Lock**: Protected key generation in `VerilogModuleInstance.cs` to prevent race conditions.
- ✅ **Parse Mode Sequencing Gate**: Ensured proper parse mode ordering in `Root.cs` and `Module.cs`.
- ✅ **Version-Stamped Color Copy**: Added version checking during color copy in `CodeDocument.cs` and `ColorHandler.cs`.

---

## Parse Instability Investigation

### Problem
Tree navigation produces inconsistent parse results. Clicking through tree nodes causes parse results to change unpredictably, with text coloring and error states fluctuating.

### Root Causes Identified

| # | Issue | Location | Severity |
|---|-------|----------|----------|
| 1 | Parallel Parse Cancellation Without Coordination | `Tool/ParseHierarchy.cs` | **High** |
| 2 | Shared Root BuildingBlocks Dictionary | `BuildingBlock.cs` | **High** |
| 3 | Non-Atomic Items Update in Updater | `VerilogCommon/Updater.cs` | **Medium** |
| 4 | Key Generation Race in VerilogModuleInstance | `VerilogModuleInstance.cs` | **Medium** |
| 5 | Parse Mode Dependent Behavior | `Root.cs`, `Module.cs` | **Medium** |
| 6 | CodeDocument Color Copy Race | `CodeDocument.cs`, `ColorHandler.cs` | **Low** |
| 7 | Include File Update Timing | `VerilogFile.cs` | **Low** |
| 8 | ParseWorker Sub-file Check Race | `ParseWorker.cs` | **Low** |

### Investigation Reports

| Project | Report |
|---------|--------|
| `CodeEditor2VerilogPlugin/` | `README.md` in project folder |
| `CodeEditor2/` | `README.md` in project folder |
| `CodeEditor2Plugin/` | `README.md` in project folder |
| `RtlEditor2.Desktop/` | `README.md` in project folder |

### Recommended Fixes

1. **Parse Request Queue**: Replace immediate cancellation with request queuing
2. **Atomic BuildingBlock Updates**: Add locks to `Root.BuildingBlocks`
3. **Atomic Items Update**: Fix `Updater.UpdateAsync` clear-then-add pattern
4. **Composite Key Lock**: Protect key generation in `VerilogModuleInstance`
5. **Parse Mode Sequencing Gate**: Ensure proper parse mode ordering
6. **Version-Stamped Color Copy**: Add version checking during color copy

### Fixed Issues

| # | Issue | Status | Commit |
|---|-------|--------|--------|
| 1 | Parse Request Queue | ✅ Fixed | b8afce6 |
| 2 | Add atomic BuildingBlock registration | ✅ Fixed | - |
| 3 | Fix Updater.UpdateAsync atomicity | ✅ Fixed | - |
| 4 | Add composite key lock to VerilogModuleInstance | ✅ Fixed | - |
| 5 | Implement Parse Mode Sequencing Gate | ✅ Fixed | - |
| 6 | Add version-stamped color copy | ✅ Fixed | - |
| 7 | Wait Order Statement (#41) | ✅ Fixed | - |
| 8 | Sequence Expression + Sequence Instance | ✅ Fixed | - |

### Next Actions
- [ ] Create test cases to verify fixes
- [ ] Performance testing under rapid node clicks
- [ ] Address remaining unimplemented SystemVerilog specs (65 items total)

---

## 未実装 SystemVerilog 仕様リスト

プロジェクト `CodeEditor2VerilogPlugin/` で未実装の SystemVerilog 仕様を発見するたびに逐次更新します。

### カテゴリ別 未実装仕様

#### 1. Building Blocks (モジュールレベル)

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 1 | **Checker Declaration** | `Verilog/BuildingBlocks/Checker.cs` | ❌ 未実装 | BNFコメントのみ、解析処理なし |
| 2 | **Clocking Declaration** | `Verilog/ModuleItems/` | ⚠️ 一部実装 | キーワード登録のみ、本文解析なし |
| 3 | **Interface Class Declaration** | `Verilog/BuildingBlocks/Class.cs` | ✅ 実装完了 | extends/implements 解析対応済み |

#### 2. 制約・機能検証 (Constraint & Coverage)

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 4 | **Constraint Declaration** | `Verilog/` | ❌ 未実装 | ParsedDocument.csでキーワード登録済みのみ |
| 5 | **Covergroup Declaration** | `Verilog/` | ❌ 未実装 | キーワード登録済みのみ |
| 6 | **Coverpoint** | `Verilog/` | ❌ 未実装 | キーワード登録済みのみ |
| 7 | **Cross** | `Verilog/` | ❌ 未実装 | キーワード登録済みのみ |

#### 3. シーケンス・プロパティ (SVA)

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 8 | **Sequence Expression** | `Verilog/Sequence/SequenceExpr.cs` | ✅ 実装完了 | `SequenceExpr.ParseCreate` 等 |
| 8a | **Sequence Instance** | `Verilog/Sequence/SequenceInstance.cs` | ✅ 実装完了 | 識別子参照と引数リスト対応 |
| 8b | **Sequence Declaration** | `Verilog/Sequence/SequenceDeclaration.cs` | ✅ 実装完了 | port list, assertion variables対応 |
| 8c | **Sequence Match Item** | `Verilog/Sequence/SequenceMatchItem.cs` | ✅ 実装完了 | operator_assignment, inc/dec対応 |
| 8d | **Dist Expression** | `Verilog/Sequence/DistExpression.cs` | ✅ 実装完了 | `dist {}` 対応 |
| 9 | **Property Expression** | `Verilog/Property/PropertyExpr.cs` | ✅ 実装完了 | 完全実装済み |
| 9a | **Property Declaration** | `Verilog/Property/PropertyDeclaration.cs` | ✅ 実装完了 | port list, assertion variables対応 |
| 9b | **Property Spec** | `Verilog/Assertion/PropertySpec.cs` | ✅ 実装完了 | clocking_event, disable iff対応 |
| 9c | **Property Instance** | `Verilog/Property/PropertyInstance.cs` | ✅ 実装完了 | property instance対応 |
| 9d | **Property Operator** | `Verilog/Property/PropertyOperator.cs` | ✅ 実装完了 | 全演算子対応 |
| 10 | **Randsequence** | `Verilog/` | ❌ 未実装 | キーワード登録済みのみ |

#### 4. DPI & 外部連携

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 11 | **DPI Export** | `Verilog/DpiImportExport.cs` | ⚠️ 部分実装 | parseExport メソッドが空 |
| 12 | **Let Declaration** | `Verilog/` | ⚠️ 部分実装 | キーワード登録済みのみ |

#### 5. データ型・宣言

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 13 | **Type Reference** | `Verilog/DataObjects/DataTypes/` | ❌ 未実装 | Variable.Create参照コメントのみ |

### 詳細分析

#### Checker Declaration (Priority: Low)

**ファイル**: `Verilog/BuildingBlocks/Checker.cs`

```csharp
public class Checker
{
    /*
    ## checker
    checker_declaration ::=
        "checker" checker_identifier [ ( [ checker_port_list ] ) ] ; 
        { { attribute_instance } checker_or_generate_item } 
        "endchecker" [ : checker_identifier ] 
    ...
    */
}
```

**問題**: BNFコメントのみで実際の実装が一切ない。checker のポート、アイテムの解析が必要。

---

#### Sequence Expression (Priority: Medium)

**ファイル**: `Verilog/Assertion/SequenceExpression.cs`

```csharp
internal class SequenceExpression
{
    /*
    sequence_expr ::=
          cycle_delay_range sequence_expr { cycle_delay_range sequence_expr }
        | sequence_expr cycle_delay_range sequence_expr { cycle_delay_range sequence_expr }
        | expression_or_dist [ boolean_abbrev ]
        | sequence_instance [ sequence_abbrev ]
        ...
    */
    public static new SequenceExpr? ParseCreate(WordScanner word, NameSpace nameSpace)
    {
        // TODO: Implementation
    }
}
```

**問題**: BNFのみ、ParseCreate が空実装。`##` delay, `and`, `or`, `intersect` 等が未対応。

---

#### Property Expression (Priority: Medium)

**ファイル**: `Verilog/Property/PropertyExpr.cs`

**ステータス**: `PropertyOperator` のみ実装済み。優先度解析のベース部分是実装済みだが、実際のプロパティ式構築処理が必要。

---

#### ModPort Clocking Declaration (Priority: Low)

**ファイル**: `Verilog/ModPort.cs`

```csharp
internal bool parse_modport_clocking_declaration(WordScanner word, NameSpace nameSpace)
{
    if (word.Text != "clocking") throw new Exception();
    word.Color(CodeDrawStyle.ColorType.Keyword);
    word.MoveNext();
    return true; // TODO: clocking_identifier の実際の処理が必要
}
```

**問題**: clocking キーワードをスキップするだけで、実際の clocking_identifier 登録処理がない。

---

#### DPI Export (Priority: Low)

**ファイル**: `Verilog/DpiImportExport.cs`

```csharp
public static void parseExport(WordScanner word, NameSpace nameSpace)
{
    // 空の実装
}
```

**問題**: export 構文の解析が未実装。

---

### 優先度ガイドライン

| 優先度 | 説明 | 対象 |
|--------|------|------|
| High | 一般的によく使用される | Sequence Expression, Property Expression |
| Medium | よく使用される | Constraint, Covergroup |
| Low | 特殊なケース | Checker, DPI Export, Let |

### 更新履歴

| 日時 | 更新内容 |
|------|----------|
| 2024-XX-XX | 初版作成、未実装Spec 13件を特定 |
| 2024-XX-XX | Bind Directive, Net Alias, Clocking Declaration, Deferred Immediate Assertions を追加 |

---

## 追加発見 未実装仕様 (第2回)

#### 6. バインディング・エイリアス

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 14 | **Bind Directive** | `Verilog/ModuleItems/` | ❌ 未実装 | Root.cs, Module.cs, Interface.cs, ModuleCommonItem.cs で言及のみ |
| 15 | **Net Alias** | `Verilog/ModuleItems/` | ❌ 未実装 | ModuleCommonItem.csのBNFに言及のみ |

#### 7. Clocking & Timing

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 16 | **Cloking Declaration** | `Verilog/ModuleItems/ModuleOrGenerateItemDeclaration.cs` | ⚠️ スキップのみ | `clocking` キーワードをスキップ、解析処理なし |
| 17 | **Default Clocking** | `Verilog/ModuleItems/ModuleOrGenerateItemDeclaration.cs` | ⚠️ スキップのみ | `default clocking` のみ識別子をスキップ |
| 18 | **Event Declaration** | `Verilog/` | ❌ 部分実装 | Function/Task 内で言及のみ |

#### 8. アサーション詳細

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 19 | **Deferred Immediate Assertion** | `Verilog/Statements/ImmidiateAssertionStatement.cs` | ✅ 実装完了 | `assert #0`, `assert final` 対応済み |
| 20 | **Checker Instantiation** | `Verilog/` | ❌ 未実装 | BNFで言及のみ |
| 21 | **Expect Property Statement** | `Verilog/Statements/ExpectPropertyStatement.cs` | ✅ 実装完了 | `ExpectPropertyStatement.ParseCreate` 実装済み |

#### 9. rand/randsequence

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 22 | **Randsequence Statement** | `Verilog/Statements/` | ❌ 存在せず | Statements.csで言及されているがファイルなし |
| 23 | **Randcase Statement** | `Verilog/Statements/` | ❌ 存在せず | Statements.csで言及されているがファイルなし |

---

### 未実装仕様 詳細 (追加分)

#### Bind Directive (Priority: Medium)

**参照箇所**:
- `Verilog/BuildingBlocks/Root.cs` (line 30, 65, 147)
- `Verilog/BuildingBlocks/Module.cs` (line 478)
- `Verilog/BuildingBlocks/Interface.cs` (line 484)
- `Verilog/Items/ModuleCommonItem.cs` (line 16)

```BNF
bind_directive ::= bind bind_target_scope [ bind_target_instance_list ] ; [ bind_items ]
bind_target_scope ::= hierarchical_identifier | wildcard import package_identifier
```

**問題**: `bind` キーワードの解析処理が一切ない。instance specific binding の機能が必要。

---

#### Net Alias (Priority: Low)

**参照箇所**: `Verilog/Items/ModuleCommonItem.cs`

```BNF
net_alias ::= alias ( list_of_net_aliases ) = expression ;
list_of_net_aliases ::= net_alias_item { , net_alias_item }
net_alias_item ::= net_lvalue
```

**問題**: ネットワークの別名付けの構文解析がない。

---

#### Deferred Immediate Assertion (Priority: Medium)

**ファイル**: `Verilog/Assertion/AssertionItemXX.cs`

```csharp
/*
deferred_immediate_assert_statement ::=
      assert #0 ( expression ) action_block
    | assert final ( expression ) action_block

deferred_immediate_assume_statement ::=
      assume #0 ( expression ) action_block
    | assume final ( expression ) action_block

deferred_immediate_cover_statement ::=
      cover #0 ( expression ) statement_or_null
    | cover final ( expression ) statement_or_null         
*/
```

**問題**: BNFコメントのみで実装なし。`#0` と `final` の両方のパターンをサポートする必要がある。

---

#### Randsequence/Randcase (Priority: Low)

**参照箇所**: `Verilog/Statements/Statements.cs` (lines 30, 111, 122)

```csharp
// randsequence_statement 
// randcase_statement 
// expect_property_statement
```

**問題**: ファイルが存在しない。これらの制御フロー文の解析処理が必要。

---

### 優先度別 待実装リスト (全22件)

| 優先度 | 件数 | 対象 |
|--------|------|------|
| High | 3件 | Sequence Expression, Property Expression, Clocking Declaration |
| Medium | 5件 | Constraint, Covergroup, Bind Directive, Deferred Immediate Assertion, Event Declaration |
| Low | 14件 | Checker, DPI Export, Let, Type Reference, Net Alias, Randsequence, Randcase, Expect Property, Checker Instantiation |

---

## 追加発見 未実装仕様 (第3回)

#### 10. Sequence/Property Declaration

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 24 | **Property Declaration** | `Verilog/Property/PropertyDeclaration.cs` | ✅ 実装完了 | port list, assertion variables対応 |
| 25 | **Property Port List** | `Verilog/Property/PropertyDeclaration.cs` | ✅ 実装完了 | local, input direction対応 |
| 26 | **Property Case Item** | `Verilog/Property/PropertyExpr.cs` | ✅ 実装完了 | `if`/`case` 式対応 |
| 27 | **Sequence Declaration** | `Verilog/Sequence/SequenceDeclaration.cs` | ✅ 実装完了 | port list, assertion variables対応 |
| 28 | **Sequence Port List** | `Verilog/Sequence/SequenceDeclaration.cs` | ✅ 実装完了 | inout 方向対応 |
| 29 | **Sequence Match Item** | `Verilog/Sequence/SequenceMatchItem.cs` | ✅ 実装完了 | operator_assignment, inc/dec対応 |

#### 11. Procedural Assertion Statement

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 30 | **Assert Property Statement** | `Verilog/Assertion/AssertPropertyStatement.cs` | ✅ 実装完了 | action_block対応 |
| 31 | **Assume Property Statement** | `Verilog/Assertion/AssumePropertyStatement.cs` | ✅ 実装完了 | action_block対応 |
| 32 | **Cover Property Statement** | `Verilog/Assertion/CoverPropertyStatement.cs` | ✅ 実装完了 | statement_or_null対応 |
| 33 | **Restrict Property Statement** | `Verilog/Assertion/RestrictPropertyStatement.cs` | ✅ 実装完了 | `;` 対応 |
| 34 | **Cover Sequence Statement** | `Verilog/Assertion/CoverSequenceStatement.cs` | ✅ 実装完了 | disable iff対応 |
| 35 | **Immediate Assertion** | `Verilog/Statements/ImmidiateAssertionStatement.cs` | ✅ 実装完了 | simple, deferred #0, final対応 |

#### 12. Package & Export

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 36 | **Anonymous Program** | `Verilog/Items/PackageItem.cs` | ❌ 未実装 | Package 内の無名プログラム |
| 37 | **Package Import Item (複数)** | `Verilog/Items/PackageExportDeclaration.cs` | ⚠️ 部分実装 | `export *::*` のみ、複合アイテムは未対応 |

#### 13. Timeunits Declaration

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 38 | **Timeunits Declaration** | `Verilog/` | ❌ 未実装 | Module, Interface, Package 内で言及のみ |

---

### 未実装仕様 詳細 (第3回追加分)

#### Property Declaration (Priority: Medium)

**ファイル**: `Verilog/Items/AssertionItem.cs`

```BNF
property_declaration ::=
    "property" property_identifier [ "(" [ property_port_list ] ")" ] ";"
    { assertion_variable_declaration }
    property_spec [ ";" ]
    "endproperty" [ ":" property_identifier ]

property_port_list ::=
    property_port_item {"," property_port_item}

property_port_item ::=
    { attribute_instance } [ "local" [ property_lvar_port_direction ] ] property_formal_type
    formal_port_identifier {variable_dimension} [ "=" property_actual_arg ]
```

**問題**: BNFコメントのみで実装なし。port list の local, input direction、property_formal_type の解析が必要。

---

#### Sequence Declaration (Priority: Medium)

**ファイル**: `Verilog/Items/AssertionItem.cs`

```BNF
sequence_declaration ::=
    "sequence" sequence_identifier [ ( [ sequence_port_list ] ) ] ;
    { assertion_variable_declaration }
    sequence_expr [ ; ]
    "endsequence" [ : sequence_identifier ]
```

**問題**: BNFコメントのみで実装なし。port list で inout もサポートが必要。

---

#### Procedural Assertion Statement (Priority: Medium)

**ファイル**: `Verilog/Statements/ProceduralAssertionStatement.cs`

```csharp
public static IStatement? ParseCreate(WordScanner word, NameSpace nameSpace, string? statement_label)
{
    switch (word.Text)
    {
        case "assert":
        case "assume":
        case "cover":
        case "restrict":
            break;
        ...
    }

    if (word.NextText == "property")
    { // concurrent assertion
        word.AddError("concurrent assertion is not supported yet.");
        word.MoveNext();
    }
    return null;
}
```

**問題**: concurrent assertion (assert property等) が完全に未対応。Simple immediate assertion も未対応。

---

#### Immediate Assertion (Priority: Medium)

**参照**: `Verilog/Items/ConcurrentAssertionItemExceptCheckerInstantiation.cs`

Simple immediate assert statement 形式:
```systemverilog
assert (expression) action_block
assume (expression) action_block
cover (expression) statement_or_null
```

**問題**: 現在 `ConcurrentAssertionItemExceptCheckerInstantiation` のみ существование。immediate 版が必要。

---

### 優先度別 待実装リスト (全38件 → 実装済みにより減少)

| 優先度 | 件数 | 対象 |
|--------|------|------|
| High | 0件 | Sequence/Property Expression/Declaration 全て実装完了 ✅ |
| Medium | 5件 | Constraint, Covergroup, Bind Directive, Event Decl, Checker Instantiation |
| Low | 18件 | DPI Export, Let, Type Reference, Net Alias, Randsequence, Randcase, etc. |

### 実装優先度推奨

| 優先度 | 理由 | 推奨実装順序 |
|--------|------|-------------|
| 1 | SVA は検証で広く使用される | Sequence/Property Declaration → Expression → Procedural Assertion |
| 2 | Bind Directive は実プロジェクトで必要 | Bind → Clocking Declaration |
| 3 | Covergroup/Constraint はクラス検証で重要 | Constraint → Covergroup |
| 4 | 低優先度は特殊なケース | Randsequence, Checker 等は後で対応 |

---

## 追加発見 未実装仕様 (第4回)

#### 14. Constraint & Solve

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 39 | **Constraint Declaration** | `Verilog/` | ❌ 未実装 | キーワード登録済みのみ、`solve` キーワードも登録済み |
| 40 | **Soft Constraint** | `Verilog/` | ❌ 未実装 | `soft` キーワード登録済みのみ |

#### 15. Wait Statement

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 41 | **Wait Order** | `Verilog/Statements/WaitStatement.cs` | ✅ 実装完了 | `parseCreate_wait_order` で既に実装済み |

#### 16. Alias

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 42 | **Net Alias** | `Verilog/` | ❌ 未実装 | `alias` キーワード登録済みだが、BNFの alias 構文が未実装 |
| 43 | **Tagged Union** | `Verilog/` | ❌ 未実装 | `tagged` キーワード登録済みのみ |

#### 17. Class 詳細

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 44 | **Class Constructor Prototype** | `Verilog/` | ⚠️ 言及のみ | Class.cs に extern new prototype への言及があるが必要 |
| 45 | **Pure Virtual Method** | `Verilog/Items/ClassItem.cs` | ⚠️ 一部実装 | `pure virtual` のパースのみ実装、本体は未対応 |
| 46 | **Extern Method** | `Verilog/Items/ClassItem.cs` | ⚠️ 一部実装 | `extern` キーワードをスキップするのみ |

#### 18. Interface & Virtual

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 47 | **Virtual Interface** | `Verilog/DataObjects/Variables/` | ❌ 未実装 | Variable.Create の switch-case にコメントのみ存在 |

---

### 未実装仕様 詳細 (第4回追加分)

#### Wait Order Statement (Priority: Medium)

**ファイル**: `Verilog/Statements/WaitStatement.cs`

```csharp
// wait_statement ::=
//     "wait" "(" expression ")" statement_or_null
//     | "wait fork" ";"
//     | "wait_order" "(" hierarchical_identifier { "," hierarchical_identifier } ")" action_block

if (word.Text == "wait_order") return parseCreate_wait_fork(word, nameSpace); // BUG: 誤ったリダイレクト
```

**問題**: `wait_order` が `wait fork` のパース関数にリダイレクトされている。正しい `wait_order` 解析が必要:
```systemverilog
wait_order(id1, id2, ...) action_block
```

---

#### Constraint Declaration (Priority: Medium)

**参照**: `ParsedDocument.cs` (line 717)

```csharp
("constraint",1), ("covergroup",1),
("solve",1),       // constraint 内で solve before に使用
```

**問題**: `constraint` ブロックの解析処理が一切ない:
```systemverilog
constraint name_c { expression; }
constraint name_c { solve var1 before var2; }
```

---

#### Tagged Union (Priority: Low)

**参照**: `ParsedDocument.cs` (line 738)

```csharp
("tagged",1),
```

**問題**: `tagged union` / `tagged struct` への対応が未実装。

---

### 待実装リスト合計 (47件)

| カテゴリ | 件数 | 主な仕様 |
|----------|------|----------|
| SVA (Sequence/Property) | 9件 | Declaration, Expression, Procedural Assertion |
| Binding & Declaration | 5件 | Bind Directive, Clocking, Constraint |
| Class & Type | 8件 | Checker, Class Constructor, Interface Class, Typedef Extensions |
| Coverage | 4件 | Covergroup, Coverpoint, Cross, Bins |
| Data & Variables | 5件 | Type Reference, Virtual Interface, Event, Timeunits |
| DPI & External | 2件 | DPI Export, Let |
| Rand/Control | 4件 | Randsequence, Randcase, Expect, Immediate Assertion |
| Other | 10件 | Alias, Net Alias, Wait Order, etc. |

---

## AGENTS.md 更新履歴

| 日時 | 更新内容 | 累積件数 |
|------|----------|----------|
| 2024-XX-XX | 初版作成 | 13件 |
| 2024-XX-XX | Bind Directive, Net Alias 等追加 (第2回) | 22件 |
| 2024-XX-XX | Property/Sequence Declaration 等追加 (第3回) | 38件 |
| 2024-XX-XX | Wait Order bug, Constraint, Tagged 等追加 (第4回) | 47件 |
| 2024-XX-XX | Program Instantiation, Distribution, Primitive, Config 等追加 (第5回) | 55件 |

---

## 追加発見 未実装仕様 (第5回)

#### 19. Program Instantiation & 配置

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 48 | **Program Instantiation** | `Verilog/Items/ModuleCommonItem.cs` | ❌ 未実装 | ModuleCommonItem.csに言及のみ |
| 49 | **Config Declaration** | `Verilog/BuildingBlocks/Root.cs` | ❌ 未実装 | Root.csに言及のみ、`config` キーワードは登録済み |
| 50 | **Cell & Library** | `Verilog/General.cs` | ⚠️ キーワードのみ | キーワード登録済みだが未解析 |

#### 20. Distribution (with dist)

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 51 | **Dist List** | `Verilog/Sequence/DistExpression.cs` | ✅ 実装完了 | `DistExpression.ParseCreate` 実装済み |
| 52 | **Distribution Weight** | `Verilog/Sequence/DistExpression.cs` | ✅ 実装完了 | `:=`, `:/` ウェイト指定対応 |
| 53 | **With Dist** | `Verilog/` | ❌ 未実装 | `randomize() with constraint` 形式 |

#### 21. Primitive & Table

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 54 | **UDP Primitive Declaration** | `Verilog/` | ❌ キーワードのみ | `primitive`/`endprimitive`/`table`/`endtable` が未解析 |
| 55 | **Primitive Table Entry** | `Verilog/` | ❌ 未実装 | combinational/sequential テーブルエントリ |

---

### 待実装リスト合計 (55件)

| カテゴリ | 件数 | 主な仕様 |
|----------|------|----------|
| SVA (Sequence/Property) | 9件 | Declaration, Expression, Procedural, Dist |
| Binding & Configuration | 7件 | Bind, Clocking, Config, Cell/Library |
| Class & Type | 10件 | Checker, Class Constructor, Constraint, Typedef |
| Coverage | 4件 | Covergroup, Coverpoint, Cross, Bins |
| Data & Variables | 5件 | Type Reference, Virtual Interface, Event, Timeunits |
| DPI & External | 2件 | DPI Export, Let |
| Rand/Control | 5件 | Randsequence, Randcase, Expect, Immediate, Dist |
| Primitive/Table | 3件 | UDP, Table Entry |
| Other | 10件 | Alias, Net Alias, Wait Order, Program, etc. |

---

### 発見済み 未実装キーワード (システム登録済み)

`ParsedDocument.cs` に登録されているが実装が不完全なキーワード:
- `alias`, `soft`, `solve`, `tagged`, `accept_on`, `reject_on`, `sync_accept_on`, `sync_reject_on`
- `timeprecision`, `timeunit`, `type`
- `throughout`, `strong`, `weak`

---

## 次の調査対象 (調査候補)

1. **Clocking Item**: `clocking_item` の詳細な実装
2. **Let Declaration**: `let` 宣言の完全な実装
3. **Import/Export Specific Item**: 特定のアイテムをエクスポートする `import ...::*` 形式
4. **Extends with Parameters**: Class の `extends` でのパラメータ付きベースクラス

---

## 追加発見 未実装仕様 (第6回)

#### 22. Interconnect & Default Nettype

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 56 | **Interconnect Declaration** | `Verilog/DataObjects/Nets/Net.cs` | ❌ 未実装 | BNFに言及あり (`interconnect implicit_data_type ...`) |
| 57 | **Default Nettype** | `Verilog/` | ❌ 未実装 | コンパイラ指示 `'default_nettype` への対応 |

#### 23. Import/Export の詳細

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 58 | **Import Package Items** | `Verilog/Items/PackageExportDeclaration.cs` | ❌ 部分実装 | `export *::*` のみ、`package_identifier :: identifier` 形式未対応 |
| 59 | **Wildcard Import** | `Verilog/` | ❌ 未実装 | `import package_name::*` 形式 |

#### 24. Drive/Charge Strength

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 60 | **Charge Strength** | `Verilog/DataObjects/Nets/Net.cs` | ⚠️ 言及あり | `ChargeStrength.ParseCreate` 呼び出しはあるが、ファイルなし |

---

### 待実装リスト合計 (60件)

| カテゴリ | 件数 | 主な仕様 |
|----------|------|----------|
| SVA (Sequence/Property) | 9件 | Declaration, Expression, Procedural, Dist |
| Binding & Configuration | 8件 | Bind, Clocking, Config, Cell/Library, Default Nettype |
| Class & Type | 10件 | Checker, Class Constructor, Constraint, Typedef |
| Coverage | 4件 | Covergroup, Coverpoint, Cross, Bins |
| Data & Variables | 6件 | Type Reference, Virtual Interface, Event, Timeunits, Interconnect |
| DPI & External | 2件 | DPI Export, Let |
| Rand/Control | 5件 | Randsequence, Randcase, Expect, Immediate, Dist |
| Primitive/Table | 3件 | UDP, Table Entry |
| Package/Import | 3件 | Import specific items, Wildcard Import |
| Net Strength | 2件 | Charge Strength, Drive Strength |
| Other | 8件 | Alias, Net Alias, Wait Order, Program, etc. |

---

## AGENTS.md 更新履歴

| 日時 | 更新内容 | 累積件数 |
|------|----------|----------|
| 2024-XX-XX | 初版作成 | 13件 |
| 2024-XX-XX | Bind Directive, Net Alias 等追加 (第2回) | 22件 |
| 2024-XX-XX | Property/Sequence Declaration 等追加 (第3回) | 38件 |
| 2024-XX-XX | Wait Order bug, Constraint, Tagged 等追加 (第4回) | 47件 |
| 2024-XX-XX | Program Instantiation, Distribution, Primitive, Config 等追加 (第5回) | 55件 |
| 2024-XX-XX | Interconnect, Default Nettype, Import/Export 等追加 (第6回) | 60件 |
| 2024-XX-XX | Fork Control, Join variants, Action Block details 等追加 (第7回) | 65件 |

---

## 追加発見 未実装仕様 (第7回)

#### 25. Fork Control & Join

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 61 | **Disable Fork** | `Verilog/Statements/` | ❌ 未実装 | `disable fork` 文への対応 |
| 62 | **Join Variants** | `Verilog/Statements/ParallelBlock.cs` | ⚠️ 言及のみ | `join_any`, `join_none` が ParallelBlock.cs に言及されているが完全には未対応 |
| 63 | **Fork-Join Block Label** | `Verilog/Statements/ParallelBlock.cs` | ⚠️ 一部実装 | `:block_name` 形式は実装済みだが、ネスト時に問題がある可能性 |

#### 26. Action Block

| # | 仕様 | ファイル | ステータス | 備考 |
|---|------|---------|----------|------|
| 64 | **Action Block (if-else)** | `Verilog/Statements/` | ❌ 未実装 | assertion の action_block での else 句への対応 |
| 65 | **Assert Failure Action** | `Verilog/` | ❌ 未実装 | `$error`, `$fatal` 等のデフォルトアクション |

---

### 待実装リスト合計 (65件 → 実装により減少)

| カテゴリ | 件数 | 主な仕様 | 備考 |
|----------|------|----------|------|
| SVA (Sequence/Property) | 0件 | ✅ 全実装完了 | Declaration, Expression, Procedural, Dist |
| Binding & Configuration | 8件 | Bind, Clocking, Config, Cell/Library, Default Nettype | |
| Class & Type | 10件 | Checker, Class Constructor, Constraint, Typedef | |
| Coverage | 4件 | Covergroup, Coverpoint, Cross, Bins | |
| Data & Variables | 6件 | Type Reference, Virtual Interface, Event, Timeunits, Interconnect | |
| DPI & External | 2件 | DPI Export, Let | |
| Rand/Control | 3件 | Randsequence, Randcase | Expect, Immediate, Dist 実装完了 ✅ |
| Primitive/Table | 3件 | UDP, Table Entry | |
| Package/Import | 3件 | Import specific items, Wildcard Import | |
| Net Strength | 2件 | Charge Strength, Drive Strength | |
| Fork/Join Control | 4件 | Disable Fork, Join variants, Action Block | |
| Other | 8件 | Alias, Net Alias, Program, etc. | Wait Order 実装完了 ✅ |

---

## AGENTS.md 更新履歴 (完全版)

| 日時 | 更新内容 | 累積件数 |
|------|----------|----------|
| 2024-XX-XX | 初版作成 | 13件 |
| 2024-XX-XX | Bind Directive, Net Alias 等追加 (第2回) | 22件 |
| 2024-XX-XX | Property/Sequence Declaration 等追加 (第3回) | 38件 |
| 2024-XX-XX | Wait Order bug, Constraint, Tagged 等追加 (第4回) | 47件 |
| 2024-XX-XX | Program Instantiation, Distribution, Primitive, Config 等追加 (第5回) | 55件 |
| 2024-XX-XX | Interconnect, Default Nettype, Import/Export 等追加 (第6回) | 60件 |
| 2024-XX-XX | Fork Control, Join variants, Action Block 等追加 (第7回) | 65件 |
| 2025-01-XX | SVA assertions 完全実装完了 | 51件 |
| 2025-01-XX | Class extends/implements 実装完了 | 64件 |

## License

MIT License

---

## 調査完了サマリー

### 調査範囲
`CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/` ディレクトリ以下の全ファイルを調査。

### 発見された未実装 SystemVerilog 仕様の総数: **65件**

### 主要カテゴリ

| カテゴリ | 件数 | 代表的仕様 | ステータス |
|----------|------|------------|------------|
| SVA (アサーション) | 0件 | ✅ Sequence, Property, Immediate Assertion 全て実装完了 | 完了 |
| Binding & Configuration | 8件 | Bind Directive, Clocking, Config | 未実装 |
| Class & Type System | 9件 | Checker, Constraint | extends/implements 実装完了 ✅ |
| Coverage | 4件 | Covergroup, Coverpoint, Cross, Bins | 未実装 |
| Data & Variables | 6件 | Virtual Interface, Interconnect, Timeunits | 一部未実装 |
| Rand/Control | 3件 | Randsequence, Randcase | 未実装 |
| Package/Import | 3件 | Wildcard Import, Anonymous Program | 一部未実装 |
| Primitive/Net | 5件 | UDP, Charge Strength, Net Alias | 未実装 |
| Fork/Join Control | 4件 | Disable Fork, Join variants | 未実装 |
| Other | 8件 | Alias, Tagged Union, Program, etc. | 未実装 |

### 特に注意すべき問題

1. ~~**Wait Statement bug**: `wait_order` が `wait fork` に誤リダイレクトされている~~ ✅ 調査の結果、`parseCreate_wait_order` は既に正しく実装されていた
2. ~~**Sequence/Property Expressions**: BNFコメントのみで実際の解析処理がない~~ ✅ `Verilog/Sequence/SequenceExpr.cs` で完全実装済み、`SequenceInstance` を追加
3. ~~**Sequence/Property Declaration**: `AssertionItem.cs` のみBNF~~ ✅ `Verilog/Sequence/SequenceDeclaration.cs`, `Verilog/Property/PropertyDeclaration.cs` で実装
4. ~~**Immediate Assertion**: simple/deferred未対応~~ ✅ `ImmidiateAssertionStatement.cs` で全対応
5. **Constraint/Covergroup**: キーワード登録済みだがブロック解析が未実装
6. **Clocking Declaration**: キーワードスキップのみで完全な解析がない

### 推奨実装優先度

| 優先度 | 理由 | 対象 |
|--------|------|------|
| 1 | 広く使用・検証で重要 | Sequence/Property Declaration & Expression |
| 2 | 実プロジェクトで必要 | Bind Directive, Clocking Declaration |
| 3 | クラス検証で重要 | Constraint Declaration, Covergroup |
| 4 | 低優先度 | UDP, Checker 等は特殊なケース |
