export interface Device {
  id: string;
  name: string;
  summary: string;
  isDefault: boolean;
  isVirtualCableCandidate: boolean;
}
export interface Settings {
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
  latestVersion: string | null;
  hasUpdateInstaller: boolean;
  hasReleasePage: boolean;
  settingsMessage: string;
  busy: boolean;
  updating: boolean;
  settings: Settings;
}
export interface Meter {
  levelPercent: number;
  signalState: string;
  samples: number[];
}
export type Command =
  | "ready"
  | "start"
  | "stop"
  | "refresh"
  | "reset"
  | "checkUpdates"
  | "dismissUpdate"
  | "openRelease"
  | "downloadUpdate"
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
  postMessage(message: unknown): void;
  addEventListener(type: "message", handler: (e: MessageEvent) => void): void;
}
declare global {
  interface Window {
    chrome?: { webview?: WebViewBridge };
    __stereoPreview?: boolean;
  }
}
