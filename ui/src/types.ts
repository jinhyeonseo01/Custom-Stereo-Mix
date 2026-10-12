export interface Device {
  id: string;
  name: string;
  summary: string;
  isDefault: boolean;
  isVirtualCableCandidate: boolean;
  returnPathGroup?: string | null;
}
export interface Settings {
  language: string;
  startWithWindows: boolean;
  gainDb: number;
  compressorEnabled: boolean;
  compressorThresholdDb: number;
  compressorRatio: number;
  isMicMixEnabled: boolean;
  micGainDb: number;
  sampleRate: number;
  channels: number;
  latencyMilliseconds: number;
  bufferMilliseconds: number;
  minimizeToTray: boolean;
}
export interface AppState {
  graph: GraphProject;
  graphRevision: number;
  advancedMode: boolean;
  simpleGraph: boolean;
  simpleBusId: string | null;
  revision: number;
  view: "main" | "settings" | "help";
  guideDraft: boolean;
  sources: Device[];
  targets: Device[];
  microphones: Device[];
  sourceDeviceId: string | null;
  targetDeviceId: string | null;
  microphoneDeviceId: string | null;
  statusText: string;
  targetMessage: string;
  noticeText: string;
  isRouting: boolean;
  canEditRoute: boolean;
  canEditSettings: boolean;
  isStartEnabled: boolean;
  isStopEnabled: boolean;
  canResetSettings: boolean;
  engineLine: string;
  trayHint: string;
  appVersion: string;
  updateMessage: string;
  isUpdateDialogOpen: boolean;
  isDownloadingUpdate: boolean;
  updateDownloaded: boolean;
  downloadMessage: string;
  downloadProgress: { received: number; total: number | null; verifying: boolean } | null;
  latestVersion: string | null;
  hasUpdateInstaller: boolean;
  hasReleasePage: boolean;
  settingsMessage: string;
  busy: boolean;
  updating: boolean;
  settings: Settings;
}
export interface Meter {
  nodes?: Record<string, { rms: number; peak: number; clipped: boolean }>;
  errors?: Record<string, string>;
  levelPercent: number;
  signalState: string;
  samples: number[];
}
export type Command =
  | "mode"
  | "graph"
  | "simpleBus"
  | "ready"
  | "start"
  | "stop"
  | "refresh"
  | "reset"
  | "checkUpdates"
  | "dismissUpdate"
  | "openRelease"
  | "downloadUpdate"
  | "cancelUpdateDownload"
  | "patch"
  | "openHelp"
  | "openSettings"
  | "closeView"
  | "guideDraft"
  | "openCableWebsite";
export type Patch = Partial<
  Settings & {
    sourceDeviceId: string;
    targetDeviceId: string;
    microphoneDeviceId: string;
  }
>;
export interface WebViewBridge {
  // Audio never crosses this command/state bridge.
  postMessage(message: unknown): void;
  addEventListener(type: "message", handler: (e: MessageEvent) => void): void;
}
declare global {
  interface Window {
    chrome?: { webview?: WebViewBridge };
    __stereoPreview?: boolean;
    __initialLanguage?: string;
  }
}
export type AudioNodeKind = "loopback" | "capture" | "bus" | "output";
export interface AudioNodeConfig {
  id: string; kind: AudioNodeKind; label: string; deviceId: string | null;
  preferredMicrophoneId?: string | null;
  preferredMicrophoneGainDb?: number;
  position: { x: number; y: number };
  gainDb: number; muted: boolean; enabled: boolean; limiterEnabled: boolean;
  compressorEnabled: boolean; compressorThresholdDb: number; compressorRatio: number;
}
export interface AudioEdgeConfig {
  id: string; source: string; target: string; gainDb: number; muted: boolean;
}
export interface GraphProject {
  schemaVersion: number; name: string; nodes: AudioNodeConfig[]; edges: AudioEdgeConfig[];
}
