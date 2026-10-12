<p align="center"><img src="ui/public/app-icon.png" width="64" height="64" alt="Custom Stereo Mixアイコン" /></p>
<h1 align="center">Custom Stereo Mix</h1>
<p align="center"><a href="README.md">한국어</a> · <a href="README.en.md">English</a> · 日本語 · <a href="README.zh-CN.md">简体中文</a></p>
<p align="center">Windows用カスタムステレオミキサー — PCの再生音とマイクをDiscord・OBS・録音・STTアプリの入力へ</p>
<p align="center"><a href="#quick-start">クイックスタート</a> · <a href="#installation">インストール</a> · <a href="#build">ビルド</a> · <a href="#troubleshooting">問題の解決</a></p>
<p align="center"><img src="docs/images/main-window.png" width="760" alt="再生デバイス、送信先、信号モニター、音量、マイクミックスを表示する画面" /></p>
<p align="center"><sub>画像は韓国語表示のデスクトップアプリです。Windowsのデバイス名はPCによって異なります。</sub></p>

**Custom Stereo Mixは、Windowsでステレオミキサー機能を実現するデスクトップアプリです。** サウンドデバイスに標準のステレオミキサーがなくても、選択したヘッドホン・スピーカーの再生音を仮想オーディオケーブル経由で別のアプリへ送れます。実際のマイクを混ぜて、PCの音と声を1つの入力にまとめることもできます。

## 主な機能

- **カスタムステレオミキサー** — 選択したデバイスの再生音をDiscord・OBS・録音・音声認識アプリへ送信。
- **PC音声とマイクのミックス** — 実際のマイクを任意で追加し、それぞれの音量を調整。
- **モニターと音声補正** — リアルタイム信号表示、出力音量、任意のコンプレッサー、最終段のリミッター。
- **トレイ動作** — ウィンドウを閉じる・最小化しても接続を維持。トレイから開始・停止・終了。
- **設定の自動保存** — デバイスID、正常な設定のバックアップ、破損時の復元と初期化。
- **内蔵ガイド** — 初回起動時の案内と、いつでも開ける仮想ケーブル設定ガイド。
- **4言語対応** — 韓国語、英語、日本語、中国語簡体字。インストール時と設定から選択可能。

> **使用前の追加設定:** ステレオミキサーを使うには、出力を入力へつなぐ**仮想オーディオケーブルを別途インストール**してください。VB-CABLE、VACなど、対応する再生・録音デバイスのペアを持つ製品が使えます。ドライバーは本アプリのインストーラーに同梱していません。

<a id="quick-start"></a>
## ステレオミキサーのクイックスタート

