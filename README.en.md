<p align="center"><img src="ui/public/app-icon.png" width="64" height="64" alt="Custom Stereo Mix" /></p>
<h1 align="center">Custom Stereo Mix</h1>
<p align="center"><a href="README.md">한국어</a> · English · <a href="README.ja.md">日本語</a> · <a href="README.zh-CN.md">简体中文</a></p>
<p align="center">Mix Windows playback and capture inputs, then send them to multiple outputs.</p>
<p align="center"><a href="#quick-start">Quick start</a> · <a href="#advanced">Advanced mode</a> · <a href="#installation">Install</a> · <a href="#build">Build</a> · <a href="#troubleshooting">Troubleshooting</a></p>

![Advanced mode: three sources, two mixes and three outputs](docs/images/advanced-mode.png)

*Screenshots show the current 2.0 browser UI preview with sample devices and simulated meters. Actual audio runs in the Windows desktop app.*

Custom Stereo Mix is a Windows app for **mixing playback loopbacks, microphones and virtual capture inputs into multiple output devices**. Simple mode keeps everyday playback routing and microphone controls. Advanced mode lets you connect several mixes with nodes and edges. With a virtual audio cable, Discord, OBS, recording or STT apps can receive a mix as an input.

This README describes the **2.0.0 source version**. Completed checks and remaining stabilization work are recorded in [validation notes](docs/VALIDATION.md).

## Features

| Feature | What it does |
|---|---|
| Simple mode | Primary playback/output routing, optional microphone, mix selection and volume |
| Advanced mode | Multiple playback/physical/virtual sources → multiple mixes → multiple outputs |
| Mixing | Per-send gain and mute, bus master gain, optional mixer dock |
| Processing | Node compressors, bus/output limiters, meters, clipping and device errors |
| Graph editing | Device search, drag connections, inspector, auto arrange, zoom and undo/redo |
| Projects | JSON import/export, automatic persistence and backed-up 1.x migration |
| App behavior | Tray routing, optional login startup and update checks |
| Languages | Korean, English, Japanese and Simplified Chinese |

<a id="quick-start"></a>

## Start in simple mode

1. To feed another app's input, install a virtual cable such as [VB-CABLE](https://vb-audio.com/Cable/) separately. Ordinary speaker/headphone outputs do not require one.
2. Choose the device actually playing audio as the **Playback source**.
3. Choose the **Output target**; for VB-CABLE, use `CABLE Input`.
4. Enable the microphone option if needed, then choose its device and gain.
5. Start routing. In the receiving app, select the paired capture endpoint; for VB-CABLE, use `CABLE Output`.

```text
Playback + microphone
  → Custom Stereo Mix → CABLE Input
  → CABLE Output → Discord / OBS / recording / STT
```

![Simple mode with playback routing and microphone toggle](docs/images/simple-mode.png)

With several mixes, choose the mix to edit. Simple mode preserves additional sends, outputs and other mixes. **Changing modes does not stop audio.**

<a id="advanced"></a>

## Use advanced mode

Enable the header toggle, then:

1. Add playback, capture, mix and output nodes from **Add node** or the device library.
2. Drag source ports into mix inputs, then mix ports into outputs. The inspector's connection selector is an alternative.
3. Adjust each send's gain and mute inside the mix. One source can feed several mixes; one mix can feed several outputs.
4. Open **…** for device and processing controls. Click an edge for send gain, mute or deletion.
5. Use **Start all / Stop all** for connected mixes.

**0 dB is the slider centre.** The left half covers -60..0 dB, the right half 0..+12 dB. Numeric input is available. Double-click or press `0` to reset; arrow keys change 0.1 dB, Shift+arrows 1 dB.

Gain, mute and processing can change while running. **Stop before changing devices, nodes or connection structure.** Duplicate output endpoints and detected playback/virtual-cable return paths are rejected.

<details>
<summary>Selected-mix dock</summary>

![Mixer dock with individual send faders and master controls](docs/images/mixer.png)

The mixer toolbar button opens send faders, meters, mutes and master controls for the selected bus. These edit the same parameters as the graph.

</details>

Import/export project JSON from the toolbar. Devices are bound by Windows endpoint ID, so a project imported on another PC may need device reassignment.

<a id="installation"></a>

## Install and run

