<p align="center"><img src="ui/public/app-icon.png" width="64" height="64" alt="Custom Stereo Mix" /></p>
<h1 align="center">Custom Stereo Mix</h1>
<p align="center"><a href="README.md">한국어</a> · <a href="README.en.md">English</a> · 日本語 · <a href="README.zh-CN.md">简体中文</a></p>
<p align="center">Windowsの再生音と入力を複数のミックスにまとめ、必要な出力へ送信。</p>
<p align="center"><a href="#quick-start">使い始める</a> · <a href="#advanced">詳細モード</a> · <a href="#installation">インストール</a> · <a href="#build">ビルド</a> · <a href="#troubleshooting">問題の解決</a></p>

![複数の入力・ミックス・出力を接続する詳細モード](docs/images/advanced-mode.png)

*画像は2.0のブラウザープレビューです。デバイスとメーターはサンプルであり、実際の音声処理はWindowsデスクトップアプリが行います。*

Custom Stereo Mixは、**PCの再生音・マイク・仮想入力を混合し、複数の出力デバイスへ送るWindowsアプリ**です。簡易モードでは再生元と送信先を選択し、詳細モードではノードと接続線で複数のミックスを構成します。仮想ケーブルを使えばDiscord・OBS・録音・STTアプリの入力へ送信できます。

このREADMEは**2.0.0のソース**を対象としています。実施した検証と未完了の安定性試験は[検証記録](docs/VALIDATION.md)に記載しています。

## 主な機能

| 機能 | 内容 |
|---|---|
| 簡易モード | 基本の再生元・出力、マイク混合、ミックス選択と音量 |
| 詳細モード | 複数の再生音・物理/仮想入力 → 複数のミックス → 複数の出力 |
| ミックス | 入力別の送信音量・ミュート、マスター音量、ミキサードック |
| 音声補正 | コンプレッサー、バス・出力リミッター、信号・クリッピング・エラー表示 |
| 編集 | デバイス検索、ポート接続、詳細パネル、整列、ズーム、取り消し・やり直し |
| プロジェクト | JSON入出力、自動保存、1.x設定の移行とバックアップ |
| アプリ動作 | トレイ、ログイン時の自動起動、更新確認 |
| 言語 | 韓国語・英語・日本語・簡体字中国語 |

<a id="quick-start"></a>

## 簡易モードで使い始める

