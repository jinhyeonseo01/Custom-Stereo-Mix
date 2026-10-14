# 프로젝트 구조

## 구성

| 경로 | 역할 |
|---|---|
| `ui/` | React·TypeScript UI, React Flow 그래프, Material Symbols 로컬 폰트 |
| `locales/` | C#·React 공통 메시지 키와 번역 |
| `src/CustomStereoMix.App/` | WPF 창, WebView2 호스트, 트레이, 자동 실행·업데이트 설치 |
| `src/CustomStereoMix.State/` | 공통 프로젝트 상태, UI 명령, 저장 예약과 JSON 브리지 |
| `src/CustomStereoMix.Core/` | 그래프·장치·설정 모델, 검증·이관, 서비스 인터페이스 |
| `src/CustomStereoMix.Audio/` | WASAPI 장치·세션, 공유 캡처, 믹싱, 리샘플링·DSP·저장소 |
| `tests/CustomStereoMix.Tests/` | 자동 회귀 검사와 선택 실행하는 실제 장치 검사 |
| `ui/tests/` | 실행 중 파라미터 이력과 구조 변경 분리, 편집 묶음의 회귀 검사 |
| `installer/`, `tools/` | Inno Setup 설치기, 릴리즈 게시, 앱 아이콘 생성 도구 |
| `docs/` | 구조·번역·배포·검증 문서와 README용 현재 화면 |
| `licenses/` | 배포에 포함하는 외부 라이선스 |

Core와 State는 WPF·NAudio에 의존하지 않는다. App은 상태와 인터페이스를 조작하고, 장치 수명은 Audio의 컨트롤러·세션이 관리한다. 기존 1.x `RouteConfig`·세션은 호환 경로로 남아 있고 데스크톱의 2.0 시작 명령은 그래프 컨트롤러를 사용한다.

## 공통 프로젝트

`GraphProject` schemaVersion 2는 노드·간선과 위치, 이름, 장치 ID, 게인·뮤트·보정 설정을 저장한다. 노드는 `loopback`, `capture`, `bus`, `output`의 네 종류다. 연결은 소스 → 버스 → 출력으로 제한한다. 현재 한 프로젝트의 검증 한도는 노드 128개·간선 512개다.

`GraphValidator`는 값 범위, ID, 연결 역할, 중복 출력과 순환 경로를 검사한다. 동일 재생 endpoint의 loopback 반환과 가상 케이블의 반환 그룹을 그래프에 추가해 전체 믹스의 순환을 확인한다. 가상 케이블 그룹은 알려진 이름·드라이버 종류를 사용하는 휴리스틱이다. 외부 앱의 연결이나 모든 드라이버 내부 경로를 알아내지는 못한다.

`GraphMigration`은 기존 재생음·선택 마이크·출력을 기본 그래프로 바꾸고 볼륨·컴프레서·리미터와 꺼진 마이크 선호 설정을 보존한다. 최초 저장 시 `settings.json.v1.bak`를 남기며 이후 저장으로 덮어쓰지 않는다.

간편·고급모드는 같은 그래프를 사용한다. 간편모드는 선택 버스의 기본 연결과 마이크를 편집하면서 나머지 연결을 보존한다. 모드 전환은 엔진을 중지하거나 그래프를 축약하지 않는다. 구조/장치 변경은 중지 후, 동일 구조의 파라미터·레이아웃 변경은 실행 중 적용한다.

## 오디오 처리

```text
N개의 WASAPI loopback / capture endpoint
  → endpoint별 공유 캡처·float 변환·지속 리샘플링·입력 버퍼
  → GraphMixer: 소스/간선 게인 → N개 버스 합산·보정
  → 출력별 합산·최종 제한
  → 독립 출력 버퍼 → N개의 WASAPI 출력
```

- `NaudioDeviceService`는 활성 Render·Capture endpoint와 기본 장치·mix format을 읽는다. 같은 표시 이름이어도 endpoint ID가 다르면 별개다.
- `GraphAudioSession`은 동일 kind+endpoint 캡처를 공유한다. 한 출력의 WASAPI 요청이 블록 처리를 구동하고 다른 출력은 각자 큐를 소비한다. UI 타이머로 오디오를 구동하지 않는다.
- `GraphMixer`는 블록을 한 번 계산해 팬아웃하며 출력에 도달하지 않는 노드는 실행하지 않는다. 처리용 배열·버퍼를 미리 만들고 파라미터 계획을 원자적으로 교체한다. 게인 변화는 10ms 프레임 램프로 적용한다.
- `AdaptiveAudioBuffer`는 프레임 정렬, 큐 용량 제한, 초기 채움과 제한된 시계 차이 보정을 제공한다. 소비자가 버퍼를 공유해 다른 출력의 샘플을 빼앗지 않는다.
- 믹서 처리 후 출력별 최종 리미터를 적용한다. 시계 보정 보간 뒤에도 활성 출력 리미터로 제한한다.
- 출력 초기화/실행 오류는 해당 출력에 표시하고 나머지는 유지할 수 있다. 주 출력이 실패하면 정상 출력으로 넘기며 모든 출력이 실패하면 전체 세션을 종료한다.
- 미터와 진단은 별도 타이머에서 읽는다. PCM은 JSON 브리지를 통과하지 않는다.

## UI와 상태 흐름

```text
React 명령 → WebView2 JSON → UiBridge → MainWindowViewModel → 서비스
서비스 상태/미터 → 상태 스냅샷·미터 메시지 → React
```

