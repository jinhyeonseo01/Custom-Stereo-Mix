# Custom Stereo Mix

Windows에서 PC 재생음을 가상 오디오 케이블로 보내 Discord, OBS, 녹음 앱과 STT 앱의 입력으로 사용하는 데스크톱 프로그램입니다.

## 구성

- 오디오 코어: C# / .NET 10, NAudio, WASAPI loopback
- 화면: React / TypeScript / Vite
- 데스크톱 호스트: WPF + WebView2
- 아이콘: [Lucide](https://lucide.dev/) (ISC 및 일부 Feather 유래 아이콘의 MIT 라이선스)
- 설치기: Inno Setup, Windows x64, 사용자별 설치

React는 표시와 명령을 담당합니다. 실제 오디오 캡처, 믹싱, 리샘플링, DSP와 출력은 C#에서 실행되며 PCM을 브라우저로 전송하지 않습니다. 화면을 새로 고치거나 트레이로 숨겨도 오디오 연결은 유지됩니다.

## 사용 전 추가 설정

이 프로그램으로 스테레오 믹스 기능을 사용하려면 **출력을 입력으로 연결하는 가상 오디오 케이블이 별도로 필요합니다.** 앱 자체가 Windows 마이크 드라이버를 만들지는 않습니다.

1. [VB-CABLE 공식 설치 페이지](https://vb-audio.com/Cable/) 등에서 가상 오디오 케이블을 설치합니다.
2. 앱의 **재생 소리**에 실제 소리가 재생되는 헤드폰 또는 스피커를 선택합니다.
3. **보낼 장치**에 `CABLE Input`을 선택하고 **소리 연결 시작**을 누릅니다.
4. Discord, OBS, 녹음 또는 STT 앱의 입력 장치에서 `CABLE Output`을 선택합니다.

VAC, Hi-Fi Cable, Voicemeeter 등 다른 제품도 대응되는 재생·녹음 장치 쌍으로 사용할 수 있습니다. Windows 기본 장치가 바뀌어도 앱은 저장한 장치 ID를 사용하므로 실제 재생 장치와 선택된 장치가 같은지 확인하세요. 신호 모니터는 앱의 출력 신호이며, 받는 앱의 입력 장치 선택까지 확인하지는 않습니다.

같은 장치를 캡처와 출력에 동시에 사용하는 연결은 차단합니다. 가상 케이블의 녹음 장치를 마이크로 다시 혼합하는 피드백 연결도 차단합니다. 장치가 사라지거나 드라이버가 오류를 보고하면 연결을 중지하고 오류를 표시합니다.

## 화면과 트레이

화이트 화면은 기본 760 × 730이고 작은 화면에서는 내용이 스크롤됩니다. 설정과 사용법은 앱 안의 별도 페이지입니다. 하단의 시작·중지 버튼은 항상 표시됩니다.

- **사용법**은 설정 버튼 왼쪽에 있습니다. 최초 실행 시 자동으로 열리고 **다시 보지 않기**는 기본 체크입니다. 닫거나 다른 페이지로 이동하면 선택이 저장됩니다. 버튼으로는 언제든 다시 볼 수 있습니다.
- **설정 → 프로그램 동작 → 시스템 트레이 사용**은 기본 활성화입니다. 최소화와 닫기는 창을 트레이로 숨기며 오디오를 유지합니다. 트레이의 **종료**는 오디오를 중지하고 설정을 저장한 뒤 실제 종료합니다. 트레이 기능을 끄면 닫기는 실제 종료합니다.
- **설정 초기화**는 확인 후 연결을 중지하고 장치와 모든 설정을 기본값으로 되돌립니다. 취소는 기존 상태를 유지합니다.
- **업데이트 확인**은 설정 최상단에 있습니다. 현재 배포 준비 안내만 표시하며 네트워크 요청이나 다운로드는 하지 않습니다. 향후 `IUpdateService` 구현으로 GitHub Releases를 연결할 수 있습니다.
- WebView2 화면 프로세스 오류 시 **다시 시도**로 화면을 복구할 수 있습니다. 오디오 엔진은 호스트에 남습니다.

## 개발 및 빌드

필요 도구: Windows, .NET 10 SDK, Node.js 24 이상과 npm, Microsoft WebView2 Runtime. 데스크톱 빌드는 npm 의존성 복원과 React 프로덕션 빌드를 함께 수행합니다.

```powershell
dotnet build CustomStereoMix.slnx -c Release
dotnet run --project src\CustomStereoMix.App\CustomStereoMix.App.csproj
```

화면만 개발하려면:

```powershell
cd ui
npm ci
npm run dev
```

브라우저 개발 모드는 가짜 장치로 화면을 미리 봅니다. **실제 오디오는 데스크톱 앱에서만 동작**하며 미리보기 구현은 프로덕션 배포에 포함되지 않습니다.

## 실행 파일 및 설치기

```powershell
.\build_exe.ps1
.\build_installer.ps1 -IsccPath 'C:\path\to\Inno Setup 7\ISCC.exe'
```

- 실행 파일과 필수 동봉 파일: `artifacts\webview2\`
- 설치기: `artifacts\setup\CustomStereoMix-1.1.0-Setup-x64.exe`
- 버전: `Directory.Build.props` (설치기 스크립트의 `-Version`으로도 지정 가능)

설치기 빌드에는 Inno Setup 7을 권장합니다. `.NET`을 포함한 Windows x64 배포이므로 대상 PC에 별도 .NET 설치가 필요하지 않습니다. 설치기는 사용자별 경로에 설치하며 관리자 권한을 요구하지 않습니다. WebView2 Runtime이 없으면 서명을 검증한 Microsoft 공식 부트스트래퍼로 설치합니다. 이때 인터넷 연결이 필요합니다. 가상 오디오 케이블 드라이버는 동봉하지 않으므로 별도 설치하세요.

배포 폴더의 `wwwroot`, WebView2 로더 및 DLL을 실행 파일과 함께 유지해야 합니다. 아이콘과 라이선스도 로컬 파일로 포함됩니다.

## 검증

```powershell
dotnet run --project tests\CustomStereoMix.Tests\CustomStereoMix.Tests.csproj -c Release
```

24개 테스트가 리샘플링 연속성·필터링, 서라운드 다운믹스, 피드백 차단, 프레임 정렬 버퍼, 비정상 숫자 처리, 설정 원자적 저장과 복구, 브리지 입력 검증, 초기화 및 종료 경합을 검증합니다. 실제 출력 장치와 수신 앱 연결은 설치된 드라이버 및 Windows 장치 상태에 따라 별도로 확인해야 합니다.

## 저장 및 진단

- 설정: `%APPDATA%\CustomStereoMix\settings.json`
- 이전 정상 설정: `settings.json.bak`
- 오디오 진단: `%APPDATA%\CustomStereoMix\audio-diagnostics.log`
- WebView2 프로필: `%LOCALAPPDATA%\CustomStereoMix\WebView2`

설정은 변경 후 잠깐의 지연 및 중지·종료 시 저장합니다. 임시 파일과 원자적 교체를 사용하고 손상된 파일은 백업 또는 기본값으로 복구합니다. 엔진 설정은 연결을 중지한 상태에서 변경합니다.

진단 로그는 장치 ID, 포맷, 시작·중지·오류와 버퍼 상태를 기록하고 약 1 MB에서 회전합니다. 캡처 음성은 저장하지 않습니다. 재생 중 무음일 때 WASAPI 패킷이 없어 발생하는 underrun은 정상일 수 있습니다. 가상 케이블을 STT에 연결할 때는 음성 처리 옵션이 PC 오디오를 변형하지 않는지도 확인하세요.

## Git 업로드

`.gitignore`는 .NET/Node 빌드 출력과 의존성, 설치기 및 다운로드한 부트스트래퍼, IDE 상태, 로컬 설정·녹음·로그, 비밀키 및 과거 Python 캐시를 제외합니다. 소스·잠금 파일·아이콘·테스트·일반 문서는 포함합니다. 이미 커밋된 파일에는 ignore 규칙이 적용되지 않으므로 업로드 전 `git status --short`와 `git status --ignored --short`를 확인하세요.

모듈 설명은 [프로젝트 구조](docs/PROJECT_STRUCTURE.md), 외부 리소스 고지는 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)를 참고하세요.

