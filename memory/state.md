# 作業状態 (State)

## 進行中タスク

- (なし)

## 完了済みタスク

- `.agents/overview.md`に`TemplateEngineHost`をProject Structureとサブモジュール一覧に追加
- `memory/state.md`を作成 (rules.mdで参照されているが実在しなかった)

## Next Steps

- ユーザからのタスクを待機

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
ただしこれらはAGENTS.mdに集約されている現状でも、overview.mdには技術スタックやアーキテクチャ指針を記載するというルールに照らすと概ね適切。必要に応じて新規タスクで対応する。
