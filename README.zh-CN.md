<p align="center"><img src="ui/public/app-icon.png" width="64" height="64" alt="Custom Stereo Mix 图标" /></p>
<h1 align="center">Custom Stereo Mix</h1>
<p align="center"><a href="README.md">한국어</a> · <a href="README.en.md">English</a> · <a href="README.ja.md">日本語</a> · 简体中文</p>
<p align="center">Windows 自定义立体声混音 — 将电脑播放声音与麦克风传送到 Discord、OBS、录音及 STT 应用</p>
<p align="center"><a href="#quick-start">快速开始</a> · <a href="#installation">安装</a> · <a href="#build">构建</a> · <a href="#troubleshooting">故障排查</a></p>
<p align="center"><img src="docs/images/main-window.png" width="760" alt="立体声混音界面：播放设备、目标设备、信号监视器、音量和麦克风混音" /></p>
<p align="center"><sub>截图展示韩语界面和真实 Windows 设备。设备名称因电脑而异。</sub></p>

**Custom Stereo Mix 是为 Windows 提供立体声混音功能的桌面应用。** 即使声卡没有内置“立体声混音”输入，也能捕获所选耳机或扬声器的播放声音，通过虚拟音频线传送到其他应用。还可混入真实麦克风，将电脑声音和人声合并为一个输入。

## 主要功能

- **自定义立体声混音** — 捕获所选设备的播放声音，用作 Discord、OBS、录音或语音转文字应用的输入。
- **电脑声音 + 麦克风** — 可选混入真实麦克风，分别调节音量。
- **信号监视与处理** — 实时信号显示、输出音量、可选压缩器和最终限幅器。
- **托盘运行** — 关闭或最小化窗口后保持连接，可从托盘开始、停止或退出。
- **设置自动保存** — 保存设备 ID，备份正常设置，支持损坏恢复与重置。
- **内置指南** — 首次启动说明，以及随时可重新打开的虚拟音频线设置指南。
- **四种语言** — 韩语、英语、日语和简体中文，可在安装时或应用设置中选择。

> **使用前需要额外设置：** 使用立体声混音前，必须**另外安装虚拟音频线**，将播放输出连接到录音输入。可使用 VB-CABLE、VAC 或其他提供配对播放与录音设备的产品。此安装程序未捆绑虚拟音频驱动。

<a id="quick-start"></a>
## 立体声混音快速开始

