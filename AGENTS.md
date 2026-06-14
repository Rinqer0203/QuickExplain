# QuickExplain エージェントメモ

## 概要

QuickExplain は Windows 向けの WPF デスクトップアプリです。クリップボード、グローバルショートカット、スクリーンショットをトリガーにして、Google / OpenAI / Ollama へ問い合わせ、翻訳・説明・任意プロンプトの結果を表示します。

## 最初に見る場所

- `QuickExplain.sln`: ソリューション。
- `QuickExplain/QuickExplain.csproj`: WPF アプリ本体。`net8.0-windows`、WPF / Windows Forms、COM 参照を使用します。
- `QuickExplain/Views`: 画面と XAML。
- `QuickExplain/ViewModels`: CommunityToolkit.Mvvm ベースの ViewModel。
- `QuickExplain/Models`: 設定、AI モデル、プロンプト、チャットメッセージなど。
- `QuickExplain/Services`: API 呼び出し、クリップボード監視、ホットキー、ウィンドウ制御などのアプリロジック。
- `.github/workflows/release.yml`: GitHub Release 用の自動ビルド。
- `README.md`: ユーザー向け概要。
- `docs/images`: README 用画像。

## 作業ルール

- 日本語を含むファイルは基本的に UTF-8 として扱います。
- 実装判断は、AGENTS.md の説明より周辺コードの既存パターンを優先します。
- 生成物やローカル設定を不要にコミットしないでください。例: `bin/`、`obj/`、`.dotnet/`、`appconfig.json`。
- README 用画像は `docs/images/` に置き、英語の kebab-case ファイル名を使います。

## ビルド・検証

このプロジェクトは COM 参照を含むため、`dotnet build QuickExplain.sln` では `ResolveComReference` が失敗することがあります。検証ビルドは Visual Studio の MSBuild を使ってください。

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' QuickExplain.sln /p:OutputPath=bin\Verify\ /p:UseSharedCompilation=false
```

- ネットワーク制限下では NuGet 脆弱性データ取得の `NU1900` 警告が出ることがあります。ビルド成功可否はエラー有無で判断してください。
- NuGet 復元が失敗する場合は、ローカルキャッシュとして `/p:RestorePackagesPath="$env:USERPROFILE\.nuget\packages"` を指定して検証できることがあります。

## リリース

- 自動更新は `Updatum` を使い、GitHub Release の exe アセットを対象にします。
- Release アセット名は `QuickExplain-{version}-win-x86.exe` 形式にしてください。
- GitHub Actions の `Release` workflow は、`v*` タグの push または手動実行で起動します。Windows 上で `win-x86` の self-contained single-file exe を発行し、workflow artifact と GitHub Release のアセットとしてアップロードします。
