<p align="center"><img src="ui/public/app-icon.png" width="64" height="64" alt="Custom Stereo Mix" /></p>
<h1 align="center">Custom Stereo Mix</h1>
<p align="center"><a href="README.md">한국어</a> · <a href="README.en.md">English</a> · <a href="README.ja.md">日本語</a> · 简体中文</p>
<p align="center">将Windows播放音频与输入组合成多个混音组，发送到所需输出。</p>
<p align="center"><a href="#quick-start">快速开始</a> · <a href="#advanced">高级模式</a> · <a href="#installation">安装</a> · <a href="#build">构建</a> · <a href="#troubleshooting">故障排查</a></p>

![多个输入、混音组和输出的高级模式](docs/images/advanced-mode.png)

*截图来自当前2.0浏览器UI预览，设备和电平为示例。实际音频由Windows桌面应用处理，以下截图同样来自预览。*

Custom Stereo Mix是一款**将PC播放音频、麦克风和虚拟输入混合后发送到多个输出设备的Windows应用**。简易模式保留播放转发与麦克风控制，高级模式通过节点和连接线配置多个混音组。使用虚拟音频线时，Discord、OBS、录音或STT应用可将混音作为输入。

本README针对**2.0.0源码版本**。已完成检查和剩余稳定性验证见[验证记录](docs/VALIDATION.md)。

## 主要功能

| 功能 | 用途 |
|---|---|
| 简易模式 | 基本播放/输出连接、可选麦克风、混音选择和音量 |
| 高级模式 | 多个播放音频及物理/虚拟输入 → 多个混音组 → 多个输出 |
| 混音控制 | 每条发送的增益/静音、主音量、可选混音器面板 |
| 声音处理 | 压缩器、混音/输出限幅器、电平、削波和设备错误 |
| 图编辑 | 设备搜索、端口拖拽、详情面板、自动排列、缩放、撤销/重做 |
| 项目 | JSON导入/导出、自动保存、1.x设置迁移及备份 |
| 应用行为 | 托盘、可选登录自启动、更新检查 |
| 语言 | 韩语、英语、日语、简体中文 |

<a id="quick-start"></a>

## 从简易模式开始

