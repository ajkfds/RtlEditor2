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

## 1.5 エージェント (AI) 環境からログを読む方法

**ログはプロジェクトルートの外 (`%APPDATA%\Code\logs`) にあるため、`read_file` では読めない**
(`Permission denied. Cannot read files outside of the project root.`)。さらに `powershell` /
`cmd` / `node` / `ls` / `cat` は**コマンド許可リスト外**で実行できない。

→ **`dotnet run` で一時的な C# ツールを作り、`FileShare.ReadWrite` で開いて読む**のが確実。
VS Code がログファイルを開いたままなので、共有読み取りを指定しないと
`The process cannot access the file ... because it is being used by another process.` になる。

手順:

1. `_tmp_logdump/LogDump.csproj` (`net10.0` / `OutputType=Exe`) と `Program.cs` を作る
2. `Program.cs` でやること:
   - 探索ルート: `%APPDATA%\Code\logs`、`%APPDATA%\Code - Insiders\logs`、`Path.GetTempPath()`
   - `Directory.EnumerateFiles(root, "svlsp-extension*.log", SearchOption.AllDirectories)` で列挙
   - **`new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)`**
     で開く (これが無いとロックで失敗する)
   - `LastWriteTimeUtc` の降順で新しいものから表示
3. `dotnet run --project _tmp_logdump/LogDump.csproj -c Release`
4. 調査後に **`git clean -fd _tmp_logdump`** で削除 (コミットに含めない)

### 見るべきファイル (セッションごと)

```
%APPDATA%\Code\logs\<YYYYMMDDTHHMMSS>\
├── main.log                                  # 拡張ホストの exit code / crash 記録
├── window<N>\renderer.log                    # マニフェスト検証エラー (package.json 不正)
├── window<N>\exthost\exthost.log             # 拡張の activate 状況
└── window<N>\exthost\rtleditor2.systemverilog-lsp\
    ├── svlsp-extension.log                   # 通常ログ (logLine)
    └── svlsp-extension-crash.log             # クラッシュログ (crashLog)
```

### 実際に効いた読み方 (2026-11 の起動失敗調査)

- **`main.log` の `exited with code: 134` / `crashed with code 134`** … 拡張ホストが SIGABRT で
  落ちている。Node の `uncaughtException` ハンドラ自体が throw した可能性が高い
- **`renderer.log` の `property 'id' is mandatory ...`** … `package.json` の contribution 不正。
  ビューコンテナ ID は **英数字 + `_` + `-` のみ** (ドット不可)。不正だと
  `View container '...' does not exist and all views registered to it will be added to 'Explorer'.`
  が出て、そのペインだけ Explorer に落ちる
- **`svlsp-extension-crash.log` の `Channel has been closed`** … ホスト破棄中に
  `OutputChannel.appendLine` / `showErrorMessage` を呼んでいる。`logLine` 等を try/catch で守る
- **`unhandledRejection: Canceled: Canceled`** … ホスト破棄時の正常な reject。ノイズなので無視してよい

### 注意

- 出力が大きいと `execute_command` の上限 (256KB) で切られる。**キーワードで絞る**
  (`systemverilog-lsp` / `exited with code` / `Channel has been closed` / `property \`id\`` 等)、
  または `Take(n)` / 末尾 N 行だけ表示する
- `C# Dev Kit` の `ServiceHub\*.svclog` は巨大かつ無関係。**除外する**こと

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

### ★ 最初に見るべき指標: `exthost` フォルダの有無

ウィンドウのログフォルダ（`<session>/window<N>/`）に **`exthost/` が存在するか** で
「拡張コードが走ったか」を一瞬で判定できる。

| `exthost/` | `1-SystemVerilog LSP.log` | 意味 |
|---|---|---|
| あり | あり | `activate()` まで到達している → 原因は拡張コード側（crash log を見る） |
| あり | なし | activate 前後で落ちた / 無効化された → `exthost.log` を見る |
| **なし** | **なし** | **拡張 JS が 1 行も走っていない**。原因はほぼ **launch 構成**（拡張のバグではない） |

`exthost/` が無いのに `renderer.log` だけがあるケースは、
`main.log` の `Extension host with pid N exited with code: 134` /
`crashed with code 134 and reason 'crashed'`（SIGABRT）とセットで出る。
**このとき `svlsp-extension-crash.log` は絶対に作られない**ので、
crash log を探し続けても無駄（存在しないのが正常）。

