# VS Code 拡張のデバッグ情報

`vscode-extension/` の不具合（起動しない / ビューが空 / 機能が動かない）を調べるときの
一次情報の場所と読み方をまとめる。**推測でコードを直す前に、まずここを見る。**

## 1. ログの出力先

拡張は 3 種類のログを出す。すべて `extension.js` の `initLog` / `logLine` / `crashLog` が担当。

| 種類 | 場所 | 内容 | いつ見るか |
|---|---|---|---|
| OutputChannel | VS Code の出力パネル「SystemVerilog LSP」 | `logLine()` の全行 | 拡張が起動しているとき |
| 通常ログ | `<拡張ログフォルダ>/svlsp-extension.log` | 同上をファイル複製 | 出力パネルを開くのが面倒なとき |
| **クラッシュログ** | `<拡張ログフォルダ>/svlsp-extension-crash.log`<br>（フォルダ不明時は `%TEMP%`） | `activate` の例外 / `uncaughtException` / `unhandledRejection` の**スタック** | **拡張が起動しないとき** |

### `<拡張ログフォルダ>` の実体

`context.logUri.fsPath`。Windows の既定では概ね次の形:

```
%APPDATA%\Code\logs\<YYYYMMDDTHHMMSS>\window<N>\exthost\rtleditor2.systemverilog-lsp\
```

`context.logUri` が無い場合は `context.globalStorageUri`、それも無ければ `os.tmpdir()`。

### ログファイルのパスを UI から知る方法

- コマンドパレット → `SystemVerilog LSP: Show Log` … OutputChannel を開く
- コマンドパレット → `SystemVerilog LSP: Copy Log File Path` … パスをクリップボードへ
- 起動失敗時は**エラー通知にクラッシュログのパスが含まれる**

## 2. サーバ (C#) 側のログ

`RawLspClient` が子プロセスの **stderr を 1 行ずつ `[server] ...` として `logLine` に流す**。
サーバ側の `Console.Error.WriteLine`（ワークスペース走査件数、parse 失敗、
`HandleHierarchy` のフルスタックトレース等）はここに出る。

```
[server] workspace roots: D:\Document\...
[server] workspace: 1100 files, parsing in background
```

サーバが**起動していない**場合はこの行が一切出ない → `resolveServerCommand` の解決失敗か
`spawn` 失敗（`server spawn error:` / `server exited with code N` が出る）。

## 3. 起動失敗の切り分け手順

1. `svlsp-extension-crash.log` を見る。
   - `activate: <stack>` … `activateCore` 内の登録処理で throw。スタックの最上位が原因箇所
   - `require(./hierarchy): <stack>` … `hierarchy.js` の読み込み失敗（構文エラー / export 不備）
   - `require(./messageView): <stack>` … `messageView.js` の読み込み失敗。
     **この場合は Message view だけが無効化され、拡張自体は起動する**（意図した設計）
   - `uncaughtException` / `unhandledRejection` … activate 外へ漏れた非同期例外
2. クラッシュログが空なら、`svlsp-extension.log` の末尾を見る（`extension activated` まで
   到達しているか、`starting server:` が出ているか）。
3. どちらも空なら、そもそも拡張が activate されていない
   （`package.json` の `activationEvents` / `main` / `contributes` の不整合を疑う）。

### 設計上の要点（なぜこの機構があるか）

- `activate()` が throw すると VS Code は通知を 1 回出すだけで**拡張を無効化**する。
  OutputChannel は Extension Development Host と一緒に消えるため、**ファイルに書かないと何も残らない**。
- 以前は `extension.js` 冒頭で `require('./messageView')` / `require('./hierarchy')` を
  **トップレベル**で実行していたため、兄弟モジュールの不具合が**拡張全体の起動不能**に
  直結していた。現在は try/catch でガードし、失敗理由を
  `messageViewLoadError` / `hierarchyLoadError` に保持している。
  - Message view = 任意機能 → 失敗しても**そのペインだけ**無効化して起動継続
  - hierarchy = 必須機能 → 失敗時は `crashLog` してから明示的に throw（原因が読める形）

## 4. よくある症状と見る場所

| 症状 | 一次情報 |
|---|---|
| 拡張が起動しない / Extension Development Host がすぐ閉じる | `svlsp-extension-crash.log` |
| RTL Hierarchy が空 | `svlsp-extension.log` の `[server] workspace: N files` と `rtl/hierarchy` の応答ログ |
| 波線 (diagnostics) が出ない | `[server]` 行に parse エラーが出ているか。`publishDiagnostics` は didOpen/didChange 後に push される |
| 補完 / hover が効かない | `initialize succeeded` が出ているか。出ていなければサーバ未起動 |
| Message view が空 | スコープが `Current file` のまま別ファイルを見ている可能性（URI キーは `canonicalKey` で正規化済み） |
| サーバが古い | `vscode-extension/server/` の同梱バイナリが publish 済みか。**プロトコル変更後は publish 更新が必須** |

## 5. 注意事項

- `node --check` はコマンド許可リスト外のため、**JS の構文チェックは自動化されていない**。
  `extension.js` / `hierarchy.js` / `messageView.js` を編集したら目視で確認すること。
- `dotnet build` が通っても拡張が動くとは限らない。拡張は同梱サーバ
  (`vscode-extension/server/`) を優先使用するため、**C# を変更したら publish 更新が必要**:
  ```
  dotnet publish SystemVerilogLanguageServer/SystemVerilogLanguageServer.csproj -c Release -o vscode-extension/server -p:DebugType=None -p:DebugSymbols=false
  ```
- 稼働中のサーバが `bin/Debug` の DLL をロックしてビルドが失敗する（MSB3027/MSB3021）ことがある。
  その場合は VS Code を閉じるか、`-p:BaseOutputPath=obj/tmpbuild/` で回避する。
- 検証用プローブ (`SystemVerilogLanguageServer.Tests/Debug*.cs`) はコミットに含めず作業ツリーに残置する方針。
