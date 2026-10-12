# 프로젝트 구조

```text
ui/                         React + TypeScript 화면, CSS, Lucide 아이콘
src/CustomStereoMix.App/     WPF 창, WebView2 호스트, 트레이와 종료 처리
src/CustomStereoMix.State/   앱 상태, 명령과 설정 저장, JSON UI 브리지
src/CustomStereoMix.Audio/   NAudio/WASAPI 장치, 세션, 리샘플링, DSP, 저장소
src/CustomStereoMix.Core/    장치·설정 모델, 인터페이스, 공통 검증
installer/                  Inno Setup 설치기 소스
tools/                     공식 SVG 기반 앱 아이콘 및 favicon 생성 도구
tests/CustomStereoMix.Tests/ 오디오와 상태 동작 회귀 검증
```

`Core`와 `State`는 Windows UI와 NAudio에 의존하지 않습니다. `App`은 인터페이스를 통해 상태를 조작하며 장치를 직접 열지 않습니다. NAudio 객체의 수명은 `AudioRouteSession`과 `NaudioRouteController`가 관리합니다.

React의 명령은 WebView2 JSON 메시지 → `UiBridge` → `MainWindowViewModel` → 서비스 인터페이스 순서로 전달됩니다. 브리지는 허용된 명령과 설정만 처리하고 전체 변경값을 검증한 뒤 적용합니다. 상태 스냅샷의 revision으로 오래된 응답이 최신 화면 상태를 덮어쓰지 않게 합니다. 미터는 별도 메시지로 전달되고 실제 PCM은 C# 안에서만 흐릅니다.

오디오 경로:

```text
재생 장치 WASAPI loopback
 → float32 변환 및 지속적인 WDL 리샘플링
 → 프레임 정렬된 링 버퍼
 → 선택적 마이크 혼합, 볼륨, 압축, 최종 리미터
 → WASAPI 가상 케이블 재생 장치
 → 수신 앱에서 해당 케이블 녹음 장치 선택
```

WebView2는 배포 폴더의 `wwwroot`를 가상 HTTPS 호스트로 표시합니다. 외부 이동·팝업·권한·호스트 객체 접근은 막고, 설치 안내 링크만 호스트가 기본 브라우저로 엽니다. 개발 미리보기의 가짜 오디오 브리지는 프로덕션 번들에서 제거됩니다.

설치기 빌드는 `build_installer.ps1`을 사용합니다. Node 의존성 복원, SVG 기반 다중 해상도 아이콘 생성, .NET self-contained publish와 Inno Setup 컴파일을 수행합니다. 설치 시 WebView2가 없을 때만 공식 런타임 부트스트래퍼를 실행하며 가상 케이블 드라이버는 포함하지 않습니다.

설정은 장치 ID로 저장하며 기본값 복구와 초기화가 같은 공통 검증을 사용합니다. 시작·중지·초기화 처리 중에는 관련 설정을 잠그고, 종료 시 진행 중인 명령을 기다린 후 오디오를 정리하고 저장합니다.


## 릴리즈와 업데이트

`.github/workflows/release.yml`은 vX.Y.Z 태그에서 설치기를 빌드하고 GitHub Releases에 게시합니다. `ReleaseVersion`이 태그의 숫자 형식을 정의하고 `GitHubReleaseUpdateService`가 공개 정식 릴리즈와 설치기 링크를 검증합니다. WPF 호스트는 첫 화면 준비 후 백그라운드 확인을 시작하며 `UiBridge`는 업데이트 상태·팝업과 허용된 릴리즈 링크 명령을 React에 연결합니다. 자세한 배포 절차는 [RELEASING.md](RELEASING.md)를 참고하세요.
