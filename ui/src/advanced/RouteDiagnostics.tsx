import { useSyncExternalStore } from "react";
import { readMeter, subscribeMeter } from "../bridge";
import { t } from "../i18n";
import type { AppState, AudioNodeConfig, AudioEdgeConfig, GraphProject } from "../types";
import { deviceList, nodeName } from "./graph";

export function NodeRuntimeBadge({ node, active, running, offline = false }: { node: AudioNodeConfig; active: boolean; running: boolean; offline?: boolean }) {
  const meter = useSyncExternalStore(subscribeMeter, readMeter);
  const error = meter.errors?.[node.id];
  if (!node.enabled) return null;
  const key = error ? "GraphNodeError" : offline ? "GraphOffline" : !active ? "GraphNodeUnconnected" : running ? "GraphNodeRunning" : "GraphNodeReady";
  return <span className={`node-state-badge runtime-${error || offline ? "error" : !active ? "unconnected" : running ? "running" : "ready"}`} title={error}>{t(key)}</span>;
}
export function RouteDiagnostics({ node, project, state, changeNode, changeEdge, inspect, showMissing = true }: {
  node: AudioNodeConfig; project: GraphProject; state: AppState;
  changeNode: (id: string, patch: Partial<AudioNodeConfig>) => void;
  changeEdge: (id: string, patch: Partial<AudioEdgeConfig>) => void; inspect: (id: string) => void; showMissing?: boolean;
}) {
  const meter = useSyncExternalStore(subscribeMeter, readMeter);
  const buses = node.kind === "bus" ? [node] : project.nodes.filter(n => n.kind === "bus" && project.edges.some(e =>
    node.kind === "output" ? e.source === n.id && e.target === node.id : e.source === node.id && e.target === n.id));
  const busIds = new Set(buses.map(n => n.id));
  const links = project.edges.filter(e => busIds.has(e.source) || busIds.has(e.target));
  const ids = new Set([node.id, ...links.flatMap(e => [e.source, e.target])]);
  const members = project.nodes.filter(n => ids.has(n.id));
  const issues: { id: string; text: string; action: string; disabled?: boolean; run: () => void }[] = [];
  for (const n of members) {
    const name = nodeName(n);
    if (!n.enabled) issues.push({ id: n.id + "-disabled", text: t("GraphIssueDisabled", name), action: t("GraphResolveEnable", name), disabled: !state.canEditRoute, run: () => changeNode(n.id, { enabled: true }) });
    if (n.muted) issues.push({ id: n.id + "-muted", text: t("GraphIssueMuted", name), action: t("GraphResolveMute", name), run: () => changeNode(n.id, { muted: false }) });
    if (n.kind !== "bus" && !deviceList(state, n.kind).some(d => d.id === n.deviceId))
      issues.push({ id: n.id + "-offline", text: t("GraphIssueOffline", name), action: t("GraphOpenNodeSettings", name), run: () => inspect(n.id) });
    if (meter.errors?.[n.id]) issues.push({ id: n.id + "-error", text: name + ": " + meter.errors[n.id], action: t("GraphOpenNodeSettings", name), run: () => inspect(n.id) });
  }
  for (const e of links.filter(e => e.muted)) {
    const name = nodeName(project.nodes.find(n => n.id === e.source)!) + " → " + nodeName(project.nodes.find(n => n.id === e.target)!);
    issues.push({ id: e.id, text: t("GraphIssueSendMuted", name), action: t("GraphResolveMute", name), run: () => changeEdge(e.id, { muted: false }) });
  }
  if (node.kind === "bus" && showMissing) {
    if (!project.edges.some(e => e.target === node.id)) issues.push({ id: "input", text: t("GraphIssueNoInput"), action: t("GraphInputAdd"), run: () => inspect(node.id) });
    if (!project.edges.some(e => e.source === node.id)) issues.push({ id: "output", text: t("GraphIssueNoOutput"), action: t("GraphOutputAdd"), run: () => inspect(node.id) });
  }
  if (!issues.length) return null;
  return <div className="route-diagnostics" role="status">{issues.map(issue => <div className="route-diagnostic" key={issue.id}>
    <span>{issue.text}</span><button className="button small" disabled={issue.disabled} onClick={issue.run}>{issue.action}</button>
  </div>)}{!state.canEditRoute && issues.some(i => i.disabled) && <p className="inspector-note">{t("GraphEnableLocked")}</p>}</div>;
}
