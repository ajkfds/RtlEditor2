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
RtlEditor2/                                # Main repository (git add/commit here)
├── RtlEditor2.Desktop/                    # Main desktop application
├── AjkAvaloniaLibs/                        # Utility library (submodule)
├── AjkLibs/                                # Utility library (submodule)
├── AvaloniaEdit/                           # Text editor control (submodule)
├── CodeEditor2/                            # Core editor component (submodule)
│   └── CodeEditor2/CodeEditor2/           # CodeDocument, Parser, Views, etc.
├── CodeEditor2Plugin/                      # Base plugin interface (submodule)
├── CodeEditor2VerilogPlugin/               # Verilog/SystemVerilog language support (submodule)
├── CodeEditor2AiPlugin/                    # AI integration plugin (submodule)
├── CodeEditor2AiAssistant/                 # AI Assistant (submodule)
├── CodeEditor2DrawIoPlugin/                # Draw.io diagram support (submodule)
├── CodeEditor2IcarusVerilogPlugin/         # Icarus Verilog simulation integration (submodule)
├── CodeEditor2LiveMarkdownPlugin/          # Live Markdown support (submodule)
├── CodeEditor2MarkdownPlugin/              # Markdown support (submodule)
├── CodeEditor2VivadoPlugin/                # Xilinx Vivado integration (submodule)
└── ajkCefGlue/                             # CEF support (submodule)
```

### サブモジュール一覧

| サブモジュール | URL | 用途 |
|---------------|-----|------|
| `CodeEditor2` | git@github.com:ajkfds/CodeEditor2.git | コアエディタコンポーネント |
| `CodeEditor2Plugin` | git@github.com:ajkfds/CodeEditor2Plugin.git | ベースプラグインインターフェース |
| `CodeEditor2VerilogPlugin` | git@github.com:ajkfds/CodeEditor2VerilogPlugin.git | Verilog/SystemVerilog言語サポート |
| `CodeEditor2AiPlugin` | git@github.com:ajkfds/CodeEditor2AiPlugin.git | AI統合プラグイン |
| `CodeEditor2AiAssistant` | git@github.com:ajkfds/CodeEditor2AiAssistant.git | AIアシスタント |
| `CodeEditor2MarkdownPlugin` | git@github.com:ajkfds/CodeEditor2MarkdownPlugin.git | Markdownサポート |
| `CodeEditor2LiveMarkdownPlugin` | git@github.com:ajkfds/CodeEditor2LiveMarkdownPlugin.git | ライブMarkdownサポート |
| `CodeEditor2DrawIoPlugin` | git@github.com:ajkfds/CodeEditor2DrawIoPlugin.git | Draw.io図形サポート |
| `CodeEditor2IcarusVerilogPlugin` | git@github.com:ajkfds/CodeEditor2IcarusVerilogPlugin.git | Icarus Verilogシミュレーション統合 |
| `CodeEditor2VivadoPlugin` | git@github.com:ajkfds/CodeEditor2VivadoPlugin.git | Xilinx Vivado統合 |
| `AjkAvaloniaLibs` | git@github.com:ajkfds/AjkAvaloniaLibs.git | Avalonia UIユーティリティライブラリ |
| `AjkLibs` | https://github.com/ajkfds/AjkLibs.git | 汎用ユーティリティライブラリ |
| `AvaloniaEdit` | git@github.com:ajkfds/AvaloniaEdit.git | テキストエディタコントロール |
| `ajkCefGlue` | git@github.com:ajkfds/ajkCefGlue.git | CEF (Chromium Embedded Framework) サポート |

## build方法

ワーニングが大量にあるので、build時にはerrorのみ参照してください。

全体build
```
dotnet build "RtlEditor2.Desktop.csproj" -clp:ErrorsOnly
```

plugin単位のbuild
```xml
<execute_command>
<command>dotnet build CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin.sln -clp:ErrorsOnly</command>
</execute_command>
```

## Git Commit手順 (重要)

このプロジェクトは**サブモジュール構造**を使用している。コミット前にパスを確認すること。

### 変更のあるファイルの場所を特定

```xml
<execute_command>
<command>git status</command>
</execute_command>
```

### 出力例:
```
modified:   CodeEditor2VerilogPlugin (new commits, modified content)
```

`(new commits, modified content)`はサブモジュール内の変更を示す。

### サブモジュール内の変更をコミットする場合

**正しい方法** (`git -C` を使用):
```xml
<execute_command>
<command>git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin add <変更ファイルのパス> && git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin commit -m "コミットメッセージ"</command>
</execute_command>
```

**例**: PortInvertSnippet.cs をコミットする場合:
```xml
<execute_command>
<command>git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin add CodeEditor2VerilogPlugin/Verilog/Snippets/PortInvertSnippet.cs && git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin commit -m "Fix PortInvertSnippet..."</command>
</execute_command>
```

### よくある間違い

❌ `git add CodeEditor2VerilogPlugin/...` - メインディレクトリからサブモジュールのファイルへアクセスできない
❌ `cd CodeEditor2VerilogPlugin && git add ...` - `cd` コマンドは許可されていない
❌ `git -C CodeEditor2VerilogPlugin add CodeEditor2VerilogPlugin/...` - パスが二重になる

✅ `git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin add CodeEditor2VerilogPlugin/...`

### サブモジュールの構造

```
RtlEditor2/                          # メインディレクトリ (git add/commitはここに)
└── CodeEditor2VerilogPlugin/        # サブモジュール
    └── CodeEditor2VerilogPlugin/    # 実際のコード (git -C で这里を指す)
        └── CodeEditor2VerilogPlugin/
            └── Verilog/
                └── Snippets/
                    └── PortInvertSnippet.cs
```

### ワークフロー

1. **ビルド**: エラーがないか確認
2. **git status**: 変更のあるファイルを確認
3. **サブモジュール内のファイルを変更した場合**:
   - `git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin add <パス>`
   - `git -C CodeEditor2VerilogPlugin/CodeEditor2VerilogPlugin commit -m "<メッセージ>"`
4. **メインディレクトリのファイルを変更した場合**:
   - `git add <パス>`
   - `git commit -m "<メッセージ>"`


## License

MIT License