1. 从 [VB-CABLE 官方页面](https://vb-audio.com/Cable/)安装虚拟音频线，按照官方说明使用管理员权限安装并重启电脑。
2. 在此应用的**播放声音**中，选择实际正在播放声音的耳机或扬声器。
3. 在**目标设备**中选择 `CABLE Input`，然后点击**开始音频连接**。
4. 在 Discord、OBS、录音或 STT 等接收应用中，将**输入设备**设为 `CABLE Output`。

```text
耳机 / 扬声器播放声音
  → Custom Stereo Mix [可选麦克风混音]
  → CABLE Input  [播放设备]
  → CABLE Output [录音设备]
  → Discord / OBS / 录音 / STT
```

其他虚拟音频线也可按同样方式连接：发送到其播放设备，在接收应用中选择配对录音设备。为防止反馈，应用会拒绝连接到同一设备，或将虚拟音频线的录音输入再次用作麦克风混音。

<a id="installation"></a>
## 安装

| 所需组件 | 说明 |
| --- | --- |
| Windows x64 | 当前支持的分发平台。 |
| WebView2 Runtime | 桌面界面所需。如果缺失，安装程序会运行 Microsoft 官方运行时安装程序。 |
| 虚拟音频线 | 需要单独安装，此安装程序未包含音频驱动。 |
| .NET Runtime | 已包含在分发构建中，运行应用无需安装 .NET SDK。 |

可从 [GitHub Releases](https://github.com/jinhyeonseo01/Custom-Stereo-Mix/releases)下载安装程序，或[从源码构建](#build)。推送 `vX.Y.Z` 标签后，GitHub Actions 会创建发布。

- 安装程序：`artifacts\setup\CustomStereoMix-1.2.2-Setup-x64.exe`
- 文件夹分发：运行 `artifacts\webview2\CustomStereoMix.App.exe`

应用安装在当前用户目录。需要安装 WebView2 时必须联网。VB-CABLE 驱动安装另需管理员权限。

**文件夹分发时，请提供完整的 `artifacts\webview2` 目录。** 除可执行文件外，还需要 `wwwroot`、WebView2 加载器及其他 DLL。

## 界面、语言和设置

浅色界面默认大小为 760 × 730。窗口较小时可滚动内容，开始和停止按钮始终保留在底部。

| 功能 | 行为 |
| --- | --- |
| 使用指南 | 位于设置按钮左侧。首次启动自动显示，默认勾选的**不再显示**会在关闭指南时保存。仍可随时手动打开。 |
| 使用系统托盘 | 默认启用。关闭和最小化会隐藏窗口并保持音频连接。托盘的**退出**始终真正退出应用；禁用后关闭按钮也会退出。 |
| 重置设置 | 确认后停止连接并恢复默认值，取消则保持现有状态。 |
| 检查更新 | 位于设置顶部，检查 GitHub 最新正式版本并提供安装程序及更新内容。 |
| 显示语言 | 位于设置 → 应用行为。立即更新界面、托盘和说明，音频连接保持运行。 |

安装开始时可选择语言。优先级为**已保存语言 → 安装时选择的语言 → Windows 显示语言 → 英语**。重新安装会保留在应用中选择的语言。重置后恢复安装语言；文件夹分发使用受支持的 Windows 显示语言作为默认值。

<details>
<summary>查看设置截图</summary>

![更新、托盘和声音处理设置](docs/images/settings.png)

音频引擎与重置设置位于下方，可滚动查看。截图为韩语界面。
</details>

<details>
<summary>查看指南截图</summary>

![虚拟音频线安装和输入输出设备选择指南](docs/images/usage-guide.png)

“安装页面”按钮会打开官方网站，指南内容本身可离线查看。
</details>

<a id="build"></a>
## 从源码构建

所有命令都在**此 README 所在的仓库根目录，使用 PowerShell**执行。

| 工具 | 用途 |
| --- | --- |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | C# 构建。`global.json` 选择 10.0.301 或兼容的更新 .NET 10 SDK。 |
| [Node.js / npm](https://nodejs.org/en/download/) | React 界面和图标构建，推荐 Node.js 24。 |
| [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) | 开发时运行桌面应用。 |
| [Inno Setup 7](https://jrsoftware.org/isdl.php) | 构建四种语言的 Windows 安装程序。 |

首次构建需要联网恢复 NuGet 与 npm 依赖。被删除的缓存与依赖会在需要时重新生成。

### 构建和运行

```powershell
dotnet build CustomStereoMix.slnx -c Release
dotnet run --project src\CustomStereoMix.App\CustomStereoMix.App.csproj -c Release
```

应用项目已连接 npm 依赖恢复和 React 生产构建，构建 C# 时会自动构建界面。

### 使用便捷脚本构建分发版本

```powershell
.\build_exe.ps1
```

脚本依次检查 npm 依赖、重新生成应用图标与 favicon，再执行 `dotnet publish`。它只是便捷工具，并非必须使用的独立构建系统。结果输出到 `artifacts\webview2\`，为包含 .NET 的 Windows x64 分发版本。

### 直接执行发布命令

```powershell
dotnet publish src\CustomStereoMix.App\CustomStereoMix.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o artifacts\webview2
```

此命令自动构建 React，并使用仓库中已有的图标。如果修改了图标源文件或生成逻辑，请先运行：

```powershell
.\tools\build_icons.ps1
```

### 生成安装程序

Inno Setup 位于默认安装路径或 PATH 中时：

```powershell
.\build_installer.ps1
```

编译器位于其他位置时：

```powershell
.\build_installer.ps1 -IsccPath 'C:\Program Files\Inno Setup 7\ISCC.exe'
```

脚本先发布应用，再下载并验证 Microsoft 签名的官方 WebView2 引导安装程序，最后生成安装程序。Inno Setup 7 提供四种安装界面翻译。

| 产物 | 默认路径 |
| --- | --- |
| 应用及必需文件 | `artifacts\webview2\` |
| Windows x64 安装程序 | `artifacts\setup\CustomStereoMix-1.2.2-Setup-x64.exe` |

默认版本由 `Directory.Build.props` 管理。安装脚本的 `-Version` 参数可覆盖应用版本和安装程序版本。

### 通过标签自动发布

[GitHub Actions 工作流](.github/workflows/release.yml)会检出标签指向的提交并在 Windows 上构建。提交并推送代码后：

```powershell
git tag -a v1.2.0 -m "Release v1.2.0"
git push origin v1.2.0
```

标签版本会应用到程序和安装文件名。测试通过后，使用 Inno Setup 构建，将安装程序及 SHA-256 文件上传到发布草稿，然后公开发布。未完成的草稿不会出现在更新提示中，已公开的同版本发布不会被覆盖。

工作流使用 GitHub 自动提供的 `GITHUB_TOKEN`，无需个人访问令牌。重试步骤见[发布指南](docs/RELEASING.md)（韩语）。

### Visual Studio 与界面开发

可在支持 .NET 10 的 Visual Studio 中打开 `CustomStereoMix.slnx`，使用 Release 构建。环境需能执行 `dotnet` 和 `npm`。文件夹发布选择 Release / win-x64 / self-contained；安装程序另行运行脚本生成。

```powershell
cd ui
npm ci
npm run dev
```

浏览器预览使用模拟设备和信号，并显示预览提示。真实音频连接在桌面应用中运行。预览桥接代码不会包含在生产包中。

### 测试

```powershell
dotnet run --project tests\CustomStereoMix.Tests\CustomStereoMix.Tests.csproj -c Release
```

测试覆盖重采样连续性与滤波、环绕声缩混、缓冲区、反馈阻止、设置保存与恢复、桥接验证、开始/重置/退出竞态、更新检查和语言保存。还需在目标电脑上检查实际设备与接收应用。

<a id="troubleshooting"></a>
## 故障排查

| 症状 | 检查内容 |
| --- | --- |
| 监视器有信号，但 Discord/STT 无声 | 接收应用应选择配对的**录音设备**。VB-CABLE 对应 `CABLE Output`。监视器无法验证接收应用的设置。 |
| 信号一直为 0% | 选择实际正在播放的设备。保存的设备 ID 不会自动跟随 Windows 默认设备更改。 |
| 列表中没有设备 | 安装音频线、重启后刷新。已知虚拟音频线录音输入会从麦克风列表排除，以防反馈。 |
| 声音断续 | 停止连接后检查延迟和缓冲区。缓冲区不能小于延迟；默认 48 kHz、立体声、50 ms 延迟、250 ms 缓冲区。 |
| 关闭窗口后继续运行 | 默认托盘行为。托盘右键 → 退出，或在设置中禁用托盘。 |
| 界面无法打开 | 检查完整分发目录是否包含 `wwwroot` 和 DLL，确认已安装 WebView2 Runtime。界面进程故障可通过重试恢复。 |
| 构建提示文件被占用 | 退出从该输出目录运行的应用。如果 npm 恢复时出现原生文件锁定，也请停止开发服务器。 |

## 更新

界面准备完成后，每次启动会在后台检查一次 GitHub Releases。只有更高的正式 `vX.Y.Z` 版本且 Windows x64 安装程序已就绪时才弹出提示。点击**稍后**关闭后，仍可从设置中的**检查更新**重新查询。

**下载安装程序**会在浏览器中打开已验证的官方链接。运行下载的安装程序前，请从托盘退出应用。**查看更新内容**打开对应版本的发布页面。没有发布、离线、限流或服务器错误时仍可正常操作音频。草稿与预发布版本不作为更新。

## 设置和诊断

| 数据 | 保存位置 |
| --- | --- |
| 设置 | `%APPDATA%\CustomStereoMix\settings.json` |
| 上一次正常设置 | `%APPDATA%\CustomStereoMix\settings.json.bak` |
| 音频诊断日志 | `%APPDATA%\CustomStereoMix\audio-diagnostics.log` |
| WebView2 配置 | `%LOCALAPPDATA%\CustomStereoMix\WebView2` |

更改后短暂延迟以及停止、退出时会自动保存设置。日志记录设备 ID、格式和缓冲区状态，不保存捕获的音频，约 1 MB 时轮换。静音播放中的 underrun 可能只是没有 WASAPI 数据包，需结合信号和发生时间判断。

## 技术与翻译

| 组件 | 作用 |
| --- | --- |
| C# / .NET 10 / NAudio | WASAPI 捕获与输出、格式转换、重采样、混音和 DSP。 |
| React / TypeScript / Vite | 界面与用户命令。 |
| WPF / WebView2 | 桌面窗口、本地界面、托盘和退出处理。 |
| Lucide | 按钮图标、应用图标和 favicon。 |
| Inno Setup | Windows x64 用户级安装程序。 |

PCM 音频由 C# 处理，界面桥接只传送命令、状态和电平数据，因此刷新或隐藏界面不会停止音频。详见[项目结构](docs/PROJECT_STRUCTURE.md)和[翻译扩展说明](docs/LOCALIZATION.md)。

翻译使用 C# 与 React 共用的固定键和参数。添加资源可注册更多应用语言，安装程序的语言需另外添加。Windows 提供的设备名称保持原样。

## 问题反馈与贡献

反馈时请提供 Windows 和应用版本、播放/目标/接收设备名称、复现步骤和症状。分享诊断日志前请检查设备 ID 等不希望公开的信息。修改后运行相关测试，并检查小窗口、托盘、设置与指南。

`.gitignore` 排除依赖、构建输出、安装程序、本地设置、日志、录音和密钥。源码、锁文件、文档截图与第三方许可证包含在仓库中。

## 第三方组件

[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)和 [licenses](licenses/) 包含第三方声明。Lucide 的 ISC 及源自 Feather 的 MIT 声明位于 [LUCIDE-LICENSE.txt](ui/public/LUCIDE-LICENSE.txt)。

虚拟音频驱动是独立产品，安装、使用及再分发应遵守各厂商规定。VB-CABLE 的 donationware 和捆绑条件请参阅 [VB-Audio 官方许可说明](https://vb-audio.com/Services/licensing.htm)。
