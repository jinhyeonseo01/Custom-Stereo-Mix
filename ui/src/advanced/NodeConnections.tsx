import { t } from "../i18n";
import type { AppState, AudioNodeConfig, GraphProject } from "../types";
import { deviceList, nodeName } from "./graph";
export function NodeConnections({ node: n, project, state, connect, selectEdge, addDevice }: {
  node: AudioNodeConfig; project: GraphProject; state: AppState;
  connect: (c: { source: string; target: string }) => void; selectEdge: (id: string) => void;
  addDevice: (kind: "loopback" | "capture" | "output", deviceId: string, bus: string) => void;
}) {
  const incoming = project.edges.filter(e => e.target === n.id), outgoing = project.edges.filter(e => e.source === n.id);
  return <section className="inspector-section inspector-connections"><h3>{t("GraphConnections")}</h3>
    {n.kind === "bus" && <p className="inspector-note setup-hint">{t(incoming.length && outgoing.length ? "GraphSetupComplete" : "GraphSetupHint")}</p>}
    {(["in", "out"] as const).filter(direction => n.kind === "bus" || direction === (n.kind === "output" ? "in" : "out")).map(direction => {
      const input = direction === "in", links = input ? incoming : outgoing, other = (e: typeof links[number]) => input ? e.source : e.target;
      const available = project.nodes.filter(d => input ? (n.kind === "output" ? d.kind === "bus" : d.kind === "capture" || d.kind === "loopback") : d.kind === (n.kind === "bus" ? "output" : "bus"))
        .filter(d => !links.some(e => other(e) === d.id));
      return <div className="inspector-routes" key={direction}><span>{t(input ? "GraphIncoming" : "GraphOutgoing")}<small>{links.length}</small></span>
        {links.length ? links.map(e => <button className="inspector-route" key={e.id} onClick={() => selectEdge(e.id)}>
          <span>{nodeName(project.nodes.find(node => node.id === other(e))!)}</span><small>{e.muted ? t("GraphNodeMuted") : e.gainDb.toFixed(1) + " dB"}</small></button>) : <p className="inspector-note">{t("GraphNoConnections")}</p>}
        <label className="graph-field"><span>{t(input ? "GraphInputAdd" : "GraphOutputAdd")}</span><select value="" disabled={!state.canEditRoute} onChange={e => {
          if (!e.target.value) return; const [kind, ...parts] = e.target.value.split(":"), id = parts.join(":");
          if (kind === "node") connect(input ? { source: id, target: n.id } : { source: n.id, target: id });
          else addDevice(kind as "loopback" | "capture" | "output", id, n.id);
        }}><option value="">{t(input ? "GraphInputAdd" : "GraphOutputAdd")}</option>
          <optgroup label={t("GraphFindExisting")}>{available.map(d => <option key={d.id} value={"node:" + d.id}>{nodeName(d)}</option>)}</optgroup>
          {n.kind === "bus" && (input ? ["loopback", "capture"] as const : ["output"] as const).map(kind => <optgroup key={kind} label={t(kind === "capture" ? "InputAudio" : kind === "loopback" ? "PlaybackAudio" : "OutputAudio")}>
            {deviceList(state, kind).filter(d => !project.nodes.some(node => node.kind === kind && node.deviceId === d.id)).map(d => <option key={d.id} value={kind + ":" + d.id}>{d.name}</option>)}
          </optgroup>)}
        </select></label>
      </div>;
    })}
  </section>;
}
