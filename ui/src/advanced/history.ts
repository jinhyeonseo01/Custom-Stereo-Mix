import type { GraphProject } from "../types";

export function sameTopology(a: GraphProject, b: GraphProject) {
  return a.nodes.length === b.nodes.length && a.edges.length === b.edges.length &&
    a.nodes.every(n => b.nodes.some(m => m.id === n.id && m.kind === n.kind && m.deviceId === n.deviceId && m.enabled === n.enabled)) &&
    a.edges.every(e => b.edges.some(f => f.id === e.id && f.source === e.source && f.target === e.target));
}
type Entry = { before: GraphProject; after: GraphProject; structure: boolean; group: number | null };
const equal = (a: unknown, b: unknown) => JSON.stringify(a) === JSON.stringify(b);
// Apply only fields changed by this operation, preserving unrelated later edits.
function mergeItems<T extends { id: string }>(current: T[], before: T[], after: T[]): T[] {
  const old = new Map(before.map(n => [n.id, n])), next = new Map(after.map(n => [n.id, n]));
  const result = current.filter(n => !old.has(n.id) || next.has(n.id)).map(n => {
    const a = old.get(n.id), b = next.get(n.id);
    if (!a || !b) return n;
    const copy = { ...n };
    for (const key of new Set([...Object.keys(a), ...Object.keys(b)]) as Set<keyof T>)
      if (!equal(a[key], b[key])) { if (key in b) copy[key] = b[key]; else delete copy[key]; }
    return copy;
  });
  for (const n of after) if (!old.has(n.id) && !result.some(m => m.id === n.id)) result.push(n);
  if (!equal(before.map(n => n.id), after.map(n => n.id))) {
    const order = new Map(after.map((n, i) => [n.id, i]));
    result.sort((a, b) => (order.get(a.id) ?? Infinity) - (order.get(b.id) ?? Infinity));
  }
  return result;
}
function apply(current: GraphProject, entry: Entry, undo: boolean): GraphProject {
  const before = undo ? entry.after : entry.before, after = undo ? entry.before : entry.after;
  return { ...current, name: before.name === after.name ? current.name : after.name,
    nodes: mergeItems(current.nodes, before.nodes, after.nodes), edges: mergeItems(current.edges, before.edges, after.edges) };
}
function applicable(current: GraphProject, entry: Entry) {
  return entry.before.nodes.filter(n => !equal(n, entry.after.nodes.find(m => m.id === n.id))).every(n =>
    current.nodes.some(m => m.id === n.id && m.kind === n.kind && m.deviceId === n.deviceId)) &&
    entry.before.edges.filter(e => !equal(e, entry.after.edges.find(f => f.id === e.id))).every(e =>
      current.edges.some(f => f.id === e.id && f.source === e.source && f.target === e.target));
}
export class GraphHistory {
  private past: Entry[] = [];
  private future: Entry[] = [];
  private group: number | null = null;
  private sequence = 0;
  begin() { this.group = ++this.sequence; }
  end() { this.group = null; }
  clear() { this.past = []; this.future = []; this.end(); }
  record(before: GraphProject, after: GraphProject) {
    if (equal(before, after)) return;
    const last = this.past.at(-1);
    if (this.group !== null && last?.group === this.group && equal(last.after, before)) {
      last.after = after; last.structure = !sameTopology(last.before, after);
      if (equal(last.before, after)) this.past.pop();
    } else this.past.push({ before, after, structure: !sameTopology(before, after), group: this.group });
    this.past = this.past.slice(-100); this.future = [];
  }
  private index(entries: Entry[], current: GraphProject, running: boolean, undo: boolean) {
    for (let i = entries.length - 1; i >= 0; i--) {
      const entry = entries[i];
      if (!running || (!entry.structure && applicable(current, entry))) return i;
    }
    return -1;
  }
  canUndo(current: GraphProject, running: boolean) { return this.index(this.past, current, running, true) >= 0; }
  canRedo(current: GraphProject, running: boolean) { return this.index(this.future, current, running, false) >= 0; }
  undo(current: GraphProject, running: boolean) {
    const i = this.index(this.past, current, running, true); if (i < 0) return null;
    this.end(); const [entry] = this.past.splice(i, 1); this.future.push(entry); return apply(current, entry, true);
  }
  redo(current: GraphProject, running: boolean) {
    const i = this.index(this.future, current, running, false); if (i < 0) return null;
    this.end(); const [entry] = this.future.splice(i, 1); this.past.push(entry); return apply(current, entry, false);
  }
}
