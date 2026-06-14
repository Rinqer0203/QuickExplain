# プロジェクト概要

QuickExplain は、Windows 向けの WPF デスクトップアプリです。クリップボード、グローバルショートカット、スクリーンショットをトリガーにして、Google / OpenAI / Ollama へ問い合わせ、翻訳・説明・任意プロンプトの結果を表示します。

## 構成

- `QuickExplain.sln`: ソリューション。
- `QuickExplain/QuickExplain.csproj`: WPF アプリ本体。`net8.0-windows`、`UseWPF`、`UseWindowsForms`、COM 参照を使用します。
- `QuickExplain/Views`: WPF の `Window` / XAML。`MainWindow`、`SettingWindow`、`SimpleResultWindow`、`PromptEditorWindow` など。
- `QuickExplain/ViewModels`: CommunityToolkit.Mvvm ベースの ViewModel。`[ObservableProperty]`、`[RelayCommand]` を使います。
- `QuickExplain/Models`: `AppConfig`、AIモデル定義、プロンプト、チャットメッセージなど。
- `QuickExplain/Services`: API呼び出し、クリップボード監視、ホットキー、ウィンドウ制御、画像ログ保存などのアプリロジック。
- `QuickExplain/Services/ApiClients`: Google / OpenAI のストリーミングAPIクライアント。Ollama は `OllamaProvider` が `HttpClient` で直接呼び出します。
- `docs/images`: README 用スクリーンショット画像。
- `README.md`: ユーザー向け概要。
- `AGENTS.md`: AIエージェント向け作業ルール。

## 主要な設計メモ

- 設定は `AppConfig.Instance` で管理し、`appconfig.json` に保存します。`appconfig.json` は exe と同じディレクトリへ配置される想定です。
- AIモデルは `AiModel(Name, Type)` で表現します。`AiType` は `Google` / `OpenAi` / `Ollama` です。
- UI 表示用のプロバイダー名は `AiModel.ProviderName` を使います。ViewModel が `AiModel` をラップする場合も、必要なら同名プロパティを公開してください。
- Google のモデル名は API URL 側で `models/{modelName}` として使うため、設定には `models/` なしで保存します。
- Ollama モデルはデフォルトでは追加しません。モデル編集ウィンドウで Ollama から利用可能モデルを列挙し、ユーザーが追加したものだけを `AppConfig.AIModels` に保存します。
- Ollama のモデル保持時間は `AppConfig.OllamaKeepAlive` で管理します。常時保持は Ollama の duration 形式に合わせて `-1m` を使います。
- MainWindow のモデル一覧は UI から追加できるため、ViewModel 側では更新可能なコレクションとして扱います。
- 画像送信時は `ImageLogService` で exe と同じディレクトリ配下の `log` フォルダへ送信画像を保存します。
- SimpleResultWindow モードでは MainWindow と同時に表へ出ないよう、MainWindow を隠す挙動があります。ウィンドウ表示まわりを変更する時は `WindowManager` と `WindowUtilities` を確認してください。

## ビルド・検証の注意

- このプロジェクトは COM 参照を含むため、通常の `dotnet build QuickExplain.sln` では `ResolveComReference` が失敗することがあります。
- 検証ビルドは Visual Studio の MSBuild を使ってください。

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' QuickExplain.sln /p:OutputPath=bin\Verify\ /p:UseSharedCompilation=false
```

- `dotnet build` を実行すると、環境によってリポジトリ直下に `.dotnet/` が作られることがあります。これは不要な生成物なのでコミットしないでください。
- ネットワーク制限下では NuGet 脆弱性データ取得の `NU1900` 警告が出ることがあります。ビルド成功可否はエラー有無で判断してください。
- WPF の XAML で `Window` を追加すると、生成される partial class は public になります。コンストラクタ引数に渡す ViewModel も public にしないと `CS0051` になる場合があります。
- MaterialDesign のアイコンボタンは既存スタイル `MaterialDesignIconForegroundButton` と `materialDesign:PackIcon` を使うと既存UIに馴染みます。
- NuGet 復元がネットワーク制限で失敗する場合は、ローカルキャッシュを使って `RestorePackagesPath=%USERPROFILE%\.nuget\packages` を指定すると検証できることがあります。

## README・ドキュメントの注意

- README 用画像は `docs/images/` に配置してください。ファイル名は `main-window.png` のような英語の kebab-case を使います。
- README では外部の `github.com/user-attachments` より、リポジトリ内画像への相対パスを優先してください。

## 自動更新・リリースの注意

- 自動更新は `Updatum` を使い、GitHub Release の exe アセットを対象にします。
- GitHub リポジトリは `Rinqer0203/QuickExplain` 前提です。リモート URL や README のリンクもこの名前に合わせてください。
- Release アセット名は `QuickExplain-{version}-win-x86.exe` 形式にしてください。`AppUpdateService` の `AssetRegexPattern` と一致しない名前にすると更新対象として検出されません。
