import { useEffect, useId, useRef, useState } from "react";
import { t } from "../i18n";
import { Power, VolumeOff, X, Trash } from "../icons";
import type { AppState, AudioNodeConfig, AudioEdgeConfig, GraphProject } from "../types";
import { activeNodeIds, deviceList, nodeName, nodeRole } from "./graph";
import { Gain, GainValue, NodeMeter } from "./AudioControls";

export function InspectorToggle({ label, hint, checked, disabled = false, primary = false, onChange }:
  { label: string; hint: string; checked: boolean; disabled?: boolean; primary?: boolean; onChange: (checked: boolean) => void }) {
  const hintId = useId();
  return <label className={`inspector-toggle ${primary ? "activation-control" : ""} ${checked ? "is-on" : "is-off"} ${disabled ? "is-locked" : ""}`}>
    {primary && <Power size={23} />}
    <span className="inspector-toggle-copy"><strong>{label}</strong><small id={hintId}>{hint}</small></span>
    <input type="checkbox" role="switch" checked={checked} disabled={disabled} aria-label={label} aria-describedby={hintId} onChange={e => onChange(e.target.checked)} />
    <span className="switch-visual" aria-hidden="true" />
  </label>;
}
function NumberField({ label, value, min, max, step, onChange }: { label: string; value: number; min: number; max: number; step: number; onChange: (value: number) => void }) {
  const [draft, setDraft] = useState(String(value));
  const cancelled = useRef(false);
  const id = useId();
  useEffect(() => setDraft(String(value)), [value]);
  function commit() {
    if (cancelled.current) { cancelled.current = false; return; }
    const next = Number(draft);
    if (draft.trim() && Number.isFinite(next) && next >= min && next <= max) onChange(next);
    else setDraft(String(value));
  }
  return <div className="graph-field"><label htmlFor={id}>{label}</label><input id={id} type="number" value={draft} min={min} max={max} step={step}
    onChange={e => setDraft(e.target.value)} onBlur={commit} onKeyDown={e => {
      if (e.key === "Enter") e.currentTarget.blur();
      if (e.key === "Escape") {
        e.preventDefault(); e.stopPropagation(); cancelled.current = true;
        setDraft(String(value)); e.currentTarget.blur();
      }
    }} /></div>;
}
type Props = {
  node?: AudioNodeConfig; edge?: AudioEdgeConfig; state: AppState; project: GraphProject;
  changeNode: (id: string, patch: Partial<AudioNodeConfig>) => void; changeEdge: (id: string, patch: Partial<AudioEdgeConfig>) => void;
  connect: (connection: { source: string; target: string }) => void; selectEdge: (id: string) => void;
  deleteNode: (id: string) => void; deleteEdge: (id: string) => void; onClose: () => void;
};
export function NodeInspector({ node: n, edge, state, project, changeNode, changeEdge, connect, selectEdge, deleteNode, deleteEdge, onClose }: Props) {
  const active = activeNodeIds(project);
  const incoming = project.edges.filter(e => e.target === n?.id), outgoing = project.edges.filter(e => e.source === n?.id);
  const gainLabel = n?.kind === "bus" ? t("MasterGain") : t("GraphNodeGain");
  const source = edge && project.nodes.find(n => n.id === edge.source), target = edge && project.nodes.find(n => n.id === edge.target);
  const routeInactive = edge && (!active.has(edge.source) || !active.has(edge.target));
  const routeMuted = edge && !edge.muted && (source?.muted || target?.muted);
  return <aside className="graph-inspector" aria-label={t("Inspector")}>
    <header className="inspector-header"><div><small>{n ? nodeRole(n) : t("GraphConnections")}</small><h2>{n ? nodeName(n) : t("SendGain")}</h2></div>
      <button className="icon-button" aria-label={t("Close")} onClick={onClose}><X size={19} /></button></header>
    <div className="inspector-content">
      {n ? <>
        <section className="inspector-activation" aria-label={t("GraphActivation")}>
          <InspectorToggle primary label={t("GraphActivation")} hint={t(n.enabled ? "GraphEnableHint" : "GraphDisableHint")}
            checked={n.enabled} disabled={!state.canEditRoute} onChange={enabled => changeNode(n.id, { enabled })} />
          {!state.canEditRoute && <p className="inspector-note">{t("GraphEnableLocked")}</p>}
        </section>
        <section className="inspector-section"><h3>{t("GraphVolume")}</h3>
          <div className="inspector-level"><span>{t("SignalLevel")}</span><NodeMeter id={n.id} silent={!active.has(n.id) || n.muted} /></div>
          <div className="graph-field"><span>{gainLabel}</span><div className="edge-gain-row">
            <Gain value={n.gainDb} label={gainLabel} onChange={gainDb => changeNode(n.id, { gainDb })} />
            <GainValue value={n.gainDb} label={gainLabel + " " + t("GraphGainValue")} onChange={gainDb => changeNode(n.id, { gainDb })} /></div></div>
          <div className="inspector-mute"><InspectorToggle label={t("Mute")} hint={t("GraphMuteHint")} checked={n.muted} onChange={muted => changeNode(n.id, { muted })} /></div>
        </section>
        <section className="inspector-section"><h3>{t("GraphIdentity")}</h3>
          <div className="graph-field"><label htmlFor={`node-name-${n.id}`}>{t("NodeName")}</label>
            <input id={`node-name-${n.id}`} value={n.label} maxLength={160} placeholder={nodeName(n)} onChange={e => changeNode(n.id, { label: e.target.value })} /></div>
          {n.kind !== "bus" && <div className="graph-field"><label htmlFor={`node-device-${n.id}`}>{t("SelectDevice")}</label>
            <select id={`node-device-${n.id}`} value={n.deviceId ?? ""} disabled={!state.canEditRoute} onChange={e => changeNode(n.id, { deviceId: e.target.value })}>
              <option value="">{t("SelectDevice")}</option>
              {n.deviceId && !deviceList(state, n.kind).some(d => d.id === n.deviceId) && <option value={n.deviceId}>{t("GraphOffline")}</option>}
              {deviceList(state, n.kind).map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
            <small className="inspector-note">{deviceList(state, n.kind).find(d => d.id === n.deviceId)?.summary ?? t("GraphOffline")}</small>
          </div>}
        </section>
        {(n.kind === "bus" || n.kind === "output") && <section className="inspector-section"><h3>{t("GraphProcessing")}</h3>
          <InspectorToggle label={t("ReduceLoudSounds")} hint={t("CompressorHint")} checked={n.compressorEnabled} onChange={compressorEnabled => changeNode(n.id, { compressorEnabled })} />
          {n.compressorEnabled && <div className="compressor-controls">
            <NumberField label={t("CompressorThreshold")} value={n.compressorThresholdDb} min={-60} max={0} step={.1} onChange={compressorThresholdDb => changeNode(n.id, { compressorThresholdDb })} />
            <NumberField label={t("CompressorRatio")} value={n.compressorRatio} min={1} max={20} step={.1} onChange={compressorRatio => changeNode(n.id, { compressorRatio })} />
          </div>}
          <InspectorToggle label={t("Limiter")} hint={t("GraphLimiterHint")} checked={n.limiterEnabled} onChange={limiterEnabled => changeNode(n.id, { limiterEnabled })} />
        </section>}
        <section className="inspector-section"><h3>{t("GraphConnections")}</h3>
          {[{ direction: "in", label: t("GraphIncoming"), links: incoming, other: (e: AudioEdgeConfig) => e.source }, { direction: "out", label: t("GraphOutgoing"), links: outgoing, other: (e: AudioEdgeConfig) => e.target }]
            .filter(group => n.kind === "bus" || group.direction === (n.kind === "output" ? "in" : "out")).map(group =>
              <div className="inspector-routes" key={group.label}><span>{group.label}<small>{group.links.length}</small></span>
                {group.links.length ? group.links.map(e => <button className="inspector-route" key={e.id} onClick={() => selectEdge(e.id)}>
                  <span>{nodeName(project.nodes.find(node => node.id === group.other(e))!)}</span><small>{e.muted ? t("GraphNodeMuted") : e.gainDb.toFixed(1) + " dB"}</small></button>)
                  : <p className="inspector-note">{t("GraphNoConnections")}</p>}
              </div>)}
          {n.kind !== "output" && <div className="graph-field"><label htmlFor={`node-connect-${n.id}`}>{t("GraphDestination")}</label>
            <select id={`node-connect-${n.id}`} value="" disabled={!state.canEditRoute} onChange={e => connect({ source: n.id, target: e.target.value })}>
              <option value="">{t("GraphConnect")}</option>{project.nodes.filter(d => d.kind === (n.kind === "bus" ? "output" : "bus") && !outgoing.some(e => e.target === d.id)).map(d => <option key={d.id} value={d.id}>{nodeName(d)}</option>)}
            </select></div>}
        </section>
      </> : edge && source && target ? <>
        <section className="inspector-section"><h3>{t("GraphConnections")}</h3><div className="inspector-route-summary"><strong>{nodeName(source)}</strong><span>→ {nodeName(target)}</span></div>
          {(routeInactive || routeMuted) && <p className="inspector-note route-state-note">{t(routeInactive ? "GraphRouteDisabled" : "GraphRouteMuted")}</p>}
        </section>
        <section className="inspector-section"><h3>{t("GraphVolume")}</h3>
          <div className="graph-field"><span>{t("SendGain")}</span><div className="edge-gain-row"><Gain value={edge.gainDb} label={t("SendGain")} onChange={gainDb => changeEdge(edge.id, { gainDb })} />
            <GainValue value={edge.gainDb} label={t("GraphGainValue")} onChange={gainDb => changeEdge(edge.id, { gainDb })} /></div></div>
          <div className="inspector-mute"><InspectorToggle label={t("Mute")} hint={t("GraphMuteHint")} checked={edge.muted} onChange={muted => changeEdge(edge.id, { muted })} /></div>
        </section>
      </> : null}
    </div>
    <footer className="inspector-footer"><button className="button danger small" disabled={!state.canEditRoute} onClick={() => n ? deleteNode(n.id) : edge && deleteEdge(edge.id)}>
      <Trash size={16} />{t(n ? "NodeDelete" : "EdgeDelete")}</button></footer>
  </aside>;
}
