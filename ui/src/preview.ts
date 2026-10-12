import { t, setLanguage, type MessageKey } from "./i18n";
import type { AppState, Settings, WebViewBridge } from "./types";
const defaults: Settings = {
  language: "ko-KR",
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
          : "Headphones (SteelSeries Arctis Nova 5)",
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
        name: "Microphone (USB Microphone)",
        summary: "48 kHz · 1채널",
        isDefault: true,
        isVirtualCableCandidate: false,
      },
    ],
    sourceDeviceId: "headphone",
    targetDeviceId: "cable",
    microphoneDeviceId: "mic",
    statusText: "Ready",
    targetMessage: "CableInputHint",
    noticeText: "",
    isRouting: false,
    canEditRoute: true,
    canEditSettings: true,
    isStartEnabled: true,
    isStopEnabled: false,
    canResetSettings: true,
    engineLine: "48 kHz · 스테레오",
    trayHint: "TrayContinues",
    appVersion: "v1.2.2",
    updateMessage: "UpdateUnconfigured",
    isUpdateDialogOpen: false,
    latestVersion: null,
    hasUpdateInstaller: false,
    hasReleasePage: false,
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
          signalState: t(state.isRouting ? "SignalDetected" : "NoSignal"),
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
          state.statusText = "Routing";
          break;
        case "stop":
          state.isRouting = false;
          state.canEditRoute = true;
          state.isStartEnabled = true;
          state.isStopEnabled = false;
          state.statusText = "Ready";
          break;
        case "patch": {
          Object.assign(state.settings, payload);
          for (const key of [
            "sourceDeviceId",
            "targetDeviceId",
            "microphoneDeviceId",
          ] as const)
            if (key in payload) state[key] = String(payload[key]);
          state.engineLine = `${state.settings.sampleRate / 1000} kHz · ${state.settings.channels === 1 ? "Mono" : "Stereo"}`;
          state.trayHint = state.settings.minimizeToTray
            ? "TrayContinues"
            : "CloseExits";
          break;
        }
        case "reset":
          state.settings = { ...defaults };
          state.isRouting = false;
          state.canEditRoute = state.isStartEnabled = true;
          state.isStopEnabled = false;
          state.statusText = "Ready";
          state.settingsMessage = "SettingsResetDone";
          break;
        case "checkUpdates":
          state.updateMessage = "PreviewBanner";
          break;
        case "dismissUpdate":
          state.isUpdateDialogOpen = false;
          break;
        case "openRelease":
        case "downloadUpdate":
          state.noticeText = "PreviewBanner";
          break;
        case "openCableWebsite":
          state.noticeText = "PreviewBanner";
          break;
      }
      state.revision++;
      setLanguage(state.settings.language);
      const snapshot = structuredClone(state);
      snapshot.statusText = t(state.statusText as MessageKey);
      snapshot.targetMessage = t("CableInputHint");
      snapshot.noticeText = state.noticeText
        ? t(state.noticeText as MessageKey)
        : "";
      snapshot.updateMessage = t(state.updateMessage as MessageKey);
      snapshot.settingsMessage = state.settingsMessage
        ? t(state.settingsMessage as MessageKey)
        : "";
      snapshot.engineLine = t(
        "EngineSummary",
        state.settings.sampleRate / 1000,
        t(state.settings.channels === 1 ? "Mono" : "Stereo"),
      );
      snapshot.trayHint = t(
        state.settings.minimizeToTray ? "TrayContinues" : "CloseExits",
      );
      for (const devices of [
        snapshot.sources,
        snapshot.targets,
        snapshot.microphones,
      ])
        for (const device of devices)
          device.summary = t(
            "DeviceSummary",
            48,
            device.id === "mic" ? 1 : 2,
            device.isDefault ? t("DefaultDevice") : "",
          );
      setTimeout(
        () => emit({ type: "response", id, ok: true, state: snapshot }),
        30,
      );
    },
  };
}