Find published installers in [GitHub Releases](https://github.com/jinhyeonseo01/Custom-Stereo-Mix/releases). Changing the source version alone does not publish a release.

- Windows x64 and WebView2 Runtime are required.
- The installer installs the official Microsoft WebView2 Runtime if missing.
- Folder distributions run `artifacts\webview2\CustomStereoMix.App.exe`. Keep the **whole folder**, including `wwwroot` and DLLs.
- Virtual drivers are separate products and are not bundled.
- Self-contained distributions include .NET; building from source requires its SDK.

Closing or minimizing hides the app to the tray by default and preserves routing. Use **tray → Exit** to quit. Tray and login startup options are in settings; automatic startup opens the app without starting audio.

<details>
<summary>Settings and guide</summary>

![Update, startup, language and tray settings](docs/images/settings.png)

![Virtual cable setup guide](docs/images/usage-guide.png)

Scroll settings for processing, engine parameters and reset. Language changes apply immediately without restarting audio.

</details>

<a id="build"></a>

## Build from source

Run from the repository root in PowerShell. Install .NET 10 SDK (`global.json`: 10.0.301), Node.js 24/npm and WebView2 Runtime.

```powershell
dotnet build CustomStereoMix.slnx -c Release
dotnet run --project src\CustomStereoMix.App -c Release
```

The C# build restores npm dependencies and builds the React frontend.

| Task | Command | Output |
|---|---|---|
| Folder distribution | `.\build_exe.ps1` | `artifacts\webview2\` |
| Installer | `.\build_installer.ps1` | `artifacts\setup\CustomStereoMix-2.0.0-Setup-x64.exe` |

Installer builds require Inno Setup 7. Pass `-IsccPath 'C:\path\ISCC.exe'` for a custom location. The default version lives in `Directory.Build.props`. Use `-RegenerateIcons` only when changing app-icon sources. See [release documentation](docs/RELEASING.md) for tagged releases and updates.

### Browser UI preview

```powershell
cd ui
npm ci
npm run dev
```

Open `http://127.0.0.1:5173/?advanced`. **The browser uses sample devices and meters, without real audio.** Its preview bridge is excluded from production bundles.

### Tests

```powershell
dotnet run --project tests\CustomStereoMix.Tests -c Release
dotnet run --project tests\CustomStereoMix.Tests -c Release -- --list-audio-devices
```

Tests cover graph validation, feedback, mixing, output buffers, migration, persistence, bridge, localization and updates. Native muted-session commands and uncompleted listening/long-run checks are in [validation notes](docs/VALIDATION.md).

<a id="troubleshooting"></a>

## Troubleshooting

| Symptom | Check |
|---|---|
| Receiving app is silent | Select the cable's capture endpoint, such as `CABLE Output`. |
| Playback meter stays silent | Select the endpoint actually playing audio. Saved selections do not follow Windows default changes automatically. |
| Missing/offline device | Stop and refresh. Similar names are not substituted automatically. Physical and virtual capture endpoints are listed. |
| Return-path error | Avoid feeding an endpoint or cable back into itself; also inspect routing in external apps. |
| One output fails | Inspect its node error and device usage. Other working outputs can continue. |
| Dropouts/latency | Stop and tune latency/buffer. Defaults: 48 kHz, stereo, 50 ms latency, 250 ms buffer. |
| UI/build fails | Check WebView2 and the complete deployment folder. Close the app before rebuilding locked files. |

Automatic device reconnection, per-app capture, EQ, bus-to-bus chains and live topology edits are not included.

## Settings, diagnostics and contribution

Settings: `%APPDATA%\CustomStereoMix\settings.json`; last valid backup: `.bak`; first 1.x migration backup: `.v1.bak`. `audio-diagnostics.log` in the same directory records device/format/buffer details, not audio. The WebView2 profile is under `%LOCALAPPDATA%\CustomStereoMix\WebView2`.

For issues, include app/Windows versions, device names, steps and node errors. Review device IDs before sharing logs.

[Architecture](docs/PROJECT_STRUCTURE.md) · [Localization](docs/LOCALIZATION.md) · [Validation](docs/VALIDATION.md) · [Releasing](docs/RELEASING.md)

React/TypeScript/React Flow provide the UI; WPF/WebView2 host it; NAudio/WASAPI process PCM in C#. Only commands, state and meters cross the UI bridge.

## License

Original project code is available under the [MIT License](LICENSE). Copyright (c) 2026 Clrain.

Third-party components retain their own licenses. Notices are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and [licenses](licenses/). UI icons use Material Symbols; existing app icons/favicon use Lucide. Virtual drivers follow their vendors' installation, usage and distribution terms.