1. [VB-CABLE公式ページ](https://vb-audio.com/Cable/)から仮想ケーブルをインストールします。公式の案内に従い、管理者権限でインストールしてPCを再起動します。
2. 本アプリの**再生音**で、実際に音が出ているヘッドホン・スピーカーを選びます。
3. **送信先デバイス**に `CABLE Input` を選び、**音声接続を開始**を押します。
4. Discord・OBS・録音・STTなどの受信アプリで、**入力デバイス**に `CABLE Output` を選びます。

```text
ヘッドホン・スピーカーの再生音
  → Custom Stereo Mix [任意のマイクミックス]
  → CABLE Input  [再生デバイス]
  → CABLE Output [録音デバイス]
  → Discord / OBS / 録音 / STT
```

ほかの仮想ケーブルでも、その製品の再生デバイスへ送信し、受信アプリで対になる録音デバイスを選びます。同じデバイスに戻す接続や、仮想ケーブルの録音入力をマイクとして再ミックスする接続は、フィードバック防止のため拒否します。

<a id="installation"></a>
## インストール

| 必要なもの | 内容 |
| --- | --- |
| Windows x64 | 現在の配布対象。 |
| WebView2 Runtime | 画面の表示に必要。未導入ならインストーラーがMicrosoft公式のランタイムをインストールします。 |
| 仮想オーディオケーブル | 別途インストールが必要。ドライバーは同梱していません。 |
| .NET Runtime | 配布ビルドに含まれます。実行時に.NET SDKは不要です。 |

インストーラーは[GitHub Releases](https://github.com/jinhyeonseo01/Custom-Stereo-Mix/releases)から入手できます。`vX.Y.Z` タグのプッシュで自動リリースされます。[ソースからビルド](#build)することも可能です。

- インストーラー: `artifacts\setup\CustomStereoMix-1.2.2-Setup-x64.exe`
- フォルダー配布: `artifacts\webview2\CustomStereoMix.App.exe` を実行

ユーザー別のフォルダーにインストールします。WebView2の追加インストールにはインターネット接続が必要です。VB-CABLEドライバーの導入には別途管理者権限が必要です。

**フォルダー配布では `artifacts\webview2` 全体を渡してください。** 実行ファイルに加え、`wwwroot`、WebView2ローダー、各DLLが必要です。

## 画面・言語・設定

明るい画面の初期サイズは760 × 730です。小さいウィンドウでは内容をスクロールでき、開始・停止ボタンは下部に残ります。

| 機能 | 動作 |
| --- | --- |
| 使い方 | 設定ボタンの左側から開けます。初回は自動表示され、初期状態でチェックされた**次回から表示しない**を閉じる時に保存します。手動でいつでも再表示できます。 |
| システムトレイを使用 | 初期状態で有効。閉じる・最小化でトレイに隠し、接続を維持します。トレイの**終了**は完全に終了します。無効にすると閉じるボタンでも終了します。 |
| 設定を初期化 | 確認後に接続を停止し、初期値に戻します。キャンセル時は変更しません。 |
| 更新を確認 | 設定の最上部。GitHubの最新正式リリースを確認し、新しいインストーラーと変更内容を案内します。 |
| 表示言語 | 設定 → アプリの動作。画面・トレイ・案内に即時反映し、音声接続を維持します。 |

インストーラー起動時に言語を選べます。優先順位は**保存した言語 → インストール時の言語 → Windowsの表示言語 → 英語**です。アプリで選択した言語は再インストール後も維持します。初期化するとインストール時の言語に戻り、フォルダー配布では対応するWindowsの言語が初期値になります。

<details>
<summary>設定画面を見る</summary>

![更新、トレイ、音声補正の設定](docs/images/settings.png)

オーディオエンジンと初期化は下へスクロールすると表示されます。画像は韓国語表示です。
</details>

<details>
<summary>使い方の画面を見る</summary>

![仮想ケーブルのインストールと入力・出力の設定ガイド](docs/images/usage-guide.png)

インストールページのボタンで公式サイトを開けます。案内自体はオフラインでも表示できます。
</details>

<a id="build"></a>
## ソースからビルド

すべてのコマンドは、**このREADMEがあるリポジトリのルートでPowerShell**から実行します。

| ツール | 用途 |
| --- | --- |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | C#のビルド。`global.json` は10.0.301または互換性のある新しい.NET 10 SDKを選択します。 |
| [Node.js / npm](https://nodejs.org/en/download/) | React UIとアイコンのビルド。Node.js 24を推奨します。 |
| [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) | 開発時にデスクトップアプリを実行。 |
| [Inno Setup 7](https://jrsoftware.org/isdl.php) | 4言語のWindowsインストーラーを作成。 |

初回はNuGetとnpmの依存関係を復元するため、インターネット接続が必要です。削除されたキャッシュと依存関係は必要に応じて再生成されます。

### ビルドと実行

```powershell
dotnet build CustomStereoMix.slnx -c Release
dotnet run --project src\CustomStereoMix.App\CustomStereoMix.App.csproj -c Release
```

アプリのプロジェクトからnpm復元とReactの本番ビルドを自動実行します。

### スクリプトで配布ビルド

```powershell
.\build_exe.ps1
```

npm依存関係の確認、アプリアイコン・faviconの再生成、`dotnet publish` をまとめた便利なスクリプトです。必須の独自ビルドシステムではありません。結果は.NETを含むWindows x64版として `artifacts\webview2\` に出力されます。

### 直接配布ビルド

```powershell
dotnet publish src\CustomStereoMix.App\CustomStereoMix.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o artifacts\webview2
```

Reactは自動ビルドされ、保存済みのアイコンを使用します。アイコンの元データや生成処理を変更した場合は、先に再生成します。

```powershell
.\tools\build_icons.ps1
```

### インストーラーを作成

Inno Setupが標準の場所またはPATHにある場合:

```powershell
.\build_installer.ps1
```

別の場所にある場合:

```powershell
.\build_installer.ps1 -IsccPath 'C:\Program Files\Inno Setup 7\ISCC.exe'
```

アプリの配布ビルド後、Microsoft署名を確認した公式WebView2ブートストラッパーを同梱し、インストーラーを作成します。Inno Setup 7は4言語のインストーラー翻訳を提供します。

| 出力 | 初期パス |
| --- | --- |
| アプリと必要なファイル | `artifacts\webview2\` |
| インストーラー | `artifacts\setup\CustomStereoMix-1.2.2-Setup-x64.exe` |

初期バージョンは `Directory.Build.props` で管理します。`-Version` を指定するとアプリとインストーラーのバージョンを上書きできます。

### タグによる自動リリース

[GitHub Actions](.github/workflows/release.yml)はタグの指すコミットをWindowsでビルドします。変更をコミット・プッシュした後:

```powershell
git tag -a v1.2.0 -m "Release v1.2.0"
git push origin v1.2.0
```

タグのバージョンをアプリとファイル名に反映します。テスト成功後、Inno Setupで作成したインストーラーとSHA-256ファイルを下書きへアップロードしてから公開します。未完成の下書きは更新通知に表示せず、公開済みの同じリリースは上書きしません。GitHubの `GITHUB_TOKEN` を使用するため、個人トークンは不要です。詳しくは[リリースガイド](docs/RELEASING.md)を参照してください（韓国語）。

### Visual Studioと画面開発

.NET 10対応のVisual Studioで `CustomStereoMix.slnx` を開き、Releaseでビルドできます。`dotnet` と `npm` が実行できる環境が必要です。フォルダー公開はRelease / win-x64 / self-containedを選び、インストーラーは別途スクリプトで作成します。

```powershell
cd ui
npm ci
npm run dev
```

ブラウザープレビューは模擬デバイスと信号を使用し、プレビュー表示が付きます。実際の音声接続はデスクトップアプリで動作します。プレビューブリッジは本番バンドルに含まれません。

### テスト

```powershell
dotnet run --project tests\CustomStereoMix.Tests\CustomStereoMix.Tests.csproj -c Release
```

リサンプリングの連続性・フィルター、サラウンドのダウンミックス、バッファー、フィードバック防止、設定の保存・復元、ブリッジ検証、開始・初期化・終了の競合、更新、言語保存を確認します。実際のデバイスと受信アプリも対象PCで確認してください。

<a id="troubleshooting"></a>
## 問題の解決

| 症状 | 確認すること |
| --- | --- |
| 信号はあるのにDiscord・STTでは無音 | 受信アプリで対になる**録音デバイス**を選びます。VB-CABLEなら `CABLE Output`。モニターでは受信アプリの設定までは確認できません。 |
| 信号が0%のまま | 実際に再生しているデバイスを選びます。保存したデバイスIDはWindowsの既定デバイス変更に自動追従しません。 |
| デバイスがない | 仮想ケーブルの導入・再起動後に更新します。既知の仮想ケーブル録音入力は、フィードバック防止のためマイク一覧から除外します。 |
| 音が途切れる | 接続を止めて遅延・バッファーを確認します。バッファーは遅延以上が必要です。初期値は48 kHz、ステレオ、遅延50 ms、バッファー250 ms。 |
| 閉じても動作し続ける | 初期設定のトレイ動作です。トレイを右クリック → 終了、または設定でトレイを無効にします。 |
| 画面が開かない | `wwwroot` とDLLを含むフォルダー全体、WebView2 Runtimeを確認します。画面プロセスの障害は再試行で復元できます。 |
| ビルド時にファイルが使用中 | 出力先で実行中のアプリを終了します。npmのネイティブファイルがロックされている場合は開発サーバーも停止します。 |

## 更新

画面の準備後、起動ごとに1回バックグラウンドでGitHub Releasesを確認します。より新しい正式版 `vX.Y.Z` と利用可能なWindows x64インストーラーがある場合のみ通知します。**後で**で閉じても、設定の**更新を確認**から再確認できます。

**インストーラーを入手**はブラウザーで検証済みの公式リンクを開きます。インストーラーを実行する前にトレイから終了してください。**変更内容を見る**はリリースページを開きます。リリースなし、オフライン、アクセス制限、サーバーエラーの場合も音声操作は維持します。下書きとプレリリースは対象外です。

## 設定と診断

| データ | 保存先 |
| --- | --- |
| 設定 | `%APPDATA%\CustomStereoMix\settings.json` |
| 前回の正常な設定 | `%APPDATA%\CustomStereoMix\settings.json.bak` |
| オーディオ診断ログ | `%APPDATA%\CustomStereoMix\audio-diagnostics.log` |
| WebView2プロファイル | `%LOCALAPPDATA%\CustomStereoMix\WebView2` |

変更後の短い遅延と停止・終了時に設定を自動保存します。ログにはデバイスID、形式、バッファー状態を記録し、音声は保存しません。約1 MBでログをローテーションします。無音時のunderrunはWASAPIパケットがない状態の場合があるため、信号と発生時刻を併せて確認します。

## 技術構成と翻訳

| 構成 | 役割 |
| --- | --- |
| C# / .NET 10 / NAudio | WASAPIキャプチャ・出力、変換、リサンプリング、ミックス、DSP。 |
| React / TypeScript / Vite | 画面とコマンド。 |
| WPF / WebView2 | ウィンドウ、ローカルUI、トレイ、終了処理。 |
| Lucide | ボタン・アプリのアイコン、favicon。 |
| Inno Setup | ユーザー別Windows x64インストーラー。 |

PCMはC#で処理し、UIブリッジはコマンド・状態・メーターだけを伝えます。画面の更新・トレイへの非表示でも音声接続を維持します。[構造](docs/PROJECT_STRUCTURE.md)と[翻訳の追加方法](docs/LOCALIZATION.md)を参照してください。

翻訳はC#とReactで共通の固定キーと引数を使用します。リソースを追加するとアプリの言語として登録され、インストーラーの言語は別途追加します。Windowsが提供するデバイス名は変更しません。

## 不具合報告と貢献

Windows・アプリのバージョン、再生・送信先・受信デバイス名、再現手順、症状を記載してください。ログを共有する前に公開したくないデバイスIDを確認してください。関連テストと、小さい画面・トレイ・設定・使い方も確認します。

`.gitignore` は依存関係、ビルド出力、インストーラー、ローカル設定・ログ・録音、秘密情報を除外します。ソース、ロックファイル、文書の画像、外部ライセンスは含めます。

## 外部コンポーネント

[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)と[licenses](licenses/)に告知を同梱します。LucideのISC・Feather由来MITライセンスは[LUCIDE-LICENSE.txt](ui/public/LUCIDE-LICENSE.txt)にあります。

仮想オーディオドライバーは別製品であり、各社の導入・使用・再配布条件に従います。VB-CABLEのdonationwareと同梱条件は[VB-Audio公式ライセンス案内](https://vb-audio.com/Services/licensing.htm)を参照してください。
