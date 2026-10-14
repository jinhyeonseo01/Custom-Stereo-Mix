import { useEffect, useId, useRef, useState } from "react";
import { t, languages } from "./i18n";
import { Icon } from "./icons";
import type { AppState, Command, Patch } from "./types";
import { NumberInput } from "./fields";
import { readState } from "./bridge";
import { InspectorToggle } from "./advanced/InspectorToggle";
import { nodeName } from "./advanced/graph";

type Props = { state: AppState; act: (command: Command, payload?: unknown) => Promise<boolean>; patch: (patch: Patch) => Promise<boolean> };
const rates = [44100, 48000, 96000, 192000], latencies = [20, 50, 100];
export function SettingsPage({ state, act, patch }: Props) {
  const [tab, setTab] = useState(0), [customRate, setCustomRate] = useState(!rates.includes(state.settings.sampleRate));
  const [customLatency, setCustomLatency] = useState(!latencies.includes(state.settings.latencyMilliseconds));
  const [resetOpen, setResetOpen] = useState(false);
  const buttons = useRef<(HTMLButtonElement | null)[]>([]), resetDialog = useRef<HTMLDialogElement>(null), id = useId();
  const processingQueue = useRef(Promise.resolve(true));
  useEffect(() => { if (resetOpen) resetDialog.current?.showModal(); else resetDialog.current?.close(); }, [resetOpen]);
  const bus = state.graph.nodes.find(n => n.id === state.simpleBusId && n.kind === "bus") ?? state.graph.nodes.find(n => n.kind === "bus");
  function processing(values: { compressorEnabled?: boolean; compressorThresholdDb?: number; compressorRatio?: number; limiterEnabled?: boolean }) {
    processingQueue.current = processingQueue.current.then(() => {
      const latest = readState()!;
      return act("graph", { revision: latest.graphRevision, project: { ...latest.graph, nodes: latest.graph.nodes.map(n => n.id === bus?.id ? { ...n, ...values } : n) } });
    });
    return processingQueue.current;
  }
  return <>
    <div className="settings-tabs" role="tablist" aria-label={t("Settings")} onKeyDown={e => {
      if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(e.key)) return;
      e.preventDefault(); const next = e.key === "Home" ? 0 : e.key === "End" ? 2 : (tab + (e.key === "ArrowRight" ? 1 : 2)) % 3;
      setTab(next); buttons.current[next]?.focus();
    }}>{(["SettingsEngineTab", "SettingsAppTab", "SettingsInfoTab"] as const).map((key, index) =>
      <button key={key} ref={el => { buttons.current[index] = el; }} role="tab" id={id + "-tab-" + index} aria-controls={id + "-panel"} aria-selected={tab === index} tabIndex={tab === index ? 0 : -1} onClick={() => setTab(index)}>{t(key)}</button>)}</div>
    <div id={id + "-panel"} role="tabpanel" aria-labelledby={id + "-tab-" + tab} className="settings-tab-content">
      {tab === 0 && <>
        <section className="card settings-card"><h3>{t("AudioEngine")}</h3>
          <div className="two-fields"><label className="device-field"><span className="field-title">{t("SampleRate")}</span>
            <select disabled={!state.canEditRoute} value={customRate ? "custom" : String(state.settings.sampleRate)} onChange={e => {
              setCustomRate(e.target.value === "custom"); if (e.target.value !== "custom") void patch({ sampleRate: Number(e.target.value) });
            }}>{rates.map(rate => <option key={rate} value={rate}>{rate / 1000} kHz</option>)}<option value="custom">{t("SettingsCustom")}</option></select>
          </label><label className="device-field"><span className="field-title">{t("SettingsChannels")}</span>
            <select disabled={!state.canEditRoute} value={state.settings.channels} onChange={e => patch({ channels: Number(e.target.value) })}><option value="1">{t("SettingsMono")}</option><option value="2">{t("SettingsStereo")}</option></select>
          </label></div>
          {customRate && <NumberInput showLabel label={t("SampleRate")} value={state.settings.sampleRate} min={22050} max={192000} step={1} unit="Hz" disabled={!state.canEditRoute} onChange={sampleRate => patch({ sampleRate })} />}
          <label className="device-field"><span className="field-title">{t("Latency")}</span><select value={customLatency ? "custom" : String(state.settings.latencyMilliseconds)} disabled={!state.canEditRoute} onChange={e => {
            setCustomLatency(e.target.value === "custom"); if (e.target.value !== "custom") void patch({ latencyMilliseconds: Number(e.target.value) });
          }}><option value="20">{t("SettingsLatencyFast")}</option><option value="50">{t("SettingsLatencyBalanced")}</option><option value="100">{t("SettingsLatencySafe")}</option><option value="custom">{t("SettingsCustom")}</option></select></label>
          {customLatency && <NumberInput showLabel label={t("Latency")} value={state.settings.latencyMilliseconds} min={10} max={250} step={1} unit="ms" disabled={!state.canEditRoute} onChange={latencyMilliseconds => patch({ latencyMilliseconds })} />}
          <p className="caption">{t("EngineDefaults")}<br />{t("StopBeforeEngineChange")}</p>
          <details className="engine-details"><summary>{t("SettingsAdvancedEngine")}</summary>
            <NumberInput showLabel label={t("Buffer")} value={state.settings.bufferMilliseconds} min={80} max={1000} step={1} unit="ms" disabled={!state.canEditRoute} onChange={bufferMilliseconds => patch({ bufferMilliseconds })} />
            <p className="caption">{t("EngineBufferHint")}</p>
          </details>
        </section>
        {state.advancedMode ? <p className="inset-note caption">{t("GraphNodeSettingsHint")}</p> : bus && <section className="card settings-card">
          <h3>{t("GraphSelectedProcessing", nodeName(bus))}</h3>
          <InspectorToggle label={t("ReduceLoudSounds")} hint={t("CompressorHint")} checked={bus.compressorEnabled} onChange={compressorEnabled => processing({ compressorEnabled })} />
          {bus.compressorEnabled && <div className="two-fields">
            <NumberInput showLabel label={t("CompressorThreshold")} value={bus.compressorThresholdDb} min={-60} max={0} unit="dBFS" onChange={compressorThresholdDb => processing({ compressorThresholdDb })} />
            <NumberInput showLabel label={t("CompressorRatio")} value={bus.compressorRatio} min={1} max={20} unit=": 1" onChange={compressorRatio => processing({ compressorRatio })} />
          </div>}
          <InspectorToggle label={t("Limiter")} hint={t("GraphLimiterHint")} checked={bus.limiterEnabled} onChange={limiterEnabled => processing({ limiterEnabled })} />
        </section>}
      </>}
      {tab === 1 && <>
        <section className="card settings-card"><h3>{t("AppBehavior")}</h3>
          <label className="device-field"><span className="field-title">{t("SettingsLanguage")}</span><select value={state.settings.language} disabled={!state.canEditSettings} onChange={e => patch({ language: e.target.value })}>
            {languages.map(language => <option key={language.code} value={language.code}>{language.name}</option>)}
          </select><span className="caption">{t("LanguageChangeHint")}</span></label>
          <InspectorToggle label={t("StartWithWindows")} hint={t("StartupHint")} checked={state.settings.startWithWindows} disabled={!state.canEditSettings} onChange={startWithWindows => patch({ startWithWindows })} />
          <label className="device-field"><span className="field-title">{t("SettingsCloseBehavior")}</span><select value={state.settings.closeToTray ? "tray" : "exit"} disabled={!state.canEditSettings} onChange={e => patch({ closeToTray: e.target.value === "tray" })}>
            <option value="tray">{t("SettingsCloseHide")}</option><option value="exit">{t("SettingsCloseExit")}</option>
          </select></label>
          <InspectorToggle label={t("SettingsMinimizeHide")} hint={t("SettingsMinimizeHint")} checked={state.settings.minimizeToTray} disabled={!state.canEditSettings} onChange={minimizeToTray => patch({ minimizeToTray })} />
          <p className="inset-note caption">{t("TrayExitHint")}</p>
        </section>
        <section className="card settings-card"><h3>{t("ManageSettings")}</h3><p className="caption">{t("ResetDescription")}</p>
          <button className="button danger" disabled={!state.canResetSettings} onClick={() => setResetOpen(true)}>{t("ResetSettings")}</button>
          {state.settingsMessage && <p className="caption" role="status">{state.settingsMessage}</p>}
        </section>
      </>}
      {tab === 2 && <section className="card settings-card"><div className="section-heading"><div><h3>{t("Updates")}</h3><p className="caption">{state.appVersion}</p></div>
        <button className="button" disabled={state.updating || state.isDownloadingUpdate} onClick={() => act("checkUpdates")}>{t(state.updating ? "Checking" : "CheckUpdates")}</button></div>
        <p className="caption" role="status">{state.updateMessage}</p>{state.hasReleasePage && <button className="button small" onClick={() => act("openRelease")}><Icon name="external" size={14} />{t("ReleasePage")}</button>}
      </section>}
    </div>
    <dialog ref={resetDialog} aria-labelledby={id + "-reset"} onCancel={() => setResetOpen(false)} onClose={() => setResetOpen(false)}>
      <div className="dialog-body"><h3 id={id + "-reset"}>{t("ResetTitle")}</h3><p>{t("ResetConfirmation")}</p><div className="dialog-actions">
        <button className="button" onClick={() => setResetOpen(false)}>{t("Cancel")}</button><button className="button danger" onClick={async () => { setResetOpen(false); await act("reset"); }}>{t("Reset")}</button>
      </div></div>
    </dialog>
  </>;
}
