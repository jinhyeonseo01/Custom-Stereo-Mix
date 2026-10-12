import { t, setLanguage } from "./i18n";
import type { AppState, Command, Meter, WebViewBridge } from "./types";
let transport: WebViewBridge | undefined;
let connection: Promise<void> | undefined;
let state: AppState | null = null;
let meter: Meter = { levelPercent: 0, signalState: t("NoSignal"), samples: [] };
let sequence = 0;
const stateListeners = new Set<() => void>();
const meterListeners = new Set<() => void>();
const pending = new Map<
  string,
  {
    resolve: () => void;
    reject: (error: Error) => void;
    timer: ReturnType<typeof setTimeout>;
  }
>();
export const subscribeState = (fn: () => void) => {
  stateListeners.add(fn);
  return () => {
    stateListeners.delete(fn);
  };
};
export const subscribeMeter = (fn: () => void) => {
  meterListeners.add(fn);
  return () => {
    meterListeners.delete(fn);
  };
};
export const readState = () => state;
export const readMeter = () => meter;
function updateState(value: AppState) {
  if (state && value.revision <= state.revision) return;
  state = value;
  setLanguage(value.settings.language);
  stateListeners.forEach((fn) => fn());
}
export function connect() {
  return (connection ??= connectTransport());
}
async function connectTransport() {
  transport = window.chrome?.webview;
  if (import.meta.env.DEV && !transport) {
    const { createPreviewBridge } = await import("./preview");
    transport = createPreviewBridge();
    window.__stereoPreview = true;
  }
  if (!transport) throw new Error(t("DesktopConnectionFailed"));
  transport.addEventListener("message", (event) => {
    const message = event.data;
    if (message.type === "state") updateState(message.state);
    if (message.type === "meter") {
      meter = message.meter;
      meterListeners.forEach((fn) => fn());
    }
    if (message.type === "response") {
      if (message.state) updateState(message.state);
      const call = pending.get(message.id);
      if (!call) return;
      clearTimeout(call.timer);
      pending.delete(message.id);
      if (message.ok) call.resolve();
      else call.reject(new Error(message.error || t("RequestFailed")));
    }
  });
  await request("ready");
}
export function request(command: Command, payload: unknown = {}) {
  return new Promise<void>((resolve, reject) => {
    if (!transport) {
      reject(new Error(t("WaitDesktop")));
      return;
    }
    const id = `${Date.now()}-${++sequence}`;
    const timer = setTimeout(() => {
      pending.delete(id);
      reject(new Error(t("ResponseDelayed")));
    }, 15000);
    pending.set(id, { resolve, reject, timer });
    try {
      transport.postMessage({ id, command, payload });
    } catch (error) {
      clearTimeout(timer);
      pending.delete(id);
      reject(error instanceof Error ? error : new Error(t("SendFailed")));
    }
  });
}
