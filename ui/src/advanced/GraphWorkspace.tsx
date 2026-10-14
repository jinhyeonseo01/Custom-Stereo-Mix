import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, useSyncExternalStore, memo } from "react";
import { ReactFlow, ReactFlowProvider, Handle, Position, Background, BackgroundVariant, MiniMap,
  applyNodeChanges, useReactFlow, useViewport, useNodesInitialized, useUpdateNodeInternals, MarkerType, type Node, type NodeProps, type Connection } from "@xyflow/react";
import { SlidersHorizontal, Headphones, Music2, Monitor, Mic, Volume2, VolumeOff, Power, Plus, Maximize2, PanelLeft, PanelBottom, X, MoreHorizontal, Undo2, Redo2, ArrowDownToLine, ArrowUpFromLine, LayoutGrid, ZoomIn, ZoomOut } from "../icons";
import "@xyflow/react/dist/style.css";
import "./advanced.css";
import { t } from "../i18n";
import { request, readState, readMeter, subscribeMeter } from "../bridge";
import type { AppState, AudioNodeConfig, AudioEdgeConfig, AudioNodeKind, GraphProject } from "../types";
import { createNode, deviceList, graphError, nodeName, nodeRole, activeNodeIds } from "./graph";
import { NodeMeter, Gain, GainValue, MuteButton } from "./AudioControls";
import { NodeInspector } from "./NodeInspector";
import { NodeContextMenu, type NodeMenuAnchor } from "./NodeContextMenu";
import { GraphHistory } from "./history";
import { TextInput } from "../fields";
import { NodeRuntimeBadge } from "./RouteDiagnostics";

const db = (n: number) => n.toFixed(1) + " dB";
type NodeData = { node: AudioNodeConfig; deviceName: string; incoming: { edge: AudioEdgeConfig; source: AudioNodeConfig }[];
  changeNode: (id: string, patch: Partial<AudioNodeConfig>) => void;
  changeEdge: (id: string, patch: Partial<AudioEdgeConfig>) => void;
  menu: (id: string, x: number, y: number, trigger: HTMLElement, toggle?: boolean) => void;
  menuOpen: boolean; canEditRoute: boolean; active: boolean; offline: boolean; isRouting: boolean; compact: boolean; zoom: number };