1. 別のアプリの入力へ送る場合は[VB-CABLE](https://vb-audio.com/Cable/)などを別途インストールします。通常のスピーカー・ヘッドホンへの出力には不要です。
2. 実際に音が出ている再生デバイスを選択します。
3. 送信先を選択します。VB-CABLEでは `CABLE Input` です。
4. 必要に応じてマイク混合を有効にし、入力デバイスと音量を設定します。
5. 接続を開始します。受信アプリの入力は対応する録音側デバイスにします。VB-CABLEでは `CABLE Output` です。

```text
PC再生音 + マイク → Custom Stereo Mix → CABLE Input
  → CABLE Output → Discord / OBS / 録音 / STT
```

![基本の接続とマイク混合スイッチを備える簡易モード](docs/images/simple-mode.png)

複数のミックスがある場合は操作するミックスを選択します。簡易モードで基本接続を変更しても、追加の入力・出力や他のミックスは保持されます。**モード切り替えで音声は停止しません。**

<a id="advanced"></a>

## 詳細モード

上部の切り替えを有効にします。

1. ノード追加またはデバイス一覧から再生音・入力・ミックス・出力を追加します。
2. ソースの右ポートをミックスの左ポートへ、ミックスを出力へドラッグします。詳細パネルの接続先選択でも接続できます。
3. ミックス内で送信音量とミュートを調整します。1つの入力を複数のミックスへ、1つのミックスを複数の出力へ送れます。
4. **…**でデバイスや音声補正を設定します。接続線をクリックすると、その送信音量・ミュート・削除を操作できます。
5. 全体の開始・停止ボタンで接続されたミックスを実行します。

**音量スライダーの中央は0 dB**です。左半分は-60..0 dB、右半分は0..+12 dB。数値入力、ダブルクリックまたは `0` キーでのリセットに対応します。矢印キーは0.1 dB、Shift+矢印は1 dBです。

実行中も音量・ミュート・補正を操作できます。**デバイス・ノード・接続構造は停止後に変更**します。出力デバイスの重複と、同じ再生デバイス・仮想ケーブルへ戻る経路を検出して拒否します。

<details>
<summary>選択したミックスのドック</summary>

![入力別フェーダー・メーター・マスター操作](docs/images/mixer.png)

ツールバーのミキサーボタンから開きます。グラフ内の操作と同じパラメーターを変更します。

</details>

プロジェクトJSONはツールバーから入出力できます。デバイスはWindowsのendpoint IDで保存するため、別のPCでは再指定が必要な場合があります。

<a id="installation"></a>

## インストールと実行

公開されたインストーラーは[GitHub Releases](https://github.com/jinhyeonseo01/Custom-Stereo-Mix/releases)で確認してください。ソースのバージョン変更だけではリリースは公開されません。

- Windows x64とWebView2 Runtimeが必要です。未導入の場合、インストーラーがMicrosoft公式ランタイムを導入します。
- フォルダー配布では `artifacts\webview2\CustomStereoMix.App.exe` を実行します。`wwwroot`とDLLを含む**フォルダー全体**が必要です。
- 仮想ドライバーは同梱しません。self-contained配布には.NETランタイムを含みます。
- 閉じる・最小化は既定でトレイに隠し、音声接続を維持します。終了は**トレイ → 終了**です。
- トレイとログイン時の自動起動は設定で変更できます。自動起動はアプリを開くだけで、音声送信は手動で開始します。

<details>
<summary>設定と使用案内</summary>

![更新・自動起動・言語・トレイ設定](docs/images/settings.png)

![仮想ケーブルの接続案内](docs/images/usage-guide.png)

設定をスクロールすると音声補正・エンジン・初期化があります。言語変更は音声を止めず、画面とトレイに即時反映します。

</details>

<a id="build"></a>

## ソースからビルド

.NET 10 SDK（`global.json`: 10.0.301）、Node.js 24/npm、WebView2 Runtimeを用意し、ルートのPowerShellで実行します。

```powershell
dotnet build CustomStereoMix.slnx -c Release
dotnet run --project src\CustomStereoMix.App -c Release
```

C#ビルドがnpmの復元とReactビルドも実行します。

| 作業 | コマンド | 出力 |
|---|---|---|
| フォルダー配布 | `.\build_exe.ps1` | `artifacts\webview2\` |
| インストーラー | `.\build_installer.ps1` | `artifacts\setup\CustomStereoMix-2.0.0-Setup-x64.exe` |

インストーラーにはInno Setup 7が必要です。別の場所にある場合は `-IsccPath 'C:\path\ISCC.exe'` を指定します。バージョンは `Directory.Build.props` で管理します。アプリアイコンの原本を変更した場合だけ `-RegenerateIcons` を使います。自動配布は[リリース案内](docs/RELEASING.md)を参照してください。

### UIプレビューとテスト

```powershell
cd ui
npm ci
npm run dev
```

`http://127.0.0.1:5173/?advanced` で確認できます。**ブラウザーはサンプルデバイスとメーターを使い、実音声には接続しません。**このブリッジは製品ビルドから除外されます。

ルートへ戻って検証します。

```powershell
dotnet run --project tests\CustomStereoMix.Tests -c Release
dotnet run --project tests\CustomStereoMix.Tests -c Release -- --list-audio-devices
```

グラフ・帰還防止・ミックス・出力バッファー・設定移行/保存・ブリッジ・翻訳・更新を検証します。実デバイスの無音セッションと未実施の試聴・長時間試験は[検証記録](docs/VALIDATION.md)に区別しています。

<a id="troubleshooting"></a>

## 問題の解決

| 症状 | 確認事項 |
|---|---|
| 受信アプリが無音 | ケーブルの録音側（例: `CABLE Output`）を入力に選択 |
| 再生音メーターが動かない | 実際に再生中のデバイスを選択。Windows既定の変更には自動追従しません |
| デバイスがない・オフライン | 停止後に更新。似た名前の別デバイスへ自動変更しません。物理/仮想入力の両方を表示します |
| 帰還経路エラー | 同じデバイス・ケーブルへ戻る経路と外部アプリのルーティングを確認 |
| 一部の出力が失敗 | ノードのエラーとデバイス使用状態を確認。他の出力は継続可能です |
| 音切れ・遅延 | 停止後に遅延/バッファーを調整。既定は48 kHz、ステレオ、50 ms / 250 ms |
| 画面・ビルドが失敗 | WebView2と配布フォルダー全体を確認。再ビルド時は実行中アプリを終了 |

自動再接続、アプリ別キャプチャ、EQ、ミックス間の接続、実行中の構造変更は未対応です。

## 設定・診断・貢献

設定は `%APPDATA%\CustomStereoMix\settings.json`、正常設定のバックアップは `.bak`、初回1.x移行のバックアップは `.v1.bak` です。同じフォルダーの `audio-diagnostics.log` はデバイス・形式・バッファーを記録し、音声は保存しません。WebView2プロファイルは `%LOCALAPPDATA%\CustomStereoMix\WebView2` です。

不具合報告にはアプリ/Windowsのバージョン、デバイス名、再現手順、ノードエラーを添えてください。ログを共有する前にデバイスIDなどを確認してください。

[構造](docs/PROJECT_STRUCTURE.md) · [翻訳](docs/LOCALIZATION.md) · [検証](docs/VALIDATION.md) · [配布](docs/RELEASING.md)

UIはReact・TypeScript・React Flow、ホストはWPF・WebView2、音声はNAudio/WASAPIです。PCMはC#で処理し、ブリッジにはコマンド・状態・メーターのみを渡します。

## ライセンス

プロジェクト独自のコードは[MIT License](LICENSE)で提供します。Copyright (c) 2026 Clrain.

外部コンポーネントは各自のライセンスを維持します。[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)と[licenses](licenses/)に告知を同梱します。UIはMaterial Symbols、既存アプリアイコン/faviconはLucideを使用します。仮想ドライバーは各ベンダーの条件に従います。
