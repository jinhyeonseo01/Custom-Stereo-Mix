import type { AppState, Settings, WebViewBridge } from "./types";
const defaults: Settings = {
  gainDb: 0,
  compressorEnabled: false,
  compressorThresholdDb: -12,
  compressorRatio: 4,
  isMicMixEnabled: false,
  micGainDb: 0,
  sampleRate: 48000,
  channels: 2,
  latencyMilliseconds: 50,
  bufferMilliseconds: 250,
  minimizeToTray: true,
};
export function createPreviewBridge(): WebViewBridge {
  const listeners = new Set<(e: MessageEvent) => void>();
  const stress = new URLSearchParams(location.search).has("stress");
  let state: AppState = {
    revision: 0,
    view: "help",
    guideDraft: true,
    sources: [
      {
        id: "headphone",
        name: stress
          ? "아주 긴 이름의 헤드폰 (SteelSeries Arctis Nova 5 Wireless 고해상도 오디오 장치)"
          : "헤드폰 (SteelSeries Arctis Nova 5)",
        summary: "48 kHz · 2채널 · 기본",
        isDefault: true,
        isVirtualCableCandidate: false,
      },
    ],
    targets: [
      {
        id: "cable",
        name: "CABLE Input (VB-Audio Virtual Cable)",
        summary: "48 kHz · 2채널",
        isDefault: false,
        isVirtualCableCandidate: true,
      },
    ],
    microphones: [
      {
        id: "mic",
        name: "마이크 (USB Microphone)",
        summary: "48 kHz · 1채널",
        isDefault: true,
        isVirtualCableCandidate: false,
      },
    ],
    sourceDeviceId: "headphone",
    targetDeviceId: "cable",
    microphoneDeviceId: "mic",
    statusText: "대기 중",
    targetMessage: "녹음 앱에서는 이 케이블의 녹음 장치를 선택하세요.",
    noticeText: "",
    isRouting: false,
    canEditRoute: true,
    canEditSettings: true,
    isStartEnabled: true,
    isStopEnabled: false,
    canResetSettings: true,
    engineLine: "48 kHz · 스테레오",
    trayHint: "닫기 · 최소화하면 트레이에서 계속 실행됩니다",
    appVersion: "v1.1.0",
    updateMessage: "업데이트 배포 준비 중입니다.",
    settingsMessage: "",
    busy: false,
    updating: false,
    settings: { ...defaults },
  };
  const emit = (data: unknown) =>
    listeners.forEach((fn) => fn(new MessageEvent("message", { data })));
  setInterval(
    () =>
      emit({
        type: "meter",
        meter: {
          levelPercent: state.isRouting ? 18 : 0,
          signalState: state.isRouting ? "신호 감지됨" : "신호 없음",
          samples: state.isRouting
            ? Array.from(
                { length: 96 },
                (_, i) =>
                  0.06 + Math.abs(Math.sin(i * 0.19 + Date.now() / 500)) * 0.23,
              )
            : [],
        },
      }),
    100,
  );
  return {
    addEventListener: (_, fn) => {
      listeners.add(fn);
    },
    postMessage(message) {
      const { id, command, payload } = message as {
        id: string;
        command: string;
        payload: Record<string, unknown>;
      };
      switch (command) {
        case "openHelp":
          state.view = "help";
          break;
        case "openSettings":
          state.view = "settings";
          break;
        case "closeView":
          state.view = "main";
          break;
        case "guideDraft":
          state.guideDraft = Boolean(payload.dontShowAgain);
          break;
        case "start":
          state.isRouting = true;
          state.canEditRoute = false;
          state.isStartEnabled = false;
          state.isStopEnabled = true;
          state.statusText = "연결 중";
          break;
        case "stop":
          state.isRouting = false;
          state.canEditRoute = true;
          state.isStartEnabled = true;
          state.isStopEnabled = false;
          state.statusText = "대기 중";
          break;
        case "patch": {
          Object.assign(state.settings, payload);
          for (const key of [
            "sourceDeviceId",
            "targetDeviceId",
            "microphoneDeviceId",
          ] as const)
            if (key in payload) state[key] = String(payload[key]);
          state.engineLine = `${state.settings.sampleRate / 1000} kHz · ${state.settings.channels === 1 ? "모노" : "스테레오"}`;
          state.trayHint = state.settings.minimizeToTray
            ? "닫기 · 최소화하면 트레이에서 계속 실행됩니다"
            : "닫으면 프로그램이 종료됩니다";
          break;
        }
        case "reset":
          state.settings = { ...defaults };
          state.isRouting = false;
          state.canEditRoute = state.isStartEnabled = true;
          state.isStopEnabled = false;
          state.statusText = "대기 중";
          state.settingsMessage =
            "설정을 기본값으로 초기화했습니다. 소리 연결은 중지되었습니다.";
          break;
        case "checkUpdates":
          state.updateMessage =
            "업데이트 배포 준비 중입니다. 배포가 시작되면 여기에서 확인할 수 있습니다.";
          break;
        case "openCableWebsite":
          state.noticeText = "미리보기에서는 외부 페이지를 열지 않습니다.";
          break;
      }
      state.revision++;
      const snapshot = structuredClone(state);
      setTimeout(
        () => emit({ type: "response", id, ok: true, state: snapshot }),
        30,
      );
    },
  };
}
