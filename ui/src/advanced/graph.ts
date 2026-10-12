import { t } from "../i18n";
import type { AppState, AudioNodeConfig, AudioNodeKind, GraphProject } from "../types";
export const nodeName = (n: AudioNodeConfig) => n.label || t(n.kind === "bus" ? "MixBus" : n.kind === "capture" ? "InputAudio" : n.kind === "output" ? "OutputAudio" : "PlaybackAudio");
export const deviceList = (s: AppState, k: AudioNodeKind) => k === "capture" ? s.microphones : k === "bus" ? [] : k === "output" ? s.targets : s.sources;
// Unity sits at the centre. Each side uses its own dB range.
export const gainToSlider = (db: number) => db <= 0 ? db / 60 * 100 : db / 12 * 100;
export const sliderToGain = (value: number) => Math.round((value <= 0 ? value / 100 * 60 : value / 100 * 12) * 10) / 10;
export function createNode(kind: AudioNodeKind, deviceId: string | null, label: string, index = 0): AudioNodeConfig {
  return { id: crypto.randomUUID(), kind, label, deviceId, position: { x: kind === "bus" ? 560 : kind === "output" ? 1145 : 0, y: index * (kind === "bus" ? 360 : 180) },
    gainDb: 0, muted: false, enabled: true, limiterEnabled: kind === "output",
    compressorEnabled: false, compressorThresholdDb: -12, compressorRatio: 4 };
}
export function graphError(g: GraphProject, catalog?: Pick<AppState, "sources" | "targets" | "microphones">): string {
  if (!g || g.schemaVersion !== 2 || !Array.isArray(g.nodes) || !Array.isArray(g.edges) ||
    g.nodes.length > 128 || g.edges.length > 512 || typeof g.name !== "string" || g.name.length > 120) return t("GraphInvalid");
  const ids = new Set<string>(), devices = new Set<string>(), pairs = new Set<string>(), edgeIds = new Set<string>();
  for (const n of g.nodes) {
    if (!n || !["loopback", "capture", "bus", "output"].includes(n.kind) || !n.id || n.id.length > 80 || ids.has(n.id) ||
      typeof n.label !== "string" || n.label.length > 160 || !n.position || !Number.isFinite(n.position.x) || !Number.isFinite(n.position.y) ||
      Math.abs(n.position.x) > 100000 || Math.abs(n.position.y) > 100000 ||
      !Number.isFinite(n.gainDb) || n.gainDb < -60 || n.gainDb > 12 ||
      !Number.isFinite(n.compressorThresholdDb) || n.compressorThresholdDb < -60 || n.compressorThresholdDb > 0 ||
      !Number.isFinite(n.compressorRatio) || n.compressorRatio < 1 || n.compressorRatio > 20 ||
      typeof n.muted !== "boolean" || typeof n.enabled !== "boolean") return t("GraphInvalid");
    ids.add(n.id);
    if (n.kind === "output" && n.deviceId) {
      if (devices.has(n.deviceId.toLowerCase())) return t("GraphDuplicate");
      devices.add(n.deviceId.toLowerCase());
    }
  }
  const adjacency = new Map(g.nodes.map(n => [n.id, [] as string[]]));
  for (const e of g.edges) {
    const s = g.nodes.find(n => n.id === e.source), d = g.nodes.find(n => n.id === e.target);
    if (!e.id || edgeIds.has(e.id) || !s || !d || !Number.isFinite(e.gainDb) || e.gainDb < -60 || e.gainDb > 12 ||
      !((["loopback", "capture"].includes(s.kind) && d.kind === "bus") || (s.kind === "bus" && d.kind === "output"))) return t("GraphInvalid");
    edgeIds.add(e.id);
    const pair = e.source + ":" + e.target;
    if (pairs.has(pair)) return t("GraphDuplicate");
    pairs.add(pair); adjacency.get(e.source)!.push(e.target);
  }
  for (const o of g.nodes.filter(n => n.kind === "output" && n.deviceId))
    for (const s of g.nodes.filter(n => n.kind === "loopback" && n.deviceId?.toLowerCase() === o.deviceId?.toLowerCase())) adjacency.get(o.id)!.push(s.id);
  if (catalog) for (const o of g.nodes.filter(n => n.kind === "output")) {
    const group = catalog.targets.find(d => d.id === o.deviceId)?.returnPathGroup;
    if (group) for (const s of g.nodes.filter(n => n.kind === "capture"))
      if (catalog.microphones.find(d => d.id === s.deviceId)?.returnPathGroup === group) adjacency.get(o.id)!.push(s.id);
  }
  const visiting = new Set<string>(), visited = new Set<string>();
  const cycle = (id: string): boolean => {
    if (visited.has(id)) return false;
    if (visiting.has(id)) return true;
    visiting.add(id); if (adjacency.get(id)!.some(cycle)) return true;
    visiting.delete(id); visited.add(id); return false;
  };
  return g.nodes.some(n => cycle(n.id)) ? t("GraphCycle") : "";
}
export function sameTopology(a: GraphProject, b: GraphProject) {
  return a.nodes.length === b.nodes.length && a.edges.length === b.edges.length &&
    a.nodes.every(n => b.nodes.some(m => m.id === n.id && m.kind === n.kind && m.deviceId === n.deviceId && m.enabled === n.enabled)) &&
    a.edges.every(e => b.edges.some(f => f.id === e.id && f.source === e.source && f.target === e.target));
}
export function previewGraph(): GraphProject {
  const nodes = [createNode("loopback", "speakers", t("GraphSourceName")), createNode("capture", "mic", t("GraphMicName"), 1),
    createNode("loopback", "music", t("GraphMusicName"), 2), createNode("bus", null, t("GraphBroadcastName")),
    createNode("bus", null, t("GraphCallName")), createNode("output", "cable", "CABLE Input"),
    createNode("output", "headphone", t("GraphHeadphones"), 1), createNode("output", "cable-a", "CABLE-A Input", 2)];
  nodes[4].position.y = 360;
  const link = (s: number, d: number, gainDb = 0) => ({ id: crypto.randomUUID(), source: nodes[s].id, target: nodes[d].id, gainDb, muted: false });
  return { schemaVersion: 2, name: t("GraphBroadcastName") + " + " + t("GraphCallName"), nodes,
    edges: [link(0, 3, -6), link(1, 3), link(2, 3, -12), link(1, 4), link(3, 5), link(3, 6), link(4, 7)] };
}