`UiBridge`는 명령을 직렬화하고 전체 수정값을 검증한다. 상태 revision과 graphRevision으로 오래된 응답·수정을 거부한다. 그래프 편집은 UI에서 짧게 묶어 순차 전송하고 거부되면 서버 상태로 복구한다. 저장되지 않은 수정이 처리되는 동안 시작/모드 전환을 잠그고 실행 중 중지는 가능하게 유지한다.

`ui/src/advanced/GraphWorkspace.tsx`는 노드·간선·장치 목록·선택 믹서·JSON 입출력을 담당한다. `NodeContextMenu.tsx`는 우클릭·…·Shift+F10의 공통 메뉴와 키보드 탐색을, `NodeInspector.tsx`는 노드/간선의 공통 우측 상세 패널을 담당한다. 출력 전용 팝업을 없애고 활성화 → 소리 → 기본 정보 → 보정 → 연결 순으로 정리했다. 전역 설정은 앱·엔진 옵션을 제공하고 간편모드에만 선택 믹스 보정을 표시한다.

노드 `enabled`는 처리 경로 참여 여부로 중지 중에만 변경한다. `muted`는 경로를 유지하는 실시간 무음 옵션이다. 회색·점선·문구로 비활성화를, 주황색·배지로 음소거를 표시하며 두 상태를 독립적으로 저장한다. `graph.ts`의 활성 경로 계산은 Core의 `GraphValidator.ActiveNodeIds`와 같은 규칙으로 경로·미터 표시를 결정한다.

`AudioControls.tsx`의 미터·게인·숫자 입력은 간편모드와 공유한다. 음수·양수 게인 범위를 슬라이더의 양쪽 절반에 나눠 0 dB를 중앙에 둔다. 숫자 편집은 Enter/포커스 이동으로 적용하고 Escape로 취소한다. 고급 그래프 모듈은 필요할 때 로드한다.

2.0.2의 `fields.tsx`는 숫자의 범위·단위·오류와 Enter/Escape 편집을 통일한다. `history.ts`는 변경된 필드만 되돌려 실행 중 파라미터 이력을 적용하고 구조 이력은 건너뛴다. 슬라이더 포인터 동작과 이름 편집을 한 단계로 묶는다. `NodeConnections.tsx`는 기존 노드와 새 장치를 동일한 입력/출력 설정 흐름으로 연결한다. `RouteDiagnostics.tsx`는 음소거·비활성·오프라인·실행 오류와 해결 동작을 간편모드와 상세 패널에서 공유한다.

확대 비율을 유지하면서 가려진 선택 노드만 이동하고, 축소 노드는 고정 크기 메뉴와 요약 정보를 표시한다. 작은 창은 패널/도크를 번갈아 열며 도크는 선택을 따라가거나 명시적으로 고정한다. 도크 높이는 포인터와 키보드로 조절하고 내부 높이에 따라 페이더를 줄인다. 미터는 -60..0 dBFS, 피크 유지와 클리핑 피드백을 사용한다.

`SettingsPage.tsx`는 오디오·프로그램·정보/업데이트 탭으로 엔진과 프로그램 옵션을 나눈다. `CloseToTray`와 `MinimizeToTray`는 독립적으로 저장하며, 이전 설정에 `CloseToTray`가 없으면 기존 `MinimizeToTray` 값을 이어받는다. 닫기와 최소화의 호스트 이벤트도 각각 해당 옵션을 참조한다.

브라우저 개발용 `preview.ts`는 샘플 장치와 시뮬레이션 미터를 사용한다. 실제 오디오와 같은 UI 흐름을 검증하기 위한 것이며 프로덕션 번들에서는 제외한다. README 이미지는 이 미리보기의 직접 캡처다.

WebView2는 배포 폴더의 `wwwroot`를 가상 HTTPS 호스트로 표시한다. 외부 이동·팝업·권한·호스트 객체 접근을 제한하고 허용된 설치·릴리즈 링크는 호스트가 기본 브라우저로 연다. 화면 새로고침·트레이 숨김은 네이티브 세션을 종료하지 않는다.

## 저장·언어·배포

설정은 장치 ID, 그래프, 선택 믹스, UI 모드와 모드별 창 크기를 포함한다. 정상 백업·손상 복구·원자적 저장을 사용한다. 종료는 진행 중인 시작·초기화 명령을 기다린 후 세션을 정리하고 저장한다.

언어는 `AppSettings.Language`에 저장한다. C#의 `MessageKey`와 React 타입은 같은 JSON 리소스를 사용한다. 설치기는 `initial-language.json`만 만들며 사용자 설정을 덮어쓰지 않는다. 언어 변경은 실행을 유지한다. 자세한 규칙은 [LOCALIZATION.md](LOCALIZATION.md)에 있다.

`build_installer.ps1`은 React 빌드·self-contained publish·Inno Setup 컴파일을 수행한다. 가상 드라이버는 포함하지 않는다. 태그 워크플로는 검사 후 설치기·SHA-256 파일을 GitHub Releases에 게시한다. 앱은 공개 정식 릴리즈를 확인하고 설치기를 검증한 뒤 사용자 확인에 따라 종료·설치한다. [RELEASING.md](RELEASING.md)를 참고한다.

실제 장치 검사와 남은 안정화 범위는 [VALIDATION.md](VALIDATION.md)에 기록한다.
