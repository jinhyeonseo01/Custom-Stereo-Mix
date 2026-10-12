<p align="center">
  <img src="ui/public/app-icon.png" width="64" height="64" alt="Custom Stereo Mix 아이콘" />
</p>

<h1 align="center">Custom Stereo Mix</h1>

<p align="center">한국어 · <a href="README.en.md">English</a> · <a href="README.ja.md">日本語</a> · <a href="README.zh-CN.md">简体中文</a></p>

<p align="center">
  Windows용 커스텀 스테레오 믹스 — PC 재생음과 마이크를 Discord · OBS · 녹음 · STT 앱의 입력으로
</p>

<p align="center">
  <a href="#quick-start">빠른 시작</a> ·
  <a href="#installation">설치</a> ·
  <a href="#build">빌드</a> ·
  <a href="#troubleshooting">문제 해결</a>
</p>

<p align="center">
  <img src="docs/images/main-window.png" width="760" alt="Custom Stereo Mix 스테레오 믹스 화면: 재생 장치와 가상 케이블 선택, 출력 신호 모니터, 볼륨과 마이크 혼합" />
</p>

<p align="center"><sub>실제 Windows 장치를 선택한 데스크톱 앱 화면입니다. 장치 이름은 PC마다 다릅니다.</sub></p>

**Custom Stereo Mix는 Windows에서 스테레오 믹스 기능을 구현하는 데스크톱 앱입니다.** 사운드 장치에 기본 스테레오 믹스 기능이 없어도, 선택한 헤드폰·스피커의 재생음을 가상 오디오 케이블을 통해 다른 앱의 입력으로 전달할 수 있습니다. 필요하면 실제 마이크도 함께 혼합해 PC 소리와 목소리를 하나의 입력으로 보낼 수 있습니다.

## 주요 기능

- **커스텀 스테레오 믹스** — 선택한 헤드폰·스피커의 재생음을 캡처해 Discord·OBS·녹음·STT 앱의 입력으로 전달
- **PC 소리 + 마이크 혼합** — 스테레오 믹스에 실제 마이크를 선택적으로 함께 보내고 볼륨을 각각 조절
- **신호 확인과 보정** — 실시간 신호 모니터, 출력 볼륨, 선택적 압축과 최종 리미터
- **트레이 실행** — 창을 닫거나 최소화해도 연결 유지, 트레이 메뉴에서 시작·중지·종료
- **설정 자동 저장** — 장치 ID 저장, 정상 설정 백업, 손상된 설정 복구와 초기화
- **내장 사용 안내** — 첫 실행 안내와 언제든 다시 열 수 있는 가상 케이블 설정 가이드
- **다국어 지원** — 한국어, 영어, 일본어, 중국어 간체. 설치 언어를 초기 설정에 반영하며 설정에서 즉시 변경 가능

> **사용 전 추가 조치:** 스테레오 믹스 기능을 사용하려면 출력을 입력으로 연결하는 **가상 오디오 케이블을 별도로 설치**해야 합니다. VB-CABLE, VAC 등 대응되는 재생·녹음 장치 쌍을 가진 제품을 사용할 수 있습니다.

<a id="quick-start"></a>

## 스테레오 믹스 빠른 시작

