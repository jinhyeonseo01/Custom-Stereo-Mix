import type { AppState, Command, Meter, WebViewBridge } from "./types";
let transport: WebViewBridge | undefined;
let connection: Promise<void> | undefined;
let state: AppState | null = null;
let meter: Meter = { levelPercent: 0, signalState: "신호 없음", samples: [] };
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
  if (!transport)
    throw new Error(
      "데스크톱 앱과 연결하지 못했습니다. 앱에서 다시 실행해 주세요.",
    );
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
      else call.reject(new Error(message.error || "요청에 실패했습니다."));
    }
  });
  await request("ready");
}
export function request(command: Command, payload: unknown = {}) {
  return new Promise<void>((resolve, reject) => {
    if (!transport) {
      reject(new Error("데스크톱 연결을 기다려 주세요."));
      return;
    }
    const id = `${Date.now()}-${++sequence}`;
    const timer = setTimeout(() => {
      pending.delete(id);
      reject(
        new Error("응답이 지연되고 있습니다. 현재 연결 상태를 확인해 주세요."),
      );
    }, 15000);
    pending.set(id, { resolve, reject, timer });
    try {
      transport.postMessage({ id, command, payload });
    } catch (error) {
      clearTimeout(timer);
      pending.delete(id);
      reject(
        error instanceof Error ? error : new Error("요청을 보내지 못했습니다."),
      );
    }
  });
}