type FlowNode = Node<NodeData, "audio">;
const AudioNode = memo(function AudioNode({ id, data, selected }: NodeProps<FlowNode>) {
  const { node: n, incoming } = data;
  const update = useUpdateNodeInternals();
  const handles = incoming.map(i => i.edge.id).join("|");
  useLayoutEffect(() => update(id), [id, handles, n.enabled, n.muted, data.compact, update]);
  const Icon = n.kind === "bus" ? SlidersHorizontal : n.kind === "capture" ? Mic : n.kind === "output" ? (data.deviceName.toLowerCase().includes("cable") ? Monitor : Headphones) : n.deviceId === "music" ? Music2 : Volume2;
  const title = nodeName(n);
  return <section className={`audio-node kind-${n.kind} ${data.compact ? "node-compact" : ""} ${selected ? "selected" : ""} ${!n.enabled ? "node-disabled" : n.muted ? "node-muted" : ""} ${data.offline ? "offline" : ""}`}
    style={{ "--node-zoom": Math.min(1, data.zoom) } as React.CSSProperties} aria-label={title} data-node-state={!n.enabled ? "disabled" : n.muted ? "muted" : "enabled"}>
    <header className="audio-node-heading">
      <div className="audio-node-icon"><Icon size={28} strokeWidth={1.8} /></div>
      <div className="audio-node-copy"><div className="node-role-row"><span className="node-role">{nodeRole(n)}</span>
        <span className={`node-state-badge ${n.enabled ? "state-enabled" : "state-disabled"}`}>{t(n.enabled ? "GraphNodeActive" : "GraphNodeInactive")}</span>
        <NodeRuntimeBadge node={n} active={data.active} running={data.isRouting} offline={data.offline} />
        {n.muted && <span className="node-state-badge state-muted"><VolumeOff size={12} />{t("GraphNodeMuted")}</span>}</div>
        <strong title={title}>{title}</strong>{n.kind !== "bus" && <small title={data.deviceName}>{data.deviceName || t("SelectDevice")}{n.kind === "loopback" && " · " + t("GraphLoopback")}</small>}</div>
      <div className="node-actions">{!data.compact && <button className={`node-power nodrag nopan ${n.enabled ? "is-on" : "is-off"}`} aria-label={title + " " + t("GraphActivation")}
        aria-pressed={n.enabled} disabled={!data.canEditRoute} title={t(data.canEditRoute ? n.enabled ? "GraphNodeDisable" : "GraphNodeEnable" : "GraphEnableLocked")}
        onClick={e => { e.stopPropagation(); data.changeNode(id, { enabled: !n.enabled }); }}><Power size={18} /></button>}
        <button className="node-more nodrag nopan" aria-label={t("GraphNodeActions")} title={t("GraphNodeActions")} aria-haspopup="menu" aria-expanded={data.menuOpen}
          onClick={e => { e.stopPropagation(); const rect = e.currentTarget.getBoundingClientRect(); data.menu(id, rect.left, rect.bottom + 6, e.currentTarget, true); }}><MoreHorizontal size={20} /></button></div>
    </header>
    {!n.enabled && <p className="node-disabled-note">{t("GraphNodeExcluded")}</p>}
    {n.kind === "bus" ? data.compact ? <div className="compact-bus-summary">
      {incoming.map(({ edge }, index) => <Handle key={edge.id} type="target" position={Position.Left} id={edge.id} style={{ top: ((index + 1) / (incoming.length + 1) * 100) + "%" }} />)}
      <Handle type="target" position={Position.Left} id="new" className="new-input-handle" />
      <span>{t("InputAudio")} · {incoming.length}</span><small>{db(n.gainDb)}</small><NodeMeter id={id} silent={!data.active || n.muted} />
    </div> : <>
      <div className="bus-inputs">{incoming.length === 0 ? <div className="empty-input">{t("NoInputs")}</div> : incoming.map(({ edge, source }) =>
        <div className="bus-input" key={edge.id}>
          <Handle type="target" position={Position.Left} id={edge.id} style={{ top: "50%" }} />
          <span className="send-name" title={nodeName(source)}>{nodeName(source)}</span>
          {data.compact ? <small className="compact-gain">{edge.muted ? t("GraphNodeMuted") : db(edge.gainDb)}</small> : <>
            <Gain value={edge.gainDb} label={nodeName(source) + " " + t("SendGain")} onChange={gainDb => data.changeEdge(edge.id, { gainDb })} />
            <GainValue value={edge.gainDb} label={nodeName(source) + " " + t("GraphGainValue")} onChange={gainDb => data.changeEdge(edge.id, { gainDb })} />
            <MuteButton label={nodeName(source) + " → " + title} muted={edge.muted} onClick={() => data.changeEdge(edge.id, { muted: !edge.muted })} /></>}
        </div>)}
        <Handle type="target" position={Position.Left} id="new" className="new-input-handle" title={t("GraphConnect")} />
      </div>
      <div className="bus-master"><div><span>{t("MasterGain")}</span><NodeMeter id={id} silent={!data.active || n.muted} /></div>
        {data.compact ? <small className="compact-gain">{db(n.gainDb)}</small> : <div><Gain value={n.gainDb} label={title + " " + t("MasterGain")} onChange={gainDb => data.changeNode(id, { gainDb })} /><GainValue value={n.gainDb} label={title + " " + t("GraphGainValue")} onChange={gainDb => data.changeNode(id, { gainDb })} /><MuteButton label={title} muted={n.muted} onClick={() => data.changeNode(id, { muted: !n.muted })} /></div>}</div>
    </> : <div className="device-node-meter"><NodeMeter id={id} silent={!data.active || n.muted} />{data.offline && <span className="offline-label">{t("GraphOffline")}</span>}</div>}
    {n.kind === "output" && <Handle type="target" position={Position.Left} id="in" />}
    {n.kind !== "output" && <Handle type="source" position={Position.Right} id="out" />}
  </section>;
});
const nodeTypes = { audio: AudioNode };
export function GraphWorkspace({ state, onPendingChange, initialNodeId, onSelectedBus }: { state: AppState; onPendingChange: (pending: boolean) => void; initialNodeId?: string | null; onSelectedBus: (id: string) => void }) {
  return <ReactFlowProvider><Workspace state={state} onPendingChange={onPendingChange} initialNodeId={initialNodeId} onSelectedBus={onSelectedBus} /></ReactFlowProvider>;
}
function RuntimeIssues({ project, inspect }: { project: GraphProject; inspect: (id: string) => void }) {
  const meter = useSyncExternalStore(subscribeMeter, readMeter);
  const errors = Object.entries(meter.errors ?? {});
  if (!errors.length) return null;
  return <div className="graph-runtime-errors notice" role="status"><strong>{t("GraphPartialError")}</strong>
    {errors.map(([id, message]) => { const node = project.nodes.find(n => n.id === id); return <div className="route-diagnostic" key={id}>
      <span>{node ? nodeName(node) : id}: {message}</span>{node && <button className="button small" onClick={() => inspect(id)}>{t("GraphOpenNodeSettings", nodeName(node))}</button>}
    </div>; })}</div>;
}
function Workspace({ state, onPendingChange, initialNodeId, onSelectedBus }: { state: AppState; onPendingChange: (pending: boolean) => void; initialNodeId?: string | null; onSelectedBus: (id: string) => void }) {
  const [project, setProject] = useState(state.graph);
  const draft = useRef(state.graph);
  const queued = useRef<GraphProject | null>(null);
  const sending = useRef(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const alive = useRef(true);
  const [error, setError] = useState("");
  const [selected, setSelected] = useState<string | null>(initialNodeId ?? null);
  const [selectedEdge, setSelectedEdge] = useState<string | null>(null);
  const [library, setLibrary] = useState(false);
  const [dock, setDock] = useState(false);
  const [inspector, setInspector] = useState(!!initialNodeId);
  const [menu, setMenu] = useState<NodeMenuAnchor | null>(null);
  const closeMenu = useCallback(() => setMenu(null), []);
  const [addOpen, setAddOpen] = useState(false);
  const [search, setSearch] = useState("");
  const history = useRef(new GraphHistory());
  const [historyVersion, setHistoryVersion] = useState(0);
  const [pinned, setPinned] = useState(false);
  const [dockHeight, setDockHeight] = useState(240);
  const [compactLayout, setCompactLayout] = useState(() => matchMedia("(max-height: 850px), (max-width: 1100px)").matches);
  const [mixId, setMixId] = useState(state.simpleBusId ?? state.graph.nodes.find(n => n.kind === "bus")?.id ?? "");
  const [flowNodes, setFlowNodes] = useState<FlowNode[]>([]);
  const flow = useReactFlow<FlowNode>();
  const { zoom } = useViewport();
  const nodesInitialized = useNodesInitialized(), initiallyFitted = useRef(false);
  const importInput = useRef<HTMLInputElement>(null);
  const canvas = useRef<HTMLDivElement>(null);
  const selectedRef = useRef(selected); selectedRef.current = selected;
  const revealNode = useCallback((id: string) => {
    requestAnimationFrame(() => requestAnimationFrame(() => {
      const node = flow.getNode(id), element = canvas.current; if (!node || !element) return;
      const rect = element.getBoundingClientRect(), viewport = flow.getViewport();
      const overlay = element.parentElement?.querySelector<HTMLElement>(".graph-inspector");
      const leftPanel = element.parentElement?.querySelector<HTMLElement>(".device-library");
      const left = leftPanel && getComputedStyle(leftPanel).position === "absolute" ? leftPanel.getBoundingClientRect().right - rect.left : 0;
      const right = overlay && getComputedStyle(overlay).position === "absolute" ? overlay.getBoundingClientRect().left - rect.left : rect.width;
      const w = (node.measured?.width ?? 340) * viewport.zoom, h = (node.measured?.height ?? 180) * viewport.zoom;
      const x = node.position.x * viewport.zoom + viewport.x, y = node.position.y * viewport.zoom + viewport.y;
      const desiredX = Math.max(left + 16, Math.min(x, Math.max(left + 16, right - w - 16)));
      const desiredY = Math.max(16, Math.min(y, Math.max(16, rect.height - h - 16)));
      if (desiredX !== x || desiredY !== y) void flow.setViewport({ ...viewport, x: viewport.x + desiredX - x, y: viewport.y + desiredY - y }, { duration: 0 });
    }));
  }, [flow]);
  const fitGraph = useCallback(async () => {
    await flow.fitView({ padding: .06, duration: 0, minZoom: .25, maxZoom: 1 });
    if (canvas.current && canvas.current.clientHeight > 650) {
      const viewport = flow.getViewport();
      const nodes = flow.getNodes();
      const top = Math.min(...nodes.map(n => n.position.y));
      await flow.setViewport({ ...viewport, y: 50 - (Number.isFinite(top) ? top : 0) * viewport.zoom }, { duration: 0 });
    }
  }, [flow]);
  useEffect(() => {
    if (nodesInitialized && !initiallyFitted.current) { initiallyFitted.current = true; void fitGraph(); }
  }, [nodesInitialized, fitGraph]);
  useEffect(() => {
    if (!canvas.current) return;
    let previous = canvas.current.getBoundingClientRect();
    const observer = new ResizeObserver(() => {
      if (!canvas.current) return;
      const rect = canvas.current.getBoundingClientRect(), viewport = flow.getViewport();
      if (flow.viewportInitialized) void flow.setViewport({ ...viewport, x: viewport.x + previous.left - rect.left, y: viewport.y + previous.top - rect.top }, { duration: 0 });
      previous = rect;
      if (selectedRef.current) revealNode(selectedRef.current);
    });
    observer.observe(canvas.current);
    return () => observer.disconnect();
  }, [flow, revealNode]);
  useEffect(() => {
    const query = matchMedia("(max-height: 850px), (max-width: 1100px)");
    const change = () => setCompactLayout(query.matches); query.addEventListener("change", change);
    return () => query.removeEventListener("change", change);
  }, []);
  useEffect(() => { if (compactLayout && dock && inspector) setDock(false); }, [compactLayout, dock, inspector]);
  useEffect(() => { if (compactLayout) setDockHeight(h => Math.min(h, 190)); }, [compactLayout]);
  useEffect(() => { if (selected && inspector) revealNode(selected); }, [selected, inspector, revealNode]);
  useEffect(() => { const finish = () => history.current.end(); document.addEventListener("pointerup", finish); document.addEventListener("pointercancel", finish);
    return () => { document.removeEventListener("pointerup", finish); document.removeEventListener("pointercancel", finish); }; }, []);
  useEffect(() => {
    if (!sending.current && !queued.current) { draft.current = state.graph; setProject(state.graph); }
  }, [state.graphRevision, state.graph]);
  useEffect(() => { alive.current = true; return () => { alive.current = false; if (timer.current) { clearTimeout(timer.current); void flush(); } }; }, []);
  async function flush() {
    timer.current = null;
    if (sending.current) return;
    sending.current = true;
    try {
      while (queued.current) {
        const next = queued.current; queued.current = null;
        await request("graph", { project: next, revision: readState()!.graphRevision });
      }
    } catch (e) {
      queued.current = null;
      history.current.clear(); setHistoryVersion(v => v + 1);
      const latest = readState()!.graph;
      draft.current = latest;
      if (alive.current) { setProject(latest); setError(e instanceof Error ? e.message : t("RequestFailed")); }
    } finally { sending.current = false; onPendingChange(false); }
  }
  const commit = useCallback((next: GraphProject, record = true) => {
    const validation = graphError(next, state);
    if (validation) { setError(validation); return; }
    setError("");
    const previous = draft.current;
    if (JSON.stringify(previous) === JSON.stringify(next)) return;
    if (record) { history.current.record(previous, next); setHistoryVersion(v => v + 1); }
    draft.current = next; setProject(next); queued.current = next;
    onPendingChange(true);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => void flush(), 75);
  }, [onPendingChange, state.sources, state.targets, state.microphones]);
  const changeNode = useCallback((id: string, patch: Partial<AudioNodeConfig>) => {
    commit({ ...draft.current, nodes: draft.current.nodes.map(n => n.id === id ? { ...n, ...patch } : n) });
  }, [commit]);
  const changeEdge = useCallback((id: string, patch: Partial<AudioEdgeConfig>) => {
    commit({ ...draft.current, edges: draft.current.edges.map(e => e.id === id ? { ...e, ...patch } : e) });
  }, [commit]);
  const details = useCallback((id: string) => {
    setSelected(id); setSelectedEdge(null); setInspector(true);
    const node = draft.current.nodes.find(n => n.id === id);
    if (node?.kind === "bus" && !pinned) setMixId(id);
    if (compactLayout) { setDock(false); setLibrary(false); setAddOpen(false); }
    revealNode(id);
  }, [pinned, compactLayout, revealNode]);
  const openMenu = useCallback((nodeId: string, x: number, y: number, trigger: HTMLElement, toggle = false) => {
    setSelected(nodeId); setSelectedEdge(null);
    if (draft.current.nodes.find(n => n.id === nodeId)?.kind === "bus" && !pinned) setMixId(nodeId);
    setMenu(previous => toggle && previous?.nodeId === nodeId ? null : { nodeId, x, y, trigger });
  }, [pinned]);
  const active = useMemo(() => activeNodeIds(project), [project]);
  const allNodes = useMemo<FlowNode[]>(() => project.nodes.map(n => ({
    id: n.id, type: "audio", position: n.position, selected: n.id === selected,
    data: { node: n, deviceName: deviceList(state, n.kind).find(d => d.id === n.deviceId)?.name ?? "",
      offline: n.kind !== "bus" && !deviceList(state, n.kind).some(d => d.id === n.deviceId),
      incoming: project.edges.filter(e => e.target === n.id).map(edge => ({ edge, source: project.nodes.find(s => s.id === edge.source)! })),
      changeNode, changeEdge, menu: openMenu, menuOpen: menu?.nodeId === n.id, canEditRoute: state.canEditRoute, active: active.has(n.id), isRouting: state.isRouting, compact: zoom < .8, zoom },
  })), [project, state.sources, state.targets, state.microphones, state.settings.language, state.canEditRoute, state.isRouting, selected, menu?.nodeId, active, zoom, changeNode, changeEdge, openMenu]);
  useEffect(() => setFlowNodes(allNodes), [allNodes]);
  const flowEdges = useMemo(() => project.edges.map(e => {
    const source = project.nodes.find(n => n.id === e.source)!, target = project.nodes.find(n => n.id === e.target)!;
    const disabled = !active.has(e.source) || !active.has(e.target), muted = e.muted || source.muted || target.muted;
    const color = disabled ? "#9ca8b9" : muted ? "#bd853b" : source.kind === "capture" ? "#17b8a5" : "#4c80f5";
    return { ...e, sourceHandle: "out", interactionWidth: 32 / Math.min(1, zoom),
    targetHandle: project.nodes.find(n => n.id === e.target)?.kind === "bus" ? e.id : "in",
    label: project.nodes.find(n => n.id === e.source)?.kind === "bus" ? undefined : db(e.gainDb),
    selected: selectedEdge === e.id, style: { stroke: color, strokeWidth: disabled ? 2 : 3, strokeDasharray: disabled ? "8 6" : muted ? "3 4" : undefined, opacity: disabled ? .65 : 1 },
    ariaLabel: nodeName(source) + " → " + nodeName(target) + (disabled ? " · " + t("GraphNodeInactive") : muted ? " · " + t("GraphNodeMuted") : ""),
    markerEnd: { type: MarkerType.ArrowClosed, color, width: 14, height: 14 }, labelStyle: { fill: "#314766", fontSize: 12 },
    labelBgStyle: { fill: "#fff", stroke: "#d8e5f5" }, labelBgPadding: [8, 5] as [number, number], labelBgBorderRadius: 6,
  }; }), [project, selectedEdge, active, zoom]);
  const [renderedEdges, setRenderedEdges] = useState(flowEdges);
  useEffect(() => {
    // Publish edges after new input handles have mounted and been measured.
    // This also covers replacing a handle without changing the input count.
    let nextFrame = 0;
    const frame = requestAnimationFrame(() => { nextFrame = requestAnimationFrame(() => setRenderedEdges(flowEdges)); });
    return () => { cancelAnimationFrame(frame); cancelAnimationFrame(nextFrame); };
  }, [flowEdges, allNodes]);
  const add = (kind: AudioNodeKind, deviceId: string | null, label: string) => {
    if (!state.canEditRoute) return;
    const n = createNode(kind, deviceId, label, project.nodes.filter(n => kind === "loopback" || kind === "capture" ? n.kind === "loopback" || n.kind === "capture" : n.kind === kind).length);
    commit({ ...draft.current, nodes: [...draft.current.nodes, n] }); setAddOpen(false); setLibrary(false); details(n.id);
    if (n.kind === "bus" && !pinned) setMixId(n.id);
    setTimeout(() => revealNode(n.id), 100);
  };
  const connect = (connection: Pick<Connection, "source" | "target">) => {
    if (!state.canEditRoute || !connection.source || !connection.target) return;
    const edge = { id: crypto.randomUUID(), source: connection.source, target: connection.target, gainDb: 0, muted: false };
    commit({ ...draft.current, edges: [...draft.current.edges, edge] });
  };
  const deleteNode = (id: string) => {
    if (!state.canEditRoute) return;
    commit({ ...draft.current, nodes: draft.current.nodes.filter(n => n.id !== id), edges: draft.current.edges.filter(e => e.source !== id && e.target !== id) });
    setSelected(null); setInspector(false);
  };
  const deleteEdge = (id: string) => { if (state.canEditRoute) { commit({ ...draft.current, edges: draft.current.edges.filter(e => e.id !== id) }); setSelectedEdge(null); } };
  const undo = () => { const previous = history.current.undo(draft.current, state.isRouting); if (previous) { commit(previous, false); setHistoryVersion(v => v + 1); } };
  const redo = () => { const next = history.current.redo(draft.current, state.isRouting); if (next) { commit(next, false); setHistoryVersion(v => v + 1); } };
  const canUndo = history.current.canUndo(project, state.isRouting), canRedo = history.current.canRedo(project, state.isRouting);
  void historyVersion;
  const addDevice = (kind: "loopback" | "capture" | "output", deviceId: string, busId: string) => {
    if (!state.canEditRoute) return;
    const current = draft.current;
    const existing = current.nodes.find(n => n.kind === kind && n.deviceId === deviceId);
    const node = existing ?? createNode(kind, deviceId, deviceList(state, kind).find(d => d.id === deviceId)?.name ?? "", current.nodes.filter(n => n.kind === kind).length);
    const edge = { id: crypto.randomUUID(), source: kind === "output" ? busId : node.id, target: kind === "output" ? node.id : busId, gainDb: 0, muted: false };
    commit({ ...current, nodes: existing ? current.nodes : [...current.nodes, node], edges: [...current.edges, edge] });
  };
  const arrange = () => {
    const counts = { source: 0, bus: 0, output: 0 };
    commit({ ...draft.current, nodes: draft.current.nodes.map(n => {
      const k = n.kind === "bus" ? "bus" : n.kind === "output" ? "output" : "source";
      return { ...n, position: { x: k === "bus" ? 560 : k === "output" ? 1145 : 0, y: counts[k]++ * (k === "bus" ? 360 : 180) } };
    }) });
    setTimeout(() => void fitGraph(), 100);
  };
  const chosen = project.nodes.find(n => n.id === selected);
  const edge = project.edges.find(e => e.id === selectedEdge);
  const bus = project.nodes.find(n => n.id === mixId && n.kind === "bus") ?? project.nodes.find(n => n.kind === "bus");
  useEffect(() => { const id = chosen?.kind === "bus" ? chosen.id : bus?.id; if (id) onSelectedBus(id); }, [chosen?.id, bus?.id, onSelectedBus]);
  const sends = project.edges.filter(e => e.target === bus?.id);
  function devices(kind: AudioNodeKind) {
    return deviceList(state, kind).filter(d => d.name.toLowerCase().includes(search.toLowerCase())).map(d => {
      const existing = project.nodes.find(n => n.kind === kind && n.deviceId === d.id);
      return <button key={d.id} className="device-library-row" disabled={!state.canEditRoute && !existing}
        title={d.name + " · " + d.summary + (existing ? " · " + t("GraphFindExisting") : "")}
        onClick={() => existing ? details(existing.id) : add(kind, d.id, d.name)}>
        {kind === "capture" ? <Mic size={19} /> : kind === "output" ? <Headphones size={19} /> : <Volume2 size={19} />}
        <span><strong>{d.name}</strong><small>{d.summary}</small><small>{t(d.isVirtualCableCandidate || d.returnPathGroup ? "GraphVirtualDevice" : "GraphPhysicalDevice")}{existing && " · " + t("GraphUsedDevice")}</small></span>{existing ? <Maximize2 size={15} /> : <Plus size={15} />}</button>;
    });
  }
  function toggleDock() { if (!dock && compactLayout) { setInspector(false); setLibrary(false); setAddOpen(false); } setDock(!dock); }
  function resizeDock(event: React.PointerEvent<HTMLDivElement>) {
    event.currentTarget.setPointerCapture(event.pointerId);
    const initialY = event.clientY, initialHeight = dockHeight;
    const move = (e: PointerEvent) => setDockHeight(Math.max(170, Math.min(340, initialHeight + initialY - e.clientY)));
    const stop = () => { document.removeEventListener("pointermove", move); document.removeEventListener("pointerup", stop); document.removeEventListener("pointercancel", stop); };
    document.addEventListener("pointermove", move); document.addEventListener("pointerup", stop); document.addEventListener("pointercancel", stop);
  }
  return <section className={`graph-workspace ${compactLayout ? "compact-layout" : ""}`} aria-label={t("AudioRouting")}
    onPointerDownCapture={e => { if ((e.target as HTMLElement).matches('input[type="range"]')) history.current.begin(); }} onKeyDown={e => {
    if (e.key === "ContextMenu" || e.shiftKey && e.key === "F10") {
      const node = (e.target as HTMLElement).closest<HTMLElement>(".react-flow__node");
      if (node?.dataset.id) { e.preventDefault(); const rect = node.getBoundingClientRect(); openMenu(node.dataset.id, rect.left + 30, rect.top + 35, node); } return;
    }
    if (e.key === "Escape") { closeMenu(); setSelected(null); setSelectedEdge(null); setInspector(false); setAddOpen(false); return; }
    if ((e.target as HTMLElement).closest("input, select, textarea")) return;
    if (e.ctrlKey && e.key.toLowerCase() === "z") { e.preventDefault(); e.shiftKey ? redo() : undo(); }
    if (e.ctrlKey && e.key.toLowerCase() === "y") { e.preventDefault(); redo(); }
    if (e.key === "Delete") { if (selectedEdge) deleteEdge(selectedEdge); else if (selected) deleteNode(selected); }
  }}>
    <div className="graph-toolbar">
      <div className="graph-project-name"><SlidersHorizontal size={18} /><TextInput aria-label={t("GraphProject")} value={project.name} maxLength={120} onChange={name => commit({ ...draft.current, name })} /></div>
      <div className="graph-tools">
        <button className={`button small ${library ? "selected" : ""}`} title={t("DeviceLibrary")} aria-label={t("DeviceLibrary")} aria-pressed={library} onClick={() => { if (!library && compactLayout) { setInspector(false); setDock(false); } setLibrary(!library); }}><PanelLeft size={17} /></button>
        <button className="button small" aria-label={t("GraphUndo")} title={t("GraphUndo")} disabled={!canUndo || state.busy} onClick={undo}><Undo2 size={16} /></button>
        <button className="button small" aria-label={t("GraphRedo")} title={t("GraphRedo")} disabled={!canRedo || state.busy} onClick={redo}><Redo2 size={16} /></button>
        <button className="button small" aria-expanded={addOpen} disabled={!state.canEditRoute} onClick={() => { if (!addOpen && compactLayout) { setInspector(false); setDock(false); } setAddOpen(!addOpen); }}><Plus size={17} />{t("NodeAdd")}</button>
        <button className="button small" aria-label={t("AutoArrange")} title={t("AutoArrange")} disabled={!state.canEditRoute} onClick={arrange}><LayoutGrid size={16} /><span>{t("AutoArrange")}</span></button>
        <button className="button small" aria-label={t("FitGraph")} title={t("FitGraph")} onClick={() => fitGraph()}><Maximize2 size={17} /><span>{t("FitGraph")}</span></button>
        <button className="button small" aria-label={t("GraphZoomIn")} title={t("GraphZoomIn")} onClick={() => flow.zoomIn({ duration: 0 })}><ZoomIn size={17} /></button>
        <button className="button small" aria-label={t("GraphZoomOut")} title={t("GraphZoomOut")} onClick={() => flow.zoomOut({ duration: 0 })}><ZoomOut size={17} /></button>
        <button className={`button small ${dock ? "selected" : ""}`} title={t("MixerDock")} aria-label={t("MixerDock")} aria-pressed={dock} onClick={toggleDock}><PanelBottom size={17} /></button>
        <button className="button small" title={t("GraphExport")} aria-label={t("GraphExport")} onClick={() => {
          const url = URL.createObjectURL(new Blob([JSON.stringify(draft.current, null, 2)], { type: "application/json" }));
          const a = document.createElement("a"); a.href = url; a.download = "stereo-mix-project.json"; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
        }}><ArrowDownToLine size={16} /></button>
        <button className="button small" title={t("GraphImport")} aria-label={t("GraphImport")} disabled={!state.canEditRoute} onClick={() => importInput.current?.click()}><ArrowUpFromLine size={16} /></button>
      </div>
    </div>
    <input hidden ref={importInput} type="file" accept=".json,application/json" onChange={async e => {
      const file = e.target.files?.[0]; e.target.value = ""; if (!file) return;
      try { if (file.size > 262144) throw new Error(t("GraphInvalid")); commit(JSON.parse(await file.text())); }
      catch { setError(t("GraphInvalid")); }
    }} />
    {error && <div className="graph-error notice" role="alert"><span>{error}</span><button className="icon-button" aria-label={t("Close")} onClick={() => setError("")}><X size={17} /></button></div>}
    <RuntimeIssues project={project} inspect={details} />
    <div className="graph-body">
      {(library || addOpen) && <aside className="device-library">
        <header><strong>{t("DeviceLibrary")}</strong><button className="icon-button" aria-label={t("Close")} onClick={() => { setLibrary(false); setAddOpen(false); }}><X size={18} /></button></header>
        <button className="button primary library-create-mix" disabled={!state.canEditRoute} onClick={() => add("bus", null, t("NewMix"))}><Plus size={18} />{t("GraphAddMixFirst")}</button>
        <input className="device-search" placeholder={t("GraphSearch")} aria-label={t("GraphSearch")} value={search} onChange={e => setSearch(e.target.value)} />
        <h3>{t("PlaybackAudio")}</h3>{devices("loopback")}<h3>{t("InputAudio")}</h3>{devices("capture")}
        <h3>{t("OutputAudio")}</h3>{devices("output")}
        <button className="button small" disabled={!state.canEditRoute} onClick={() => request("refresh").catch(e => setError(e.message))}>{t("Refresh")}</button>
      </aside>}
      <div ref={canvas} className="graph-canvas">
        <ReactFlow<FlowNode> nodes={flowNodes} edges={renderedEdges.filter(e => project.edges.some(current => current.id === e.id))} nodeTypes={nodeTypes}
          minZoom={.25} maxZoom={1.75} nodesConnectable={state.canEditRoute && zoom >= .8} deleteKeyCode={null} edgesFocusable
          onNodesChange={changes => setFlowNodes(nodes => applyNodeChanges(changes.filter(c => c.type !== "remove"), nodes))}
          onNodeDragStop={(_, node) => changeNode(node.id, { position: node.position })}
          onConnect={connect} isValidConnection={c => !graphError({ ...project, edges: [...project.edges, { id: "candidate", source: c.source, target: c.target, gainDb: 0, muted: false }] }, state)}
          onNodeClick={(event, node) => { if ((event.target as HTMLElement).closest("button,input,select,label")) return; closeMenu(); details(node.id); }}
          onNodeContextMenu={(event, node) => { if ((event.target as HTMLElement).closest("input,select,textarea")) return; event.preventDefault(); openMenu(node.id, event.clientX, event.clientY, event.currentTarget as HTMLElement); }}
          onMoveStart={closeMenu}
          onEdgeClick={(_, edge) => { closeMenu(); setSelectedEdge(edge.id); setSelected(null); setInspector(true); if (compactLayout) { setDock(false); setLibrary(false); setAddOpen(false); } }}
          onPaneClick={() => { closeMenu(); setSelected(null); setSelectedEdge(null); setInspector(false); }}>
          <Background variant={BackgroundVariant.Dots} gap={22} size={1.2} color="#ccddef" />
          <MiniMap<FlowNode> nodeColor={n => !n.data.node.enabled ? "#aab3bf" : n.data.node.muted ? "#edd5ac" : n.data.node.kind === "capture" ? "#bfeade" : n.data.node.kind === "output" ? "#dacffe" : "#c6d9fb"} pannable zoomable />
        </ReactFlow>
        {!project.nodes.length && <p className="graph-empty">{t("GraphEmpty")}</p>}
        <div className="graph-hint">{t(zoom < .8 ? "GraphCompactHint" : state.isRouting ? "GraphStopEdit" : "GraphConnectHint")}</div>
      </div>
      {inspector && (chosen || edge) && <NodeInspector key={chosen?.id ?? edge?.id} node={chosen} edge={edge} state={state} project={project}
        changeNode={changeNode} changeEdge={changeEdge} connect={connect} deleteNode={deleteNode} deleteEdge={deleteEdge}
        selectEdge={id => { setSelectedEdge(id); setSelected(null); }} selectNode={details} addDevice={addDevice} onClose={() => setInspector(false)} />}
    </div>
    {menu && project.nodes.find(n => n.id === menu.nodeId) && <NodeContextMenu node={project.nodes.find(n => n.id === menu.nodeId)!} anchor={menu}
      canEditRoute={state.canEditRoute} change={patch => changeNode(menu.nodeId, patch)} inspect={() => details(menu.nodeId)} remove={() => deleteNode(menu.nodeId)} close={closeMenu} />}
    {dock && bus && <section className="mixer-dock" style={{ height: dockHeight }}>
      <div className="mixer-resize" role="separator" tabIndex={0} aria-label={t("GraphDockHeight")} aria-orientation="horizontal" aria-valuemin={170} aria-valuemax={340} aria-valuenow={dockHeight}
        onPointerDown={resizeDock} onKeyDown={e => { if (e.key === "ArrowUp" || e.key === "ArrowDown") { e.preventDefault(); setDockHeight(h => Math.max(170, Math.min(340, h + (e.key === "ArrowUp" ? 20 : -20)))); } }} />
      <header><SlidersHorizontal size={21} /><strong>{t(pinned ? "GraphMixerPinned" : "GraphSelectedMix")}</strong>
      <select aria-label={t("MixBus")} value={bus.id} onChange={e => { setMixId(e.target.value); if (!pinned) { setSelected(e.target.value); setSelectedEdge(null); } }}>{project.nodes.filter(n => n.kind === "bus").map(n => <option key={n.id} value={n.id}>{nodeName(n)}</option>)}</select>
      <button className={`button small ${pinned ? "selected" : ""}`} aria-pressed={pinned} onClick={() => { if (pinned && chosen?.kind === "bus") setMixId(chosen.id); setPinned(!pinned); }}>{t(pinned ? "GraphFollowSelection" : "GraphPinMixer")}</button>
      <button className="icon-button" aria-label={t("Close")} onClick={() => setDock(false)}><X size={18} /></button></header>
      <div className="mixer-strips">{sends.map(e => { const source = project.nodes.find(n => n.id === e.source)!; return <div className="mixer-strip" key={e.id}>
        <strong title={nodeName(source)}>{nodeName(source)}</strong><output aria-live="off">{db(e.gainDb)}</output><div className="fader-track"><NodeMeter id={source.id} vertical /><Gain value={e.gainDb} label={nodeName(source) + " " + t("SendGain")} vertical onChange={gainDb => changeEdge(e.id, { gainDb })} /></div>
        <MuteButton label={nodeName(source) + " → " + nodeName(bus)} muted={e.muted} onClick={() => changeEdge(e.id, { muted: !e.muted })} /></div>; })}
        <div className="mixer-strip master-strip"><strong title={nodeName(bus) + " · " + t("MasterGain")}>{nodeName(bus)} · {t("MasterGain")}</strong><output aria-live="off">{db(bus.gainDb)}</output><div className="fader-track"><NodeMeter id={bus.id} silent={!active.has(bus.id) || bus.muted} vertical /><Gain value={bus.gainDb} label={nodeName(bus) + " · " + t("MasterGain")} vertical onChange={gainDb => changeNode(bus.id, { gainDb })} /></div><MuteButton label={nodeName(bus)} muted={bus.muted} onClick={() => changeNode(bus.id, { muted: !bus.muted })} /></div>
      </div>
    </section>}
  </section>;
}
