import { useEffect, useState } from "react";
import { t } from "../i18n";
import { X, Trash } from "../icons";
import type { AppState, AudioNodeConfig, AudioEdgeConfig, GraphProject } from "../types";
import { activeNodeIds, deviceList, nodeName, nodeRole } from "./graph";
import { Gain, GainValue, NodeMeter } from "./AudioControls";
import { InspectorToggle } from "./InspectorToggle";
import { NumberInput, TextInput } from "../fields";
import { NodeConnections } from "./NodeConnections";
import { NodeRuntimeBadge, RouteDiagnostics } from "./RouteDiagnostics";

type Props = {
  node?: AudioNodeConfig; edge?: AudioEdgeConfig; state: AppState; project: GraphProject;
  changeNode: (id: string, patch: Partial<AudioNodeConfig>) => void; changeEdge: (id: string, patch: Partial<AudioEdgeConfig>) => void;
  connect: (connection: { source: string; target: string }) => void; selectEdge: (id: string) => void;
  selectNode: (id: string) => void; addDevice: (kind: "loopback" | "capture" | "output", deviceId: string, bus: string) => void;
  deleteNode: (id: string) => void; deleteEdge: (id: string) => void; onClose: () => void;
};
export function NodeInspector({ node: n, edge, state, project, changeNode, changeEdge, connect, selectEdge, selectNode, addDevice, deleteNode, deleteEdge, onClose }: Props) {
  const active = activeNodeIds(project);
  const incoming = project.edges.filter(e => e.target === n?.id), outgoing = project.edges.filter(e => e.source === n?.id);
  const gainLabel = n?.kind === "bus" ? t("MasterGain") : t("GraphNodeGain");
  const [setup, setSetup] = useState(() => n?.kind === "bus" && (!incoming.length || !outgoing.length));
  useEffect(() => { if (n?.kind === "bus" && (!incoming.length || !outgoing.length)) setSetup(true); }, [n?.kind, incoming.length, outgoing.length]);
  const connections = n && <NodeConnections node={n} project={project} state={state} connect={connect} selectEdge={selectEdge} addDevice={addDevice} />;
  const source = edge && project.nodes.find(n => n.id === edge.source), target = edge && project.nodes.find(n => n.id === edge.target);
  const routeInactive = edge && (!active.has(edge.source) || !active.has(edge.target));
  const routeMuted = edge && !edge.muted && (source?.muted || target?.muted);
  return <aside className="graph-inspector" aria-label={t("Inspector")}>
    <header className="inspector-header"><div><small>{n ? nodeRole(n) : t("GraphConnections")}</small><h2>{n ? nodeName(n) : t("SendGain")}</h2>
      {n && <NodeRuntimeBadge node={n} active={active.has(n.id)} running={state.isRouting} offline={n.kind !== "bus" && !deviceList(state, n.kind).some(d => d.id === n.deviceId)} />}</div>
      <button className="icon-button" aria-label={t("Close")} onClick={onClose}><X size={19} /></button></header>
    <div className="inspector-content">
      {n ? <>
        <section className="inspector-activation" aria-label={t("GraphActivation")}>
          <InspectorToggle primary label={t("GraphActivation")} hint={t(n.enabled ? "GraphEnableHint" : "GraphDisableHint")}
            checked={n.enabled} disabled={!state.canEditRoute} onChange={enabled => changeNode(n.id, { enabled })} />
          {!state.canEditRoute && <p className="inspector-note">{t("GraphEnableLocked")}</p>}
        </section>
        {setup && <div className="graph-field setup-name"><label htmlFor={`setup-name-${n.id}`}>{t("NodeName")}</label>
          <TextInput id={`setup-name-${n.id}`} value={n.label} maxLength={160} placeholder={nodeName(n)} onChange={label => changeNode(n.id, { label })} /></div>}
        {setup && connections}
        <RouteDiagnostics node={n} project={project} state={state} changeNode={changeNode} changeEdge={changeEdge} inspect={selectNode} showMissing={false} />
        <section className="inspector-section"><h3>{t("GraphVolume")}</h3>
          <NodeMeter id={n.id} detailed label={nodeName(n) + " · " + t("SignalLevel")} silent={!active.has(n.id) || n.muted} />
          <div className="graph-field"><span>{gainLabel}</span><div className="edge-gain-row">
            <Gain value={n.gainDb} label={gainLabel} showRange onChange={gainDb => changeNode(n.id, { gainDb })} />
            <GainValue value={n.gainDb} label={gainLabel + " " + t("GraphGainValue")} onChange={gainDb => changeNode(n.id, { gainDb })} /></div></div>
          <div className="inspector-mute"><InspectorToggle label={t("Mute")} hint={t("GraphMuteHint")} checked={n.muted} onChange={muted => changeNode(n.id, { muted })} /></div>
        </section>
        {!setup && <section className="inspector-section"><h3>{t("GraphIdentity")}</h3>
          <div className="graph-field"><label htmlFor={`node-name-${n.id}`}>{t("NodeName")}</label>
            <TextInput id={`node-name-${n.id}`} value={n.label} maxLength={160} placeholder={nodeName(n)} onChange={label => changeNode(n.id, { label })} /></div>
          {n.kind !== "bus" && <div className="graph-field"><label htmlFor={`node-device-${n.id}`}>{t("SelectDevice")}</label>
            <select id={`node-device-${n.id}`} value={n.deviceId ?? ""} disabled={!state.canEditRoute} onChange={e => changeNode(n.id, { deviceId: e.target.value })}>
              <option value="">{t("SelectDevice")}</option>
              {n.deviceId && !deviceList(state, n.kind).some(d => d.id === n.deviceId) && <option value={n.deviceId}>{t("GraphOffline")}</option>}
              {deviceList(state, n.kind).map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
            <small className="inspector-note">{deviceList(state, n.kind).find(d => d.id === n.deviceId)?.summary ?? t("GraphOffline")}</small>
          </div>}
        </section>}
        {(n.kind === "bus" || n.kind === "output") && <section className="inspector-section"><h3>{t("GraphProcessing")}</h3>
          <InspectorToggle label={t("ReduceLoudSounds")} hint={t("CompressorHint")} checked={n.compressorEnabled} onChange={compressorEnabled => changeNode(n.id, { compressorEnabled })} />
          {n.compressorEnabled && <div className="compressor-controls">
            <NumberInput showLabel label={t("CompressorThreshold")} value={n.compressorThresholdDb} min={-60} max={0} unit="dBFS" onChange={compressorThresholdDb => changeNode(n.id, { compressorThresholdDb })} />
            <NumberInput showLabel label={t("CompressorRatio")} value={n.compressorRatio} min={1} max={20} unit=": 1" onChange={compressorRatio => changeNode(n.id, { compressorRatio })} />
          </div>}
          <InspectorToggle label={t("Limiter")} hint={t("GraphLimiterHint")} checked={n.limiterEnabled} onChange={limiterEnabled => changeNode(n.id, { limiterEnabled })} />
        </section>}
        {!setup && connections}
      </> : edge && source && target ? <>
        <section className="inspector-section"><h3>{t("GraphConnections")}</h3><div className="inspector-route-summary">
          <button className="inspector-route" onClick={() => selectNode(source.id)}>{nodeName(source)}</button><button className="inspector-route" onClick={() => selectNode(target.id)}>→ {nodeName(target)}</button></div>
          {(routeInactive || routeMuted) && <p className="inspector-note route-state-note">{t(routeInactive ? "GraphRouteDisabled" : "GraphRouteMuted")}</p>}
          {[source, target].filter(node => node.muted || !node.enabled || node.kind !== "bus" && !deviceList(state, node.kind).some(d => d.id === node.deviceId)).map(node =>
            <div className="route-diagnostic" key={node.id}><button className="button small" onClick={() => selectNode(node.id)}>{t("GraphOpenNodeSettings", nodeName(node))}</button>
              {node.muted && <button className="button small" onClick={() => changeNode(node.id, { muted: false })}>{t("GraphResolveMute", nodeName(node))}</button>}</div>)}
        </section>
        <section className="inspector-section"><h3>{t("GraphVolume")}</h3>
          <div className="graph-field"><span>{t("SendGain")}</span><div className="edge-gain-row"><Gain value={edge.gainDb} label={t("SendGain")} showRange onChange={gainDb => changeEdge(edge.id, { gainDb })} />
            <GainValue value={edge.gainDb} label={t("GraphGainValue")} onChange={gainDb => changeEdge(edge.id, { gainDb })} /></div></div>
          <div className="inspector-mute"><InspectorToggle label={t("Mute")} hint={t("GraphMuteHint")} checked={edge.muted} onChange={muted => changeEdge(edge.id, { muted })} /></div>
        </section>
      </> : null}
    </div>
    <footer className="inspector-footer"><button className="button danger small" disabled={!state.canEditRoute} onClick={() => n ? deleteNode(n.id) : edge && deleteEdge(edge.id)}>
      <Trash size={16} />{t(n ? "NodeDelete" : "EdgeDelete")}</button></footer>
  </aside>;
}