1. [VB-CABLE 공식 페이지](https://vb-audio.com/Cable/)에서 가상 케이블을 설치합니다. VB-CABLE 안내에 따라 관리자 권한으로 설치하고 재부팅합니다.
2. Custom Stereo Mix의 **재생 소리**에서 실제 소리가 재생되는 헤드폰·스피커를 선택합니다.
3. **보낼 장치**에서 `CABLE Input`을 선택하고 **소리 연결 시작**을 누릅니다.
4. Discord·OBS·녹음·STT 앱의 **입력 장치**를 `CABLE Output`으로 선택합니다.

```text
헤드폰·스피커 재생음
  → Custom Stereo Mix [선택적 마이크 혼합]
  → CABLE Input  [재생 장치]
  → CABLE Output [녹음 장치]
  → Discord / OBS / 녹음 / STT
```

다른 가상 케이블도 같은 방식으로 해당 제품의 재생 장치로 보내고, 받는 앱에서 짝이 되는 녹음 장치를 선택합니다. 같은 장치로 되돌리는 연결과 가상 케이블의 녹음 장치를 마이크로 재혼합하는 연결은 피드백 방지를 위해 차단합니다.

<a id="installation"></a>

## 설치

| 필요한 항목 | 안내 |
| --- | --- |
| Windows x64 | 현재 배포 대상입니다. |
| WebView2 Runtime | 데스크톱 화면에 필요합니다. 설치기는 없을 때 Microsoft 공식 런타임 설치를 진행합니다. |
| 가상 오디오 케이블 | 별도 설치가 필요합니다. 현재 설치기에 드라이버가 동봉되어 있지 않습니다. |
| .NET Runtime | 배포용 빌드에 포함됩니다. 완성된 앱을 실행할 때 .NET SDK 설치는 필요하지 않습니다. |

설치기는 [GitHub Releases](https://github.com/jinhyeonseo01/Custom-Stereo-Mix/releases)에서 받을 수 있습니다. 첫 릴리즈는 `vX.Y.Z` 태그를 푸시하면 생성됩니다. 직접 빌드하려면 아래 [빌드 안내](#build)를 참고하세요.

- **설치기:** `artifacts\setup\CustomStereoMix-1.2.2-Setup-x64.exe`
- **폴더 배포:** `artifacts\webview2\CustomStereoMix.App.exe` 실행

설치기는 사용자별 경로에 설치합니다. WebView2 Runtime 설치가 필요하면 인터넷 연결이 필요합니다. VB-CABLE의 드라이버 설치에는 별도로 관리자 권한이 필요합니다.

**폴더 배포 시 `artifacts\webview2` 전체를 전달하세요.** 실행 파일과 함께 `wwwroot`, WebView2 로더와 DLL이 있어야 합니다.

## 화면과 설정

화이트 화면은 기본 760 × 730이며 작은 창에서는 내용이 스크롤됩니다. 시작·중지 버튼은 하단에 유지됩니다.

| 기능 | 동작 |
| --- | --- |
| 사용법 | 설정 버튼 왼쪽에서 열 수 있습니다. 첫 실행에는 자동 표시되며, 기본 체크된 **다시 보지 않기**를 닫을 때 저장합니다. 버튼으로는 언제든 다시 볼 수 있습니다. |
| 시스템 트레이 사용 | 기본 활성화입니다. 닫기·최소화는 트레이로 숨기고 오디오 연결을 유지합니다. 트레이의 **종료**는 실제 종료합니다. 기능을 끄면 닫기도 실제 종료합니다. |
| 설정 초기화 | 확인 후 연결을 중지하고 모든 설정을 기본값으로 복원합니다. 취소하면 기존 상태를 유지합니다. |
| 업데이트 확인 | 설정 최상단에 있습니다. GitHub의 최신 정식 릴리즈를 확인합니다. 새 버전의 설치기가 있으면 알림을 표시하고 다운로드 또는 변경 사항 페이지를 열 수 있습니다. |
| 표시 언어 | 설정 → 프로그램 동작에서 변경합니다. 화면·트레이·안내에 즉시 반영되며 오디오 연결은 유지됩니다. |

설치 시작 시 언어를 선택할 수 있습니다. 앱은 **저장한 언어 → 설치 시 선택한 언어 → Windows 표시 언어 → 영어** 순서로 적용합니다. 기존 설정에서 직접 선택한 언어는 재설치해도 유지합니다. 초기화하면 설치 시 선택한 언어로 돌아가며, 폴더 배포에서는 Windows 표시 언어를 기본값으로 사용합니다.

<details>
<summary>설정 화면 보기</summary>

![설정 화면: 업데이트 확인, 트레이 동작, 소리 보정](docs/images/settings.png)

오디오 엔진 설정과 초기화 항목은 아래로 스크롤하면 확인할 수 있습니다.

</details>

<details>
<summary>사용법 화면 보기</summary>

![가상 오디오 케이블 설치와 입력·출력 선택을 설명하는 사용법 화면](docs/images/usage-guide.png)

설치 페이지 버튼으로 공식 사이트를 열 수 있으며, 안내 내용은 오프라인에서도 볼 수 있습니다.

</details>

<a id="build"></a>

## 소스에서 빌드

모든 명령은 **프로젝트 루트 — 이 README가 있는 폴더 — 의 PowerShell**에서 실행합니다.

### 빌드 환경

| 도구 | 필요한 경우 |
| --- | --- |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 모든 C# 빌드. `global.json` 기준 10.0.301 및 호환되는 최신 10.0 SDK를 사용합니다. |
| [Node.js / npm](https://nodejs.org/en/download/) | React UI와 아이콘 빌드. Node.js 24를 권장합니다. |
| [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) | 개발 중 데스크톱 앱 실행 |
| [Inno Setup 7](https://jrsoftware.org/isdl.php) | 네 언어를 제공하는 설치기 생성. |

첫 빌드에서 NuGet 및 npm 패키지를 복원하므로 인터넷 연결이 필요합니다. 삭제한 빌드 캐시와 의존성은 필요한 경우 자동으로 다시 생성됩니다.

### 일반 빌드와 실행

```powershell
dotnet build CustomStereoMix.slnx -c Release
dotnet run --project src\CustomStereoMix.App\CustomStereoMix.App.csproj -c Release
```

`.csproj`에 npm 복원과 React 프로덕션 빌드가 연결되어 있으므로 C# 빌드 시 화면도 함께 빌드됩니다.

### 배포용 실행 파일 — 편의 스크립트

```powershell
.\build_exe.ps1
```

이 스크립트는 **npm 의존성 확인 → 앱 아이콘·favicon 재생성 → `dotnet publish`**를 묶은 편의 도구입니다. 사용이 필수인 별도 빌드 시스템은 아닙니다.

결과는 `artifacts\webview2\`에 생성됩니다. .NET을 포함한 Windows x64 배포이며 폴더 전체가 필요합니다.

### 배포용 실행 파일 — 직접 명령

스크립트 없이 동일한 배포 구성을 만들 수 있습니다.

```powershell
dotnet publish src\CustomStereoMix.App\CustomStereoMix.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o artifacts\webview2
```

React UI는 자동 빌드됩니다. 이 명령은 저장소에 있는 기존 아이콘을 사용합니다. 아이콘 원본이나 생성 방식을 변경했다면 먼저 다음 명령으로 다시 생성하세요.

```powershell
.\tools\build_icons.ps1
```

### 설치기 생성

Inno Setup이 기본 경로에 설치되어 있거나 `ISCC.exe`가 PATH에 있으면:

```powershell
.\build_installer.ps1
```

설치 위치가 다르면 실제 컴파일러 경로를 지정합니다.

```powershell
.\build_installer.ps1 -IsccPath 'C:\Program Files\Inno Setup 7\ISCC.exe'
```

스크립트가 앱 게시까지 수행한 뒤 설치기를 만듭니다. WebView2 공식 부트스트래퍼를 내려받아 Microsoft 서명을 검증하고 동봉합니다.

| 산출물 | 기본 경로 |
| --- | --- |
| 앱 실행 파일 및 필수 파일 | `artifacts\webview2\` |
| Windows x64 설치기 | `artifacts\setup\CustomStereoMix-1.2.2-Setup-x64.exe` |

기본 버전은 `Directory.Build.props`에서 관리합니다. 설치기 스크립트의 `-Version`으로도 지정할 수 있으며 파일 이름에 해당 버전이 반영됩니다.

### 태그로 자동 릴리즈

[GitHub Actions 워크플로](.github/workflows/release.yml)는 `vX.Y.Z` 태그가 푸시된 커밋을 Windows에서 빌드합니다. 예를 들어 변경 사항을 커밋하고 푸시한 뒤:

```powershell
git tag -a v1.2.3 -m "Release v1.2.3"
git push origin v1.2.3
```

태그 버전이 앱 버전과 설치기 이름에 적용됩니다. 테스트가 통과하면 Inno Setup으로 설치기를 만들고, 설치기와 SHA-256 파일을 업로드한 뒤 릴리즈를 공개합니다. 빌드 중인 초안은 앱의 업데이트 확인에 나타나지 않습니다. 이미 공개된 같은 버전의 릴리즈는 덮어쓰지 않습니다.

자동 릴리즈에는 GitHub가 제공하는 `GITHUB_TOKEN`을 사용하므로 별도 개인 액세스 토큰은 필요하지 않습니다. 자세한 단계와 재시도 방법은 [릴리즈·업데이트 안내](docs/RELEASING.md)를 참고하세요.

### Visual Studio에서 빌드

.NET 10을 지원하는 Visual Studio에서 `CustomStereoMix.slnx`를 열고 **Release**로 빌드할 수 있습니다. `dotnet`과 `npm`을 실행할 수 있는 환경이 필요합니다.

폴더 게시 시 **Release / win-x64 / self-contained**를 선택하면 위 직접 게시 명령에 해당하는 구성을 만들 수 있습니다. 설치기 생성은 `build_installer.ps1`을 별도로 실행합니다.

### React 화면만 개발

```powershell
cd ui
npm ci
npm run dev
```

브라우저 미리보기는 가짜 장치와 신호를 사용하며 화면에 미리보기 표시가 나옵니다. 실제 오디오 연결은 데스크톱 앱에서 동작합니다. 미리보기 브리지는 프로덕션 번들에 포함되지 않습니다.

### 테스트

```powershell
dotnet run --project tests\CustomStereoMix.Tests\CustomStereoMix.Tests.csproj -c Release
```

리샘플링 연속성·필터링, 서라운드 다운믹스, 링 버퍼, 피드백 차단, 설정 저장·복구, UI 브리지 검증과 시작·초기화·종료 경합을 확인합니다. 실제 장치 및 수신 앱 연결은 해당 PC에서도 확인해야 합니다.

<a id="troubleshooting"></a>

## 문제 해결

| 증상 | 확인할 항목 |
| --- | --- |
| 모니터에는 신호가 있는데 Discord·STT에서는 무음 | 받는 앱에서 짝이 되는 **녹음 장치**를 선택했는지 확인합니다. VB-CABLE 기준 `CABLE Output`입니다. 모니터는 수신 앱의 설정까지 확인하지 않습니다. |
| 신호가 계속 0% | **재생 소리**가 실제 재생 장치와 같은지 확인합니다. 장치 선택은 저장된 ID를 사용하므로 Windows 기본 장치가 바뀌어도 자동으로 따라 바뀌지 않습니다. |
| 장치가 목록에 없음 | 가상 케이블 설치 및 재부팅 후 **새로고침**합니다. 마이크 목록에는 피드백 방지를 위해 알려진 가상 케이블 녹음 장치가 제외됩니다. |
| 소리가 끊김 | 연결을 중지하고 엔진의 지연·버퍼 설정을 확인합니다. 버퍼는 지연보다 작게 설정할 수 없습니다. 기본값은 48 kHz, 스테레오, 지연 50 ms, 버퍼 250 ms입니다. |
| 닫기를 눌러도 계속 실행됨 | 기본 트레이 동작입니다. 트레이 우클릭 **종료**를 사용하거나 설정에서 트레이 사용을 끕니다. |
| 화면이 열리지 않음 | `wwwroot`와 DLL을 포함한 전체 배포 폴더인지, WebView2 Runtime이 설치되어 있는지 확인합니다. 화면 프로세스 오류는 **다시 시도**로 복구할 수 있습니다. |
| 빌드 중 파일 사용 오류 | 같은 출력 폴더에서 실행 중인 앱을 종료합니다. npm 복원 중 네이티브 파일 잠금 오류라면 개발 서버도 종료한 뒤 다시 빌드합니다. |

## 업데이트

앱 화면이 준비되면 시작 시 한 번 GitHub Releases를 백그라운드에서 확인합니다. 더 높은 `vX.Y.Z` 버전의 Windows x64 설치기가 준비되어 있을 때만 팝업이 표시됩니다. **나중에**로 닫아도 설정의 **업데이트 확인**으로 다시 확인할 수 있습니다.

**설치기 다운로드**는 기본 브라우저로 공식 릴리즈 설치기 링크를 엽니다. 다운로드한 설치기를 실행하기 전 트레이에서 앱을 종료하세요. **변경 사항 보기**는 해당 버전의 릴리즈 페이지를 엽니다.

릴리즈가 없거나 오프라인·조회 제한·서버 오류가 발생해도 앱은 계속 사용할 수 있습니다. 확인 결과는 설정에 표시되며 오디오 시작·중지는 네트워크 응답을 기다리지 않습니다. 초안과 사전 릴리즈는 업데이트 대상에서 제외됩니다.

## 설정과 진단

| 데이터 | 저장 위치 |
| --- | --- |
| 설정 | `%APPDATA%\CustomStereoMix\settings.json` |
| 이전 정상 설정 | `%APPDATA%\CustomStereoMix\settings.json.bak` |
| 오디오 진단 로그 | `%APPDATA%\CustomStereoMix\audio-diagnostics.log` |
| WebView2 프로필 | `%LOCALAPPDATA%\CustomStereoMix\WebView2` |

설정은 변경 후 잠깐의 지연 및 중지·종료 시 자동 저장됩니다. 진단 로그에는 장치 ID, 포맷과 버퍼 상태 등이 기록되며 캡처 오디오는 저장하지 않습니다. 로그는 약 1 MB에서 회전합니다. 무음 재생 중 발생하는 underrun은 WASAPI 패킷이 없는 상태일 수 있으므로 신호와 발생 시점을 함께 확인하세요.

## 기술 구성

| 구성 | 역할 |
| --- | --- |
| C# / .NET 10 / NAudio | WASAPI 캡처·출력, 오디오 변환, 리샘플링, 믹싱과 DSP |
| React / TypeScript / Vite | 화면과 사용자 명령 |
| WPF / WebView2 | 데스크톱 창, 로컬 UI, 트레이와 종료 처리 |
| Lucide | 버튼 아이콘, 앱 아이콘과 favicon |
| Inno Setup | Windows x64 사용자별 설치기 |

실제 PCM은 C#에서 처리합니다. UI 브리지는 명령·상태·미터만 전달하므로 화면을 새로 고치거나 트레이로 숨겨도 오디오 연결을 유지합니다. 모듈별 역할과 메시지 흐름은 [프로젝트 구조](docs/PROJECT_STRUCTURE.md)를 참고하세요.

번역은 C#·React가 공유하는 고정 메시지 키와 매개변수를 사용합니다. 리소스 파일을 추가하면 새 앱 언어가 등록되며, 설치기 언어는 별도로 추가합니다. 확장 방법은 [다국어 구조 안내](docs/LOCALIZATION.md)에 있습니다. Windows가 제공하는 장치 이름은 원문을 유지합니다.

## 문제 제보와 기여

문제를 제보할 때는 Windows 및 앱 버전, 재생·출력·수신 장치 이름, 재현 단계와 증상을 함께 적어 주세요. 진단 로그를 첨부한다면 장치 ID 등 공개하고 싶지 않은 정보를 확인한 뒤 공유하세요.

변경 사항은 관련 테스트와 함께 확인하고, 화면을 변경했다면 데스크톱 앱의 작은 창·트레이·설정·사용법 동작도 확인하세요.

`.gitignore`는 의존성, 빌드 출력, 설치기, 로컬 설정·로그·녹음과 비밀키를 제외합니다. 소스, 잠금 파일, 문서용 화면 캡처와 외부 라이선스는 포함합니다. 업로드 전 `git status --short`를 확인하세요.

## 외부 구성요소와 라이선스

[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)와 [licenses](licenses/)에 외부 구성요소의 고지를 동봉합니다. Lucide 아이콘의 ISC 및 Feather 유래 아이콘 MIT 고지는 [LUCIDE-LICENSE.txt](ui/public/LUCIDE-LICENSE.txt)에 있습니다.

VB-CABLE 등 가상 오디오 드라이버는 별도 제품입니다. 해당 제품의 설치·사용·배포 조건은 각 제작사의 안내를 따릅니다. VB-CABLE의 donationware 및 동봉 조건은 [VB-Audio 공식 라이선스 안내](https://vb-audio.com/Services/licensing.htm)에서 확인할 수 있습니다.