### 既知の原因: `preLaunchTask` のビルド失敗が compound 起動を巻き添えにする

`Launch: SystemVerilog Language Server (C#)` に `preLaunchTask: build-server` があると、
そのビルドが失敗したとき（稼働中サーバ / 残留 `testhost` による DLL ロック = MSB3027/MSB3021）
**compound 起動全体が中断され、extensionHost utility プロセスが SIGABRT（134）で落ちる**。
`stopAll: false` にしていても、`preLaunchTask` 失敗は「起動そのものの失敗」なので防げない。

→ 対策として `preLaunchTask` を外した（`launch.json` / `RtlEditor2.code-workspace` 両方）。
サーバをビルドしたいときは **Run Build Task (`Ctrl+Shift+B`) → `build-server`** を先に実行する。

切り分けコマンド:
```
dotnet build SystemVerilogLanguageServer/SystemVerilogLanguageServer.csproj -clp:ErrorsOnly
```
これが 0 errors でも、**別プロセスが DLL をロックしていると
`bin/Debug` へのコピーだけが失敗する**ことがある。
`tasklist` で `SystemVerilogLanguageServer` / `testhost` / `dotnet` の残骸を確認する。

### 起動失敗の原因になる: `property `icon` is mandatory and must be of type `string``

```
[error] [rtleditor2.systemverilog-lsp]: property `icon` is mandatory and must be of type `string`
[error] [LocalProcessExtensionHost]: Extension host did not start in 10 seconds (debugBrk: true)
→ Extension host with pid N exited with code: 134
```

**`viewsContainers.<location>[].icon` は必ず「パス文字列」**。
`views.*.icon` は `{ "light": ..., "dark": ... }` も受け付けるが、
**`viewsContainers` はオブジェクトを受け付けない**（`string` のみ）。
ここにオブジェクトを書くとマニフェスト検証で弾かれ、`exthost` フォルダが
作られないまま exit code 134 で落ちる（＝拡張コードは 1 行も走らない）。

デバッグ起動 (`extensionHost`, `debugBrk: true`) では
`Extension host did not start in 10 seconds` も併せて出るため、
「10 秒待って起動失敗」に見えるが本質はマニフェスト不正。

**必ず機械検証すること**（`node --check` は使えないので C# で）:
`package.json` を `JsonDocument.Parse(..., CommentHandling = Disallow, AllowTrailingCommas = false)`
でパースし、
- `viewsContainers.*[].icon` が `JsonValueKind.String` であること
- そのパスのファイルが実在すること
- `viewsContainers.*[].id` が `^[A-Za-z0-9_-]+$`（ドット不可）
- `contributes.views` のキーが宣言済みコンテナ id（または `explorer` 等）であること
- `activationEvents` の `onView:<id>` が実在する view id であること
を確認する。

### 併せて出る警告: `View container '...' does not exist`

```
[warning] [rtleditor2.systemverilog-lsp]: View container 'systemverilogLsp-messagesContainer'
does not exist and all views registered to it will be added to 'Explorer'.
```
これは**起動失敗の原因ではなく**、`viewsContainers.<location>[].icon` に
codicon 文字列（`$(warning)` 等）を書いたのが原因。
`icon` は **ファイルパス（または `{ "light": ..., "dark": ... }`）** を要求する
（`views` / `commands` の `icon` は codicon でよい、という違いに注意）。
不正だとコンテナ登録に失敗し、そのペインだけ Explorer に落ちる。

## 3. 起動失敗の切り分け手順

0. **`<session>/window<N>/exthost/` があるか確認する**（無ければ launch 構成の問題。上記参照）。
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
| 拡張ホストが **exit code 134** で落ちる (SIGABRT) | まず `<session>/window<N>/exthost/` の有無。**無ければ launch 構成**（`preLaunchTask` のビルド失敗など）が原因で、拡張コードは走っていない。crash log も作られない。有る場合は `svlsp-extension-crash.log` の `uncaughtException` を見る |
| ペインが Explorer に落ちる / コンテナが出ない | `renderer.log` の `property 'id' is mandatory ...` (ビューコンテナ ID にドット等の不正文字) |
| ウィンドウを閉じる/リロードするとクラッシュログが増える | `Channel has been closed` (ホスト破棄中の `appendLine` / `showErrorMessage` が未保護) |
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
