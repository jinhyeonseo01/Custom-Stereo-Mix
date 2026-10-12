# README 구성 참고 조사

2026-10-12 기준으로 널리 알려진 Windows 데스크톱 및 오디오 관련 GitHub 프로젝트 4개의 실제 README를 확인했다. 제품 문구나 이미지의 복제가 아닌 정보 구조를 참고했으며, 아래 해석은 관찰 결과를 바탕으로 정리했다.

## 수집한 레포와 구조

| 레포 | 실제 README | 확인한 구성 | 이번 프로젝트에 적용한 요소 |
| --- | --- | --- | --- |
| Microsoft PowerToys | [README](https://github.com/microsoft/PowerToys/blob/main/README.md) | 상단 비주얼·중앙 정렬 소개·주요 링크 → 유틸리티 목록 → 설치 방법 → 변경 사항·계획 → 기여·개인정보 | 한 줄 설명과 빠른 탐색, 기능 뒤 설치 안내, 자세한 구조는 별도 문서 연결 |
| ShareX | [README](https://github.com/ShareX/ShareX/blob/develop/README.md) | 배너·배지·실제 화면 → 제품 소개와 장점 → 기능별 설명 → 다운로드·링크 → 상세 문서 | 실제 앱 화면을 상단에 배치하고 기능을 짧게 설명 |
| Files | [README](https://github.com/files-community/Files/blob/main/.github/README.md) | 상단 비주얼·배지 → 소개 → 설치 → 소스 빌드 → 기여 → 화면 캡처 | 사용자 설치와 개발자 빌드를 구분하고 보조 화면을 제공 |
| OBS Studio | [README](https://github.com/obsproject/obs-studio/blob/master/README.rst) | 상태 배지 → 제품 소개·라이선스 → 문서·빌드·문제 제보 링크 → 기여 안내 | 빌드·문제 해결·제보 방법을 쉽게 찾을 수 있게 배치 |

README 위치는 레포마다 다르다. Files는 루트 README.md가 아닌 .github/README.md, OBS는 README.rst를 사용한다.

## 공통적으로 확인한 점

1. **첫 부분에서 제품의 목적을 설명한다.** 기능 구현 세부 사항보다 사용자가 무엇을 할 수 있는지를 먼저 보여준다.
2. **설치와 도움말의 진입점이 명확하다.** 사용자용 설치 안내와 개발자용 빌드·문서 경로를 구분한다.
3. **기여나 문제 제보를 위한 경로를 제공한다.** 사용 후 문제가 생겼을 때 필요한 정보를 찾을 수 있다.
4. **시각 자료와 배지는 선택적으로 사용한다.** PowerToys·ShareX·Files는 상단 시각 자료를 사용한다. ShareX는 실제 화면도 상단에 두고, OBS는 소개와 주요 링크를 중심으로 구성한다. 모든 레포가 같은 배치를 사용하는 것은 아니다.
5. **긴 세부 문서는 별도로 연결한다.** README는 프로젝트의 입구 역할을 하고, 상세 빌드·기여·사용 안내는 문서나 공식 사이트에 연결한다.

## Custom Stereo Mix에 적용한 구조

```text
아이콘 · 제품명 · 한 줄 설명 · 빠른 링크
 → 실제 메인 화면 캡처
 → 주요 기능
 → 가상 케이블 준비와 빠른 시작
 → 실행 환경과 설치 방법
 → 설정 · 트레이 · 보조 화면
 → 일반 빌드 / 스크립트 / 직접 게시 / 설치기 / Visual Studio
 → React 개발과 테스트
 → 문제 해결 · 설정과 진단
 → 기술 구성 · 기여 · 외부 라이선스
```

프로그램을 바로 사용할 사람에게 필요한 가상 케이블 설정을 앞쪽에 배치했다. 빌드 안내에서는 build_exe.ps1이 편의 스크립트이며 표준 dotnet build/publish와 Visual Studio도 사용할 수 있다는 점을 명시했다.

확인된 GitHub 저장소의 릴리즈 페이지와 태그 기반 Actions 배포 절차를 안내한다. 빌드 상태·다운로드 수·프로젝트 라이선스 배지는 추가하지 않고 실제 명령과 산출물을 설명한다. 프로젝트 자체의 LICENSE 파일은 현재 없으며, 외부 구성요소의 라이선스 고지는 기존 파일에 연결했다.

## 화면 캡처

- 메인: [main-window.png](images/main-window.png)
- 설정: [settings.png](images/settings.png)
- 사용 안내: [usage-guide.png](images/usage-guide.png)

최신 artifacts/webview2 배포의 실제 WPF/WebView2 화면을 별도 호스트로 열고 Windows 장치를 열거한 상태에서 캡처했다. 문서 캡처용 메모리 설정을 사용했고, 기존 사용자 설정은 읽거나 저장하지 않았다. 오디오 연결은 시작하지 않았다. UI 화면 자체를 캡처해 주변 데스크톱이나 다른 앱이 포함되지 않도록 했다.
