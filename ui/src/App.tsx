import { t, languages } from "./i18n";
import React, {
  useEffect,
  useId,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";

import {
  connect,
  request,
  subscribeState,
  readState,
} from "./bridge";
import { Icon } from "./icons";
import type { AppState, Command, Device, Patch, Settings } from "./types";
import { Gain, GainValue, NodeMeter } from "./advanced/AudioControls";
import "./advanced/advanced.css";
import { nodeName, createNode, graphError } from "./advanced/graph";

const db = (n: number) => `${n > 0 ? "+" : ""}${n.toFixed(1)} dB`;
const GraphWorkspace = React.lazy(() => import("./advanced/GraphWorkspace").then(module => ({ default: module.GraphWorkspace })));
export default function App() {
  const state = useSyncExternalStore(subscribeState, readState);
  const [error, setError] = useState("");
  const [connecting, setConnecting] = useState(true);
  const [graphPending, setGraphPending] = useState(false);
  useEffect(() => {
    connect()
      .catch((e) => setError(e.message))
      .finally(() => setConnecting(false));
  }, []);
  async function act(command: Command, payload?: unknown) {
    setError("");
    try {
      await request(command, payload);
      return true;
    } catch (e) {
      setError(e instanceof Error ? e.message : t("RequestFailed"));
      return false;
    }
  }
  const patch = (value: Patch) => act("patch", value);
  if (!state)
    return (
      <main className="loading">
        <div className="brand-mark">
          <Icon name="logo" size={26} />
        </div>
        <h1>Custom Stereo Mix</h1>
        <p role="status">
          {error ||
            (connecting ? t("ConnectingInterface") : t("WaitingConnection"))}
        </p>
      </main>
    );
  return (
    <div className={`app-shell ${state.advancedMode ? "advanced" : ""}`}>
      <header className="app-header">
        <div className="brand">
          <div className="brand-mark">
            <Icon name="logo" size={25} />
          </div>
          <div className="brand-copy">
            <h1>Custom Stereo Mix</h1>
            <p>{t("AppTagline")}</p>
          </div>
        </div>
        <label className="mode-switch"><input type="checkbox" role="switch" checked={state.advancedMode}
          disabled={state.busy || graphPending} onChange={e => act("mode", { advanced: e.target.checked })} />
          <span className="switch-visual" aria-hidden="true" /><span>{t("AdvancedMode")}</span></label>
        <nav className="header-actions" aria-label={t("AppMenu")}>
          <span
            className={`status-chip ${state.isRouting ? "running" : ""}`}
            title={state.statusText}
          >
            <span />
            {state.statusText}
          </span>
          <button
            className={`button subtle ${state.view === "help" ? "selected" : ""}`}
            onClick={() =>
              act(state.view === "help" ? "closeView" : "openHelp")
            }
            title={t("GuideExtraSetup")}
            aria-haspopup="dialog" aria-expanded={state.view === "help"}
          >
            <Icon name="help" />
            <span>{t("Guide")}</span>
          </button>
          <button
            className={`button subtle ${state.view === "settings" ? "selected" : ""}`}
            onClick={() =>
              act(state.view === "settings" ? "closeView" : "openSettings")
            }
            aria-haspopup="dialog" aria-expanded={state.view === "settings"}
          >
            <Icon name="settings" />
            <span>{t("Settings")}</span>
          </button>
        </nav>
      </header>
      {window.__stereoPreview && (
        <div className="preview-note">{t("PreviewBanner")}</div>
      )}
      {state.advancedMode && (error || state.noticeText) && <div className="notice graph-error" role="alert"><span>{error || state.noticeText}</span>
        {error && <button className="icon-button" aria-label={t("DismissError")} onClick={() => setError("")}><Icon name="close" /></button>}</div>}
      {state.advancedMode ? <React.Suspense fallback={<main className="loading"><p>{t("Working")}</p></main>}><GraphWorkspace state={state} onPendingChange={setGraphPending} /></React.Suspense> : <main className="page-scroll">
        <div className="page-content">
          {(error || state.noticeText) && (
            <div className="notice" role="alert">
              <span>{error || state.noticeText}</span>
              {error && (
                <button
                  aria-label={t("DismissError")}
                  className="icon-button"
                  onClick={() => setError("")}
                >
                  <Icon name="close" size={16} />
                </button>
              )}
            </div>
          )}
          <SimpleGraphPage state={state} act={act} onPendingChange={setGraphPending} />
        </div>
      </main>}
      <footer className="app-footer">
        <div className="footer-copy"><strong>{state.engineLine}{state.advancedMode && " · " + t("GraphConfiguredLatency") + " " + state.settings.latencyMilliseconds + " ms"}</strong><span>{state.trayHint}</span></div>
        <button className={`button route-action ${state.isRouting ? "danger" : "primary"}`}
          disabled={state.busy || (!state.isRouting && graphPending) || (state.isRouting ? !state.isStopEnabled : !state.isStartEnabled)}
          onClick={() => act(state.isRouting ? "stop" : "start")}>
          <Icon name={state.isRouting ? "stop" : "play"} size={17} />
          {t(state.busy ? "Working" : state.advancedMode ? state.isRouting ? "GraphStopAll" : "GraphStartAll" : state.isRouting ? "StopRouting" : "StartRouting")}
        </button>
      </footer>
      <PanelDialog state={state} act={act} patch={patch} error={error} />
      <UpdateDialog state={state} act={act} />
    </div>
  );
}

type Actions = {
  state: AppState;
  act: (c: Command, p?: unknown) => Promise<boolean>;
  patch: (p: Patch) => Promise<boolean>;
};
function SimpleGraphPage({ state, act, onPendingChange }: Pick<Actions, "state" | "act"> & { onPendingChange: (pending: boolean) => void }) {
  const bus = state.graph.nodes.find(n => n.id === state.simpleBusId && n.kind === "bus") ?? state.graph.nodes.find(n => n.kind === "bus");
  const draft = useRef(state.graph), queued = useRef<typeof state.graph | null>(null), sending = useRef(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [local, setLocal] = useState(state.graph);
  const [localError, setLocalError] = useState("");
  useEffect(() => { if (!sending.current && !queued.current) { draft.current = state.graph; setLocal(state.graph); } }, [state.graphRevision, state.graph]);
  useEffect(() => () => { if (timer.current) { clearTimeout(timer.current); void flush(); } }, []);
  async function flush() {
    timer.current = null; if (sending.current) return; sending.current = true;
    try { while (queued.current) { const project = queued.current; queued.current = null;
      if (!await act("graph", { revision: readState()!.graphRevision, project })) { queued.current = null; draft.current = readState()!.graph; setLocal(draft.current); break; }
    } } finally { sending.current = false; onPendingChange(false); }
  }
  const update = (project: typeof state.graph) => {
    const error = graphError(project, state);
    if (error) { setLocalError(error); return; }
    setLocalError(""); draft.current = project; setLocal(project); queued.current = project; onPendingChange(true);
    if (timer.current) clearTimeout(timer.current); timer.current = setTimeout(() => void flush(), 75); };
  const current = local.nodes.find(n => n.id === bus?.id);
  if (!current) return <div className="simple-graph-summary"><p>{t("GraphEmpty")}</p><button className="button" onClick={() => {
    update({ ...draft.current, nodes: [...draft.current.nodes, createNode("bus", null, t("NewMix"))] });
  }}>{t("NewMix")}</button></div>;
  const inputs = local.edges.filter(e => e.target === current.id);
  const outputs = local.edges.filter(e => e.source === current.id);
  const sourceEdge = inputs.find(e => local.nodes.find(n => n.id === e.source)?.kind === "loopback");
  const micEdge = inputs.find(e => local.nodes.find(n => n.id === e.source)?.kind === "capture");
  const source = local.nodes.find(n => n.id === sourceEdge?.source);
  const target = local.nodes.find(n => n.id === outputs[0]?.target);
  const mic = local.nodes.find(n => n.id === (micEdge?.source ?? current.preferredMicrophoneId)) ?? local.nodes.find(n => n.kind === "capture");
  const change = (patch: Partial<typeof current>) => update({ ...draft.current, nodes: draft.current.nodes.map(n => n.id === current.id ? { ...n, ...patch } : n) });
  const choose = (kind: "loopback" | "capture" | "output", deviceId: string, enabled = true) => {
    if (!state.canEditRoute) return;
    const graph = draft.current;
    let node = graph.nodes.find(n => n.kind === kind && n.deviceId === deviceId);
    const nodes = [...graph.nodes];
    if (!node) { node = createNode(kind, deviceId, kind === "capture" ? state.microphones.find(d => d.id === deviceId)?.name ?? t("InputAudio") : deviceId === "headphone" ? t("GraphHeadphones") : "", nodes.filter(n => kind === "output" ? n.kind === kind : n.kind === "capture" || n.kind === "loopback").length); nodes.push(node); }
    const replaced = kind === "output" ? outputs[0] : kind === "capture" ? micEdge : sourceEdge;
    const from = kind === "output" ? current.id : node.id, to = kind === "output" ? node.id : current.id;
    const existing = graph.edges.find(e => e.source === from && e.target === to);
    const edges = graph.edges.filter(e => e.id !== replaced?.id && e.id !== existing?.id);
    if (enabled) {
      const replacement = existing ?? { id: crypto.randomUUID(), source: from, target: to, gainDb: replaced?.gainDb ?? (kind === "capture" ? current.preferredMicrophoneGainDb ?? 0 : 0), muted: false };
      const index = replaced ? graph.edges.findIndex(e => e.id === replaced.id) : edges.length;
      edges.splice(Math.min(Math.max(0, index), edges.length), 0, replacement);
    }
    update({ ...graph, nodes: nodes.map(n => n.id === current.id && kind === "capture" ? { ...n, preferredMicrophoneId: node!.id } : n), edges });
  };
  const micEnabled = !!micEdge;
  return <div className="simple-graph-summary">
    {localError && <div className="notice" role="alert"><span>{localError}</span><button className="icon-button" aria-label={t("DismissError")} onClick={() => setLocalError("")}><Icon name="close" /></button></div>}
    <div className="section-heading"><h2>{t("AudioRouting")}</h2>
      {local.nodes.filter(n => n.kind === "bus").length > 1 && <select className="simple-mix-picker" aria-label={t("MixBus")} value={current.id}
        onChange={e => act("simpleBus", { id: e.target.value })}>{local.nodes.filter(n => n.kind === "bus").map(n => <option key={n.id} value={n.id}>{nodeName(n)}</option>)}</select>}
      <button className="button small" disabled={!state.canEditRoute} onClick={() => act("refresh")}><Icon name="refresh" size={14} />{t("Refresh")}</button></div>
    <div className="route-grid"><section className="card"><DeviceSelect label={t("PlaybackSource")} icon="speaker" devices={state.sources}
      selected={source?.deviceId ?? null} disabled={!state.canEditRoute} onChange={id => choose("loopback", id)} /></section>
      <span className="route-arrow" aria-hidden="true">→</span><section className="card"><DeviceSelect label={t("OutputTarget")} icon="cable" devices={state.targets}
      selected={target?.deviceId ?? null} disabled={!state.canEditRoute} onChange={id => choose("output", id)} /></section></div>
    <p className="caption route-help">{t("CableInputHint")}</p>
    {(inputs.length > Number(!!sourceEdge) + Number(!!micEdge) || outputs.length > 1) && <p className="caption">{t("GraphAdditionalRoutes", Math.max(0, inputs.length - Number(!!sourceEdge) - Number(!!micEdge)), Math.max(0, outputs.length - 1))}</p>}
    <section className="card signal-card"><div className="signal-heading"><strong>{nodeName(current)} · {t("SignalMonitor")}</strong></div><div className="simple-node-meter"><NodeMeter id={current.id} /></div></section>
    <div className="gain-row"><span className="field-title">{t("OutputGain")}</span><Gain value={current.gainDb} label={t("OutputGain")} onChange={gainDb => change({ gainDb })} /><GainValue value={current.gainDb} label={t("GraphGainValue")} onChange={gainDb => change({ gainDb })} /></div>
    <section className="card mic-card"><Switch label={t("IncludeMicrophone")} icon="mic" value={micEnabled} disabled={!state.canEditRoute || !state.microphones.length}
      onChange={enabled => { if (enabled) { const id = mic?.deviceId ?? state.microphones.find(d => d.isDefault)?.id ?? state.microphones[0]?.id; if (id) choose("capture", id); }
        else update({ ...draft.current, nodes: draft.current.nodes.map(n => n.id === current.id ? { ...n, preferredMicrophoneId: mic?.id, preferredMicrophoneGainDb: micEdge?.gainDb ?? 0 } : n), edges: draft.current.edges.filter(e => e.id !== micEdge?.id) }); }} />
      {micEnabled && <div className="mic-details"><DeviceSelect label={t("Microphone")} icon="mic" devices={state.microphones} selected={mic?.deviceId ?? null}
        disabled={!state.canEditRoute} onChange={id => choose("capture", id)} /><div className="gain-row"><Gain value={micEdge!.gainDb} label={t("MicrophoneGain")}
          onChange={gainDb => update({ ...draft.current, edges: draft.current.edges.map(e => e.id === micEdge!.id ? { ...e, gainDb } : e) })} />
          <GainValue value={micEdge!.gainDb} label={t("MicrophoneGain")} onChange={gainDb => update({ ...draft.current, edges: draft.current.edges.map(e => e.id === micEdge!.id ? { ...e, gainDb } : e) })} /></div></div>}
    </section>
    {local.nodes.filter(n => n.kind === "bus").length > 1 && <p className="caption">{t("GraphRunningOthers")}</p>}
  </div>;
}
function PanelDialog({ state, act, patch, error }: Actions & { error: string }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const trigger = useRef<HTMLElement | null>(null);
  useEffect(() => {
    if (state.view !== "main") {
      if (!dialog.current?.open) {
        trigger.current = document.activeElement as HTMLElement;
        dialog.current?.showModal();
      }
      const content = dialog.current?.querySelector(".panel-content");
      if (content) content.scrollTop = 0;
    } else if (dialog.current?.open) {
      dialog.current.close();
      if (trigger.current?.isConnected) trigger.current.focus({ preventScroll: true });
    }
  }, [state.view]);
  return <dialog ref={dialog} className="panel-dialog" aria-labelledby="panel-title"
    onCancel={(event) => { event.preventDefault(); void act("closeView"); }}>
    <div className="panel-layout">
      <header className="panel-header">
        <div><h2 id="panel-title">{t(state.view === "help" ? "GuideTitle" : "Settings")}</h2>
          <p className="caption">{t(state.view === "help" ? "GuideSubtitle" : "AutoSaveHint")}</p></div>
        <button autoFocus className="icon-button" aria-label={t("Close")} onClick={() => act("closeView")}><Icon name="close" /></button>
      </header>
      <div className="panel-content">
        {(error || state.noticeText) && <div className="notice" role="alert">{error || state.noticeText}</div>}
        {state.view === "settings" && <SettingsPage state={state} act={act} patch={patch} />}
        {state.view === "help" && <HelpPage />}
      </div>
      <footer className="panel-footer">
        {state.view === "help" && <label className="check-label"><input type="checkbox" checked={state.guideDraft}
          onChange={(e) => act("guideDraft", { dontShowAgain: e.target.checked })} />{t("DontShowAgain")}</label>}
        <button className="button" onClick={() => act("closeView")}>{t("Close")}</button>
      </footer>
    </div>
  </dialog>;
}
function UpdateDialog({ state, act }: Pick<Actions, "state" | "act">) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [opening, setOpening] = useState(false);
  const [error, setError] = useState("");
  useEffect(() => {
    setError("");
    if (state.isUpdateDialogOpen) dialog.current?.showModal();
    else dialog.current?.close();
  }, [state.isUpdateDialogOpen]);
  async function openInstaller() {
    setOpening(true);
    const ok = await act("downloadUpdate");
    setOpening(false);
    if (!ok) setError(t("InstallerLinkFailed"));
  }
  return (
    <dialog
      ref={dialog}
      aria-labelledby="update-dialog-title"
      onCancel={(event) => {
        event.preventDefault();
        if (!state.isDownloadingUpdate) void act("dismissUpdate");
      }}
    >
      <div className="dialog-body">
        <h3 id="update-dialog-title">{t("UpdateAvailableTitle")}</h3>
        <p>
          {state.appVersion} → {state.latestVersion}
        </p>
        <p>{t("UpdateInstallHint")}</p>
        {state.downloadMessage && <p role="status">{state.downloadMessage}</p>}
        {state.isDownloadingUpdate && <progress aria-label={t("UpdateDownloading")} max={state.downloadProgress?.total ?? undefined}
          value={state.downloadProgress?.total ? state.downloadProgress.received : undefined} />}
        {state.isDownloadingUpdate && state.downloadProgress?.total && <p className="download-size">
          {Math.min(100, Math.round(state.downloadProgress.received / state.downloadProgress.total * 100))}% · {(state.downloadProgress.received / 1048576).toFixed(1)} / {(state.downloadProgress.total / 1048576).toFixed(1)} MB
        </p>}
        {state.hasReleasePage && (
          <button className="button small" onClick={() => act("openRelease")}>
            <Icon name="external" size={14} />
            {t("ReleaseNotes")}
          </button>
        )}
        {error && <p role="alert">{error}</p>}
        <div className="dialog-actions">
          <button className="button" onClick={() => act(state.isDownloadingUpdate ? "cancelUpdateDownload" : "dismissUpdate")}>
            {t(state.isDownloadingUpdate ? "Cancel" : "Later")}
          </button>
          <button
            className="button primary"
            disabled={!state.hasUpdateInstaller || opening || state.isDownloadingUpdate}
            onClick={openInstaller}
          >
            {t(state.isDownloadingUpdate ? "UpdateDownloading" : state.updateDownloaded ? "InstallUpdate" : "DownloadInstaller")}
          </button>
        </div>
      </div>
    </dialog>
  );
}
function DeviceSelect({
  label,
  icon,
  devices,
  selected,
  disabled,
  onChange,
}: {
  label: string;
  icon: "speaker" | "cable" | "mic";
  devices: Device[];
  selected: string | null;
  disabled: boolean;
  onChange: (id: string) => void;
}) {
  const current = devices.find((d) => d.id === selected);
  const id = useId();
  return (
    <div className="device-field">
      <label className="field-title" htmlFor={id}>
        <Icon name={icon} size={16} />
        {label}
      </label>
      <select
        id={id}
        aria-describedby={`${id}-hint`}
        value={selected ?? ""}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        title={current?.name}
      >
        <option value="" disabled>
          {devices.length ? t("SelectDevice") : t("NoDevices")}
        </option>
        {selected && !current && <option value={selected}>{t("GraphOffline")}</option>}
        {devices.map((d) => (
          <option key={d.id} value={d.id}>
            {d.name}
          </option>
        ))}
      </select>
      <span className="caption" id={`${id}-hint`}>
        {current?.summary ?? t("RefreshDeviceHint")}
      </span>
    </div>
  );
}
function Switch({
  label,
  description,
  icon,
  value,
  disabled,
  onChange,
}: {
  label: string;
  description?: string;
  icon?: "mic";
  value: boolean;
  disabled?: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <label className="switch-row">
      <span className="switch-copy">
        <span className="field-title">
          {icon && <Icon name={icon} size={16} />}
          {label}
        </span>
        {description && <span className="caption">{description}</span>}
      </span>
      <input
        type="checkbox"
        role="switch"
        className="switch-input"
        checked={value}
        disabled={disabled}
        onChange={(e) => onChange(e.target.checked)}
      />
      <span className="switch-visual" aria-hidden="true" />
    </label>
  );
}
function Range({
  label,
  value,
  min,
  max,
  step = 0.1,
  disabled,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  max: number;
  step?: number;
  disabled?: boolean;
  onChange: (value: number) => void;
}) {
  const [draft, setDraft] = useState(value);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const dragging = useRef(false);
  const committed = useRef(value);
  useEffect(() => {
    if (!dragging.current && !timer.current) { setDraft(value); committed.current = value; }
  }, [value]);
  useEffect(
    () => () => {
      if (timer.current) clearTimeout(timer.current);
    },
    [],
  );
  function commit(n: number) {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
    if (n !== committed.current) { committed.current = n; onChange(n); }
  }
  return (
    <div className="range-control">
    <input
      type="range"
      aria-label={label}
      value={draft}
      min={min}
      max={max}
      step={step}
      disabled={disabled}
      onPointerDown={() => {
        dragging.current = true;
      }}
      onChange={(e) => {
        const n = Number(e.target.value);
        setDraft(n);
        if (timer.current) clearTimeout(timer.current);
        timer.current = setTimeout(() => commit(n), 80);
      }}
      onPointerUp={(e) => {
        dragging.current = false;
        commit(Number(e.currentTarget.value));
      }}
      onBlur={(e) => {
        dragging.current = false;
        commit(Number(e.currentTarget.value));
      }}
      onPointerCancel={(e) => { dragging.current = false; commit(Number(e.currentTarget.value)); }}
      style={
        {
          "--range-fill": `${((draft - min) / (max - min)) * 100}%`,
        } as React.CSSProperties
      }
    />
    <div className="range-ticks" aria-hidden="true">
      {Array.from({ length: 5 }, (_, i) => <span key={i}><i />{i === 0 || i === 2 || i === 4 ? Number((min + (max - min) * i / 4).toFixed(1)) : ""}</span>)}
    </div>
    </div>
  );
}
function Numeric({
  label,
  name,
  state,
  min,
  max,
  step,
  patch,
}: {
  label: string;
  name: keyof Settings;
  state: AppState;
  min: number;
  max: number;
  step: number;
  patch: Actions["patch"];
}) {
  const current = Number(state.settings[name]);
  const [draft, setDraft] = useState(String(current));
  useEffect(() => setDraft(String(current)), [current]);
  async function commit(input: HTMLInputElement) {
    if (!input.checkValidity() || draft.trim() === "") {
      input.reportValidity();
      setDraft(String(current));
      return;
    }
    if (Number(draft) !== current && !(await patch({ [name]: Number(draft) }))) setDraft(String(current));
  }
  return (
    <label className="numeric-field">
      <span className="caption">{label}</span>
      <input
        type="number"
        value={draft}
        min={min}
        max={max}
        step={step}
        disabled={!state.canEditRoute}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={(e) => commit(e.currentTarget)}
        onKeyDown={(e) => {
          if (e.key === "Enter") e.currentTarget.blur();
        }}
      />
    </label>
  );
}
function SettingsPage({ state, act, patch }: Actions) {
  const processingBus = state.graph.nodes.find(n => n.id === state.simpleBusId && n.kind === "bus") ?? state.graph.nodes.find(n => n.kind === "bus");
  const processing = processingBus ?? state.settings;
  const patchProcessing = (values: { compressorEnabled?: boolean; compressorThresholdDb?: number; compressorRatio?: number }) => {
    const latest = readState()!;
    return act("graph", { revision: latest.graphRevision, project: { ...latest.graph,
      nodes: latest.graph.nodes.map(n => n.id === processingBus?.id ? { ...n, ...values } : n) } });
  };
  const [resetOpen, setResetOpen] = useState(false);
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    if (resetOpen) dialog.current?.showModal();
    else dialog.current?.close();
  }, [resetOpen]);
  return (
    <>
      <section className="card settings-card">
        <div className="section-heading">
          <div>
            <h3>{t("Updates")}</h3>
            <p className="caption">{state.appVersion}</p>
          </div>
          <button
            className="button"
            disabled={state.updating || state.isDownloadingUpdate}
            onClick={() => act("checkUpdates")}
          >
            {state.updating ? t("Checking") : t("CheckUpdates")}
          </button>
        </div>
        <p className="caption" role="status">
          {state.updateMessage}
        </p>
        {state.hasReleasePage && (
          <button className="button small" onClick={() => act("openRelease")}>
            <Icon name="external" size={14} />
            {t("ReleasePage")}
          </button>
        )}
      </section>
      <section className="card settings-card">
        <h3>{t("AppBehavior")}</h3>
        <Switch label={t("StartWithWindows")} description={t("StartupHint")} value={state.settings.startWithWindows}
          disabled={!state.canEditSettings} onChange={(startWithWindows) => patch({ startWithWindows })} />
        <label className="device-field language-field">
          <span className="field-title">{t("SettingsLanguage")}</span>
          <select
            value={state.settings.language}
            disabled={!state.canEditSettings}
            onChange={(e) => patch({ language: e.target.value })}
          >
            {languages.map((language) => (
              <option key={language.code} value={language.code}>
                {language.name}
              </option>
            ))}
          </select>
          <span className="caption">{t("LanguageChangeHint")}</span>
        </label>
        <Switch
          label={t("UseTray")}
          description={t("TrayBehaviorHint")}
          value={state.settings.minimizeToTray}
          disabled={!state.canEditSettings}
          onChange={(minimizeToTray) => patch({ minimizeToTray })}
        />
        <p className="inset-note caption">
          {t("TrayExitHint")}
          <br />
          {t("TrayDisabledHint")}
        </p>
      </section>
      <section className="card settings-card">
        <h3>{t("AudioProcessing")}</h3>
        <Switch
          label={t("ReduceLoudSounds")}
          description={t("CompressorHint")}
          value={processing.compressorEnabled}
          disabled={!state.canEditSettings || !processingBus}
          onChange={(compressorEnabled) => patchProcessing({ compressorEnabled })}
        />
        {processing.compressorEnabled && (
          <div className="two-fields">
            <label className="slider-field">
              <span className="caption">
                {t(
                  "ThresholdValue",
                  processing.compressorThresholdDb.toFixed(1),
                )}
              </span>
              <Range
                label={t("CompressorThreshold")}
                value={processing.compressorThresholdDb}
                min={-60}
                max={0}
                disabled={!state.canEditSettings}
                onChange={(compressorThresholdDb) =>
                  patchProcessing({ compressorThresholdDb })
                }
              />
            </label>
            <label className="slider-field">
              <span className="caption">
                {t("RatioValue", processing.compressorRatio.toFixed(1))}
              </span>
              <Range
                label={t("CompressorRatio")}
                value={processing.compressorRatio}
                min={1}
                max={20}
                disabled={!state.canEditSettings}
                onChange={(compressorRatio) => patchProcessing({ compressorRatio })}
              />
            </label>
          </div>
        )}
      </section>
      <section className="card settings-card">
        <h3>{t("AudioEngine")}</h3>
        <div className="two-fields">
          <Numeric
            label={t("SampleRate")}
            name="sampleRate"
            state={state}
            min={22050}
            max={192000}
            step={1}
            patch={patch}
          />
          <Numeric
            label={t("Channels")}
            name="channels"
            state={state}
            min={1}
            max={2}
            step={1}
            patch={patch}
          />
          <Numeric
            label={t("Latency")}
            name="latencyMilliseconds"
            state={state}
            min={10}
            max={250}
            step={1}
            patch={patch}
          />
          <Numeric
            label={t("Buffer")}
            name="bufferMilliseconds"
            state={state}
            min={80}
            max={1000}
            step={1}
            patch={patch}
          />
        </div>
        <p className="caption">
          {t("EngineDefaults")}
          <br />
          {t("StopBeforeEngineChange")}
          <br />{t("EngineBufferHint")}
        </p>
      </section>
      <section className="card settings-card">
        <h3>{t("ManageSettings")}</h3>
        <p className="caption">{t("ResetDescription")}</p>
        <button
          className="button"
          disabled={!state.canResetSettings}
          onClick={() => setResetOpen(true)}
        >
          {t("ResetSettings")}
        </button>
        {state.settingsMessage && (
          <p className="caption" role="status">
            {state.settingsMessage}
          </p>
        )}
      </section>
      <dialog
        ref={dialog}
        onCancel={() => setResetOpen(false)}
        onClose={() => setResetOpen(false)}
      >
        <div className="dialog-body">
          <h3>{t("ResetTitle")}</h3>
          <p>{t("ResetConfirmation")}</p>
          <div className="dialog-actions">
            <button className="button" onClick={() => setResetOpen(false)}>
              {t("Cancel")}
            </button>
            <button
              className="button danger"
              onClick={async () => {
                setResetOpen(false);
                await act("reset");
              }}
            >
              {t("Reset")}
            </button>
          </div>
        </div>
      </dialog>
    </>
  );
}
function HelpPage() {
  const [error, setError] = useState("");
  return (
    <>
      <section className="card settings-card">
        <h3>{t("GuideExtraSetup")}</h3>
        <p>
          <strong>{t("GuideCableRequired")}</strong>
        </p>
        <p className="caption">{t("GuideExistingCable")}</p>
        <p className="caption">{t("GuideCableChoice")}</p>
      </section>
      <section className="card settings-card">
        <h3>{t("GuideCableSetup")}</h3>
        <ol className="steps">
          <li>
            <span>{t("GuideInstallCable")}</span>
            <button
              className="button small"
              onClick={async () => {
                try {
                  await request("openCableWebsite");
                  setError("");
                } catch {
                  setError(t("GuidePageFailed"));
                }
              }}
            >
              {t("InstallPage")}
              <Icon name="external" size={14} />
            </button>
          </li>
          <li>
            <span>{t("GuideChooseSource")}</span>
          </li>
          <li>
            <span>{t("GuideChooseTarget")}</span>
          </li>
          <li>
            <span>{t("GuideChooseInput")}</span>
          </li>
        </ol>
      </section>
      <aside className="flow-note">
        <strong>{t("GuideFlow")}</strong>
        <p className="caption">{t("GuideOtherCable")}</p>
      </aside>
      {error && (
        <p className="notice" role="alert">
          {error}
        </p>
      )}
    </>
  );
}
