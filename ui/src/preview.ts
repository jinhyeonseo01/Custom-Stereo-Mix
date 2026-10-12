import { t, setLanguage, type MessageKey } from "./i18n";
import type { AppState, Settings, WebViewBridge } from "./types";
import { previewGraph, graphError, sameTopology, activeNodeIds, signalNodeIds } from "./advanced/graph";
const defaults: Settings = {
  language: "ko-KR",
  startWithWindows: false,
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
  const cachedGraph = () => {
    try {
      const saved = sessionStorage.getItem("stereo-preview-graph");
      if (saved) { const graph = JSON.parse(saved); if (!graphError(graph)) return graph; }
    } catch { /* Preview storage is optional. */ }
    return previewGraph();
  };
  const initialGraph = cachedGraph();
  let state: AppState = {
    graph: initialGraph, graphRevision: 0,
    advancedMode: new URLSearchParams(location.search).has("advanced"), simpleGraph: false,
    simpleBusId: null,
    revision: 0,
    view: new URLSearchParams(location.search).has("advanced") ? "main" : "help",
    guideDraft: true,
    sources: [
      { id: "speakers", name: "Realtek Speakers", summary: "48 kHz · 2채널", isDefault: false, isVirtualCableCandidate: false },
      { id: "music", name: "USB DAC", summary: "48 kHz · 2채널", isDefault: false, isVirtualCableCandidate: false },
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
      { id: "headphone", name: "SteelSeries Arctis Nova 5", summary: "48 kHz · 2채널", isDefault: true, isVirtualCableCandidate: false },
      { id: "cable-a", name: "CABLE-A Input (VB-Audio)", summary: "48 kHz · 2채널", isDefault: false, isVirtualCableCandidate: true, returnPathGroup: "vb:CABLE-A" },
      {
        id: "cable",
        name: "CABLE Input (VB-Audio Virtual Cable)",
        summary: "48 kHz · 2채널",
        isDefault: false,
        isVirtualCableCandidate: true,
        returnPathGroup: "vb:CABLE",
      },
    ],
    microphones: [
      { id: "cable-out", name: "CABLE Output (VB-Audio Virtual Cable)", summary: "48 kHz · 2채널", isDefault: false, isVirtualCableCandidate: true, returnPathGroup: "vb:CABLE" },
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
    isStartEnabled: activeNodeIds(initialGraph).size > 0,
    isStopEnabled: false,
    canResetSettings: true,
    engineLine: "48 kHz · 스테레오",
    trayHint: "TrayContinues",
    appVersion: "v2.0.1",
    updateMessage: "UpdateUnconfigured",
    isUpdateDialogOpen: false,
  isDownloadingUpdate: false,
  updateDownloaded: false,
  downloadMessage: "",
  downloadProgress: null,
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
    () => {
      const signal = state.isRouting ? signalNodeIds(state.graph) : new Set<string>();
      emit({
        type: "meter",
        meter: {
          nodes: Object.fromEntries(state.graph.nodes.map((n, i) => [n.id, { rms: signal.has(n.id) ? .12 + .08 * Math.abs(Math.sin(Date.now() / 800 + i)) : 0, peak: signal.has(n.id) ? .24 + .3 * Math.abs(Math.sin(Date.now() / 800 + i)) : 0, clipped: false }])),
          errors: {},
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
      });
    },
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
        case "mode": state.advancedMode = Boolean(payload.advanced); break;
        case "simpleBus": state.simpleBusId = String(payload.id); break;
        case "graph": {
          const graph = payload.project as AppState["graph"];
          const error = graphError(graph, state) || (payload.revision !== state.graphRevision ? t("GraphStale") : "") ||
            (state.isRouting && !sameTopology(state.graph, graph) ? t("StopBeforeChange") : "");
          if (error) { setTimeout(() => emit({ type: "response", id, ok: false, error }), 20); return; }
          state.graph = structuredClone(graph); state.graphRevision++; break;
        }
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
          if (!activeNodeIds(state.graph).size) { setTimeout(() => emit({ type: "response", id, ok: false, error: t("GraphIncomplete") }), 20); return; }
          state.isRouting = true;
          state.canEditRoute = false;
          state.isStartEnabled = false;
          state.isStopEnabled = true;
          state.statusText = "GraphRunning";
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
      state.isStartEnabled = !state.isRouting && activeNodeIds(state.graph).size > 0;
      try { sessionStorage.setItem("stereo-preview-graph", JSON.stringify(state.graph)); } catch { }
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