1. 如需发送到其他应用的输入，单独安装[VB-CABLE](https://vb-audio.com/Cable/)等虚拟音频线。普通扬声器/耳机输出无需安装。
2. 选择实际正在播放声音的设备作为播放来源。
3. 选择目标输出；VB-CABLE使用 `CABLE Input`。
4. 如需加入人声，启用麦克风混音并选择设备和音量。
5. 开始连接。接收应用选择配对的录音端；VB-CABLE使用 `CABLE Output`。

```text
PC播放音频 + 麦克风 → Custom Stereo Mix → CABLE Input
  → CABLE Output → Discord / OBS / 录音 / STT
```

![简易模式的播放转发和麦克风开关](docs/images/simple-mode.png)

有多个混音组时，选择要操作的混音组。简易模式编辑基本连接，同时保留额外输入、输出和其他混音组。**切换模式不会停止音频。**

<a id="advanced"></a>

## 高级模式

打开顶部开关后：

1. 从节点添加或设备列表加入播放、输入、混音与输出节点。
2. 将来源右侧端口拖到混音左侧端口，再将混音连接到输出。详情中的目标选择器也可建立连接。
3. 在混音节点内调节各输入的发送增益与静音。同一输入可进入多个混音组，同一混音可送往多个输出。
4. **…**打开设备与处理参数。点击连接线可调节其发送音量、静音或删除。
5. 使用全部开始/全部停止控制已连接的混音组。

**音量滑块中央为0 dB**。左半段为-60..0 dB，右半段为0..+12 dB，支持直接输入数字。双击或按 `0` 重置，方向键调整0.1 dB，Shift+方向键调整1 dB。

运行中可调整增益、静音和处理参数。**设备、节点与连接结构须停止后修改。**重复输出以及返回同一播放设备/虚拟音频线的已检测路径会被拒绝。

<details>
<summary>所选混音组的混音器面板</summary>

![输入推子、电平、静音和主音量](docs/images/mixer.png)

工具栏混音器按钮打开该面板，其参数与节点内控制同步。

</details>

工具栏支持项目JSON导入/导出。设备按Windows endpoint ID保存，因此在其他PC导入后可能需要重新指定设备。

<a id="installation"></a>

## 安装和运行

已发布安装包见[GitHub Releases](https://github.com/jinhyeonseo01/Custom-Stereo-Mix/releases)。修改源码版本本身不会发布新版本。

- 需要Windows x64及WebView2 Runtime。缺失时，安装程序会安装Microsoft官方运行时。
- 文件夹分发运行 `artifacts\webview2\CustomStereoMix.App.exe`，必须保留含 `wwwroot` 和DLL的**整个文件夹**。
- 虚拟驱动不随应用提供。self-contained分发包含.NET运行时。
- 默认关闭/最小化会隐藏到托盘并保持音频。真正退出使用**托盘 → 退出**。
- 托盘和登录自启动可在设置中调整。自启动只打开应用，音频发送须手动开始。

<details>
<summary>设置和使用指南</summary>

![更新、自启动、语言和托盘设置](docs/images/settings.png)

![虚拟音频线连接指南](docs/images/usage-guide.png)

向下滚动设置可查看声音处理、引擎和重置。切换语言会立即更新界面和托盘，不会停止音频。

</details>

<a id="build"></a>

## 从源码构建

准备.NET 10 SDK（`global.json`: 10.0.301）、Node.js 24/npm及WebView2 Runtime，在项目根目录PowerShell中运行：

```powershell
dotnet build CustomStereoMix.slnx -c Release
dotnet run --project src\CustomStereoMix.App -c Release
```

C#构建自动执行npm恢复和React构建。

| 任务 | 命令 | 产物 |
|---|---|---|
| 文件夹分发 | `.\build_exe.ps1` | `artifacts\webview2\` |
| 安装包 | `.\build_installer.ps1` | `artifacts\setup\CustomStereoMix-2.0.0-Setup-x64.exe` |

安装包需要Inno Setup 7。自定义路径使用 `-IsccPath 'C:\path\ISCC.exe'`。默认版本位于 `Directory.Build.props`。仅修改应用图标源文件时使用 `-RegenerateIcons`。自动发布见[发布指南](docs/RELEASING.md)。

### UI预览和测试

```powershell
cd ui
npm ci
npm run dev
```

打开 `http://127.0.0.1:5173/?advanced`。**浏览器使用示例设备和电平，不连接真实音频。**预览桥接不会进入生产包。

回到项目根目录运行：

```powershell
dotnet run --project tests\CustomStereoMix.Tests -c Release
dotnet run --project tests\CustomStereoMix.Tests -c Release -- --list-audio-devices
```

测试覆盖图验证、反馈、混音、输出缓冲、设置迁移/保存、桥接、翻译和更新。实际设备静音会话命令及尚未完成的听音/长时间验证见[验证记录](docs/VALIDATION.md)。

<a id="troubleshooting"></a>

## 故障排查

| 症状 | 检查 |
|---|---|
| 接收应用无声 | 选择音频线的录音端，如 `CABLE Output` |
| 播放电平不动 | 选择实际播放设备；保存的选择不会自动跟随Windows默认设备变化 |
| 设备缺失/离线 | 停止后刷新，不会自动替换成相似名称；物理与虚拟输入都会列出 |
| 返回路径错误 | 避免送回同一设备/音频线，同时检查外部应用路由 |
| 部分输出失败 | 检查节点错误和设备占用，其他正常输出可以继续 |
| 卡顿/延迟 | 停止后调整延迟/缓冲；默认48 kHz、立体声、50 ms / 250 ms |
| 界面/构建失败 | 检查WebView2和完整分发目录，重建前关闭占用文件的应用 |

当前不支持自动重连、应用级捕获、EQ、混音组间连接和运行中的结构编辑。

## 设置、诊断和贡献

设置为 `%APPDATA%\CustomStereoMix\settings.json`，正常备份为 `.bak`，首次1.x迁移备份为 `.v1.bak`。同目录的 `audio-diagnostics.log` 记录设备、格式、缓冲，不保存音频。WebView2配置位于 `%LOCALAPPDATA%\CustomStereoMix\WebView2`。

报告问题时提供应用/Windows版本、设备名称、复现步骤与节点错误。共享日志前检查设备ID等不想公开的信息。

[项目结构](docs/PROJECT_STRUCTURE.md) · [翻译扩展](docs/LOCALIZATION.md) · [验证](docs/VALIDATION.md) · [发布](docs/RELEASING.md)

UI使用React、TypeScript、React Flow，宿主使用WPF、WebView2，音频使用NAudio/WASAPI。PCM在C#处理，桥接仅传递命令、状态和电平。

第三方许可见[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)及[licenses](licenses/)。UI图标使用Material Symbols，现有应用图标/favicon使用Lucide。虚拟驱动遵循各厂商的安装、使用和分发条款。
