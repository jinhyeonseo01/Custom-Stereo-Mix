import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, useSyncExternalStore, memo } from "react";
import { ReactFlow, ReactFlowProvider, Handle, Position, Background, BackgroundVariant, MiniMap,
  applyNodeChanges, useReactFlow, useUpdateNodeInternals, MarkerType, type Node, type NodeProps, type Connection } from "@xyflow/react";
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

const db = (n: number) => n.toFixed(1) + " dB";
type NodeData = { node: AudioNodeConfig; deviceName: string; incoming: { edge: AudioEdgeConfig; source: AudioNodeConfig }[];
  changeNode: (id: string, patch: Partial<AudioNodeConfig>) => void;
  changeEdge: (id: string, patch: Partial<AudioEdgeConfig>) => void;
  menu: (id: string, x: number, y: number, trigger: HTMLElement, toggle?: boolean) => void;
  menuOpen: boolean; canEditRoute: boolean; active: boolean; offline: boolean };
type FlowNode = Node<NodeData, "audio">;
const AudioNode = memo(function AudioNode({ id, data, selected }: NodeProps<FlowNode>) {
  const { node: n, incoming } = data;
  const update = useUpdateNodeInternals();
  const handles = incoming.map(i => i.edge.id).join("|");
  useLayoutEffect(() => update(id), [id, handles, n.enabled, n.muted, update]);
  const Icon = n.kind === "bus" ? SlidersHorizontal : n.kind === "capture" ? Mic : n.kind === "output" ? (data.deviceName.toLowerCase().includes("cable") ? Monitor : Headphones) : n.deviceId === "music" ? Music2 : Volume2;
  const title = nodeName(n);
  return <section className={`audio-node kind-${n.kind} ${selected ? "selected" : ""} ${!n.enabled ? "node-disabled" : n.muted ? "node-muted" : ""} ${data.offline ? "offline" : ""}`}
    aria-label={title} data-node-state={!n.enabled ? "disabled" : n.muted ? "muted" : "enabled"}>
    <header className="audio-node-heading">
      <div className="audio-node-icon"><Icon size={28} strokeWidth={1.8} /></div>
      <div className="audio-node-copy"><div className="node-role-row"><span className="node-role">{nodeRole(n)}</span>
        <span className={`node-state-badge ${n.enabled ? "state-enabled" : "state-disabled"}`}>{t(n.enabled ? "GraphNodeActive" : "GraphNodeInactive")}</span>
        {n.muted && <span className="node-state-badge state-muted"><VolumeOff size={12} />{t("GraphNodeMuted")}</span>}</div>
        <strong title={title}>{title}</strong>{n.kind !== "bus" && <small title={data.deviceName}>{data.deviceName || t("SelectDevice")}{n.kind === "loopback" && " · " + t("GraphLoopback")}</small>}</div>
      <div className="node-actions"><button className={`node-power nodrag nopan ${n.enabled ? "is-on" : "is-off"}`} aria-label={title + " " + t("GraphActivation")}
        aria-pressed={n.enabled} disabled={!data.canEditRoute} title={t(data.canEditRoute ? n.enabled ? "GraphNodeDisable" : "GraphNodeEnable" : "GraphEnableLocked")}
        onClick={e => { e.stopPropagation(); data.changeNode(id, { enabled: !n.enabled }); }}><Power size={18} /></button>
        <button className="node-more nodrag nopan" aria-label={t("GraphNodeActions")} title={t("GraphNodeActions")} aria-haspopup="menu" aria-expanded={data.menuOpen}
          onClick={e => { e.stopPropagation(); const rect = e.currentTarget.getBoundingClientRect(); data.menu(id, rect.left, rect.bottom + 6, e.currentTarget, true); }}><MoreHorizontal size={20} /></button></div>
    </header>
    {!n.enabled && <p className="node-disabled-note">{t("GraphNodeExcluded")}</p>}
    {n.kind === "bus" ? <>
      <div className="bus-inputs">{incoming.length === 0 ? <div className="empty-input">{t("NoInputs")}</div> : incoming.map(({ edge, source }) =>
        <div className="bus-input" key={edge.id}>
          <Handle type="target" position={Position.Left} id={edge.id} style={{ top: "50%" }} />
          <span className="send-name" title={nodeName(source)}>{nodeName(source)}</span>
          <Gain value={edge.gainDb} label={nodeName(source) + " " + t("SendGain")} onChange={gainDb => data.changeEdge(edge.id, { gainDb })} />
          <GainValue value={edge.gainDb} label={nodeName(source) + " " + t("GraphGainValue")} onChange={gainDb => data.changeEdge(edge.id, { gainDb })} /><MuteButton muted={edge.muted} onClick={() => data.changeEdge(edge.id, { muted: !edge.muted })} />
        </div>)}
        <Handle type="target" position={Position.Left} id="new" className="new-input-handle" title={t("GraphConnect")} />
      </div>
      <div className="bus-master"><div><span>{t("MasterGain")}</span><NodeMeter id={id} silent={!data.active || n.muted} /></div>
        <div><Gain value={n.gainDb} label={title + " " + t("MasterGain")} onChange={gainDb => data.changeNode(id, { gainDb })} /><GainValue value={n.gainDb} label={title + " " + t("GraphGainValue")} onChange={gainDb => data.changeNode(id, { gainDb })} /><MuteButton muted={n.muted} onClick={() => data.changeNode(id, { muted: !n.muted })} /></div></div>
    </> : <div className="device-node-meter"><NodeMeter id={id} silent={!data.active || n.muted} />{data.offline && <span className="offline-label">{t("GraphOffline")}</span>}</div>}
    {n.kind === "output" && <Handle type="target" position={Position.Left} id="in" />}
    {n.kind !== "output" && <Handle type="source" position={Position.Right} id="out" />}
  </section>;
});
const nodeTypes = { audio: AudioNode };
export function GraphWorkspace({ state, onPendingChange }: { state: AppState; onPendingChange: (pending: boolean) => void }) {
  return <ReactFlowProvider><Workspace state={state} onPendingChange={onPendingChange} /></ReactFlowProvider>;
}
function RuntimeIssues({ project }: { project: GraphProject }) {
  const meter = useSyncExternalStore(subscribeMeter, readMeter);
  const errors = Object.entries(meter.errors ?? {});
  if (!errors.length) return null;
  return <div className="graph-runtime-errors notice" role="status"><strong>{t("GraphPartialError")}</strong>
    <span>{errors.map(([id, message]) => (nodeName(project.nodes.find(n => n.id === id) ?? { kind: "output", label: id } as AudioNodeConfig)) + ": " + message).join(" · ")}</span></div>;
}
function Workspace({ state, onPendingChange }: { state: AppState; onPendingChange: (pending: boolean) => void }) {
  const [project, setProject] = useState(state.graph);
  const draft = useRef(state.graph);
  const queued = useRef<GraphProject | null>(null);
  const sending = useRef(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const alive = useRef(true);
  const [error, setError] = useState("");
  const [selected, setSelected] = useState<string | null>(null);
  const [selectedEdge, setSelectedEdge] = useState<string | null>(null);
  const [library, setLibrary] = useState(false);
  const [dock, setDock] = useState(false);
  const [inspector, setInspector] = useState(false);
  const [menu, setMenu] = useState<NodeMenuAnchor | null>(null);
  const closeMenu = useCallback(() => setMenu(null), []);
  const [addOpen, setAddOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [history, setHistory] = useState<GraphProject[]>([]);
  const [future, setFuture] = useState<GraphProject[]>([]);
  const [mixId, setMixId] = useState(state.graph.nodes.find(n => n.kind === "bus")?.id ?? "");
  const [flowNodes, setFlowNodes] = useState<FlowNode[]>([]);
  const flow = useReactFlow<FlowNode>();
  const importInput = useRef<HTMLInputElement>(null);
  const canvas = useRef<HTMLDivElement>(null);
  const fitGraph = useCallback(async () => {
    await flow.fitView({ padding: .025, duration: 0, maxZoom: 1 });
    if (canvas.current && canvas.current.clientHeight > 650) {
      const viewport = flow.getViewport();
      const nodes = flow.getNodes();
      const top = Math.min(...nodes.map(n => n.position.y));
      await flow.setViewport({ ...viewport, y: 50 - (Number.isFinite(top) ? top : 0) * viewport.zoom }, { duration: 0 });
    }
  }, [flow]);
  useEffect(() => {
    if (!canvas.current) return;
    let resizeTimer: ReturnType<typeof setTimeout>;
    const observer = new ResizeObserver(() => {
      clearTimeout(resizeTimer);
      resizeTimer = setTimeout(() => void fitGraph(), 120);
    });
    observer.observe(canvas.current);
    return () => { observer.disconnect(); clearTimeout(resizeTimer); };
  }, [fitGraph]);
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
    if (record) { setHistory(h => [...h.slice(-49), previous]); setFuture([]); }
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
  const details = useCallback((id: string) => { setSelected(id); setSelectedEdge(null); setInspector(true); }, []);
  const openMenu = useCallback((nodeId: string, x: number, y: number, trigger: HTMLElement, toggle = false) => {
    setSelected(nodeId); setSelectedEdge(null);
    if (draft.current.nodes.find(n => n.id === nodeId)?.kind === "bus") setMixId(nodeId);
    setMenu(previous => toggle && previous?.nodeId === nodeId ? null : { nodeId, x, y, trigger });
  }, []);
  const active = useMemo(() => activeNodeIds(project), [project]);
  const allNodes = useMemo<FlowNode[]>(() => project.nodes.map(n => ({
    id: n.id, type: "audio", position: n.position, selected: n.id === selected,
    data: { node: n, deviceName: deviceList(state, n.kind).find(d => d.id === n.deviceId)?.name ?? "",
      offline: n.kind !== "bus" && !deviceList(state, n.kind).some(d => d.id === n.deviceId),
      incoming: project.edges.filter(e => e.target === n.id).map(edge => ({ edge, source: project.nodes.find(s => s.id === edge.source)! })),
      changeNode, changeEdge, menu: openMenu, menuOpen: menu?.nodeId === n.id, canEditRoute: state.canEditRoute, active: active.has(n.id) },
  })), [project, state.sources, state.targets, state.microphones, state.settings.language, state.canEditRoute, selected, menu?.nodeId, active, changeNode, changeEdge, openMenu]);
  useEffect(() => setFlowNodes(allNodes), [allNodes]);
  const flowEdges = useMemo(() => project.edges.map(e => {
    const source = project.nodes.find(n => n.id === e.source)!, target = project.nodes.find(n => n.id === e.target)!;
    const disabled = !active.has(e.source) || !active.has(e.target), muted = e.muted || source.muted || target.muted;
    const color = disabled ? "#9ca8b9" : muted ? "#bd853b" : source.kind === "capture" ? "#17b8a5" : "#4c80f5";
    return { ...e, sourceHandle: "out",
    targetHandle: project.nodes.find(n => n.id === e.target)?.kind === "bus" ? e.id : "in",
    label: project.nodes.find(n => n.id === e.source)?.kind === "bus" ? undefined : db(e.gainDb),
    selected: selectedEdge === e.id, style: { stroke: color, strokeWidth: disabled ? 2 : 3, strokeDasharray: disabled ? "8 6" : muted ? "3 4" : undefined, opacity: disabled ? .65 : 1 },
    ariaLabel: nodeName(source) + " → " + nodeName(target) + (disabled ? " · " + t("GraphNodeInactive") : muted ? " · " + t("GraphNodeMuted") : ""),
    markerEnd: { type: MarkerType.ArrowClosed, color, width: 14, height: 14 }, labelStyle: { fill: "#314766", fontSize: 12 },
    labelBgStyle: { fill: "#fff", stroke: "#d8e5f5" }, labelBgPadding: [8, 5] as [number, number], labelBgBorderRadius: 6,
  }; }), [project, selectedEdge, active]);
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
    commit({ ...draft.current, nodes: [...draft.current.nodes, n] }); setAddOpen(false); setSelected(n.id);
    setTimeout(() => void flow.fitView({ padding: .05, duration: 0 }), 100);
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
  const undo = () => { const last = history.at(-1); const current = draft.current; if (last && state.canEditRoute) { setFuture(f => [...f, current]); setHistory(h => h.slice(0, -1)); commit(last, false); } };
  const redo = () => { const next = future.at(-1); const current = draft.current; if (next && state.canEditRoute) { setHistory(h => [...h, current]); setFuture(f => f.slice(0, -1)); commit(next, false); } };
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
  const sends = project.edges.filter(e => e.target === bus?.id);
  function devices(kind: AudioNodeKind) {
    return deviceList(state, kind).filter(d => d.name.toLowerCase().includes(search.toLowerCase())).map(d =>
      <button key={d.id} className="device-library-row" disabled={!state.canEditRoute || kind === "output" && project.nodes.some(n => n.kind === "output" && n.deviceId === d.id)}
        onClick={() => add(kind, d.id, d.name)}>
        {kind === "capture" ? <Mic size={19} /> : kind === "output" ? <Headphones size={19} /> : <Volume2 size={19} />}
        <span><strong>{d.name}</strong><small>{d.summary}</small></span><Plus size={15} /></button>);
  }
  return <section className="graph-workspace" aria-label={t("AudioRouting")} onKeyDown={e => {
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
      <div className="graph-project-name"><SlidersHorizontal size={18} /><input aria-label={t("GraphProject")} value={project.name} maxLength={120} onChange={e => commit({ ...draft.current, name: e.target.value })} /></div>
      <div className="graph-tools">
        <button className={`button small ${library ? "selected" : ""}`} title={t("DeviceLibrary")} aria-label={t("DeviceLibrary")} aria-pressed={library} onClick={() => setLibrary(!library)}><PanelLeft size={17} /></button>
        <button className="button small" aria-label={t("GraphUndo")} title={t("GraphUndo")} disabled={!history.length || !state.canEditRoute} onClick={undo}><Undo2 size={16} /></button>
        <button className="button small" aria-label={t("GraphRedo")} title={t("GraphRedo")} disabled={!future.length || !state.canEditRoute} onClick={redo}><Redo2 size={16} /></button>
        <button className="button small" aria-expanded={addOpen} disabled={!state.canEditRoute} onClick={() => setAddOpen(!addOpen)}><Plus size={17} />{t("NodeAdd")}</button>
        <button className="button small" aria-label={t("AutoArrange")} title={t("AutoArrange")} disabled={!state.canEditRoute} onClick={arrange}><LayoutGrid size={16} /><span>{t("AutoArrange")}</span></button>
        <button className="button small" aria-label={t("FitGraph")} title={t("FitGraph")} onClick={() => fitGraph()}><Maximize2 size={17} /><span>{t("FitGraph")}</span></button>
        <button className="button small" aria-label={t("GraphZoomIn")} title={t("GraphZoomIn")} onClick={() => flow.zoomIn({ duration: 0 })}><ZoomIn size={17} /></button>
        <button className="button small" aria-label={t("GraphZoomOut")} title={t("GraphZoomOut")} onClick={() => flow.zoomOut({ duration: 0 })}><ZoomOut size={17} /></button>
        <button className={`button small ${dock ? "selected" : ""}`} title={t("MixerDock")} aria-label={t("MixerDock")} aria-pressed={dock} onClick={() => setDock(!dock)}><PanelBottom size={17} /></button>
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
    <RuntimeIssues project={project} />
    <div className="graph-body">
      {(library || addOpen) && <aside className="device-library">
        <header><strong>{t("DeviceLibrary")}</strong><button className="icon-button" aria-label={t("Close")} onClick={() => { setLibrary(false); setAddOpen(false); }}><X size={18} /></button></header>
        <input className="device-search" placeholder={t("GraphSearch")} aria-label={t("GraphSearch")} value={search} onChange={e => setSearch(e.target.value)} />
        <h3>{t("PlaybackAudio")}</h3>{devices("loopback")}<h3>{t("InputAudio")}</h3>{devices("capture")}
        <h3>{t("MixBus")}</h3><button className="device-library-row" disabled={!state.canEditRoute} onClick={() => add("bus", null, t("NewMix"))}><SlidersHorizontal size={20} /><span>{t("NewMix")}</span><Plus size={16} /></button>
        <h3>{t("OutputAudio")}</h3>{devices("output")}
        <button className="button small" disabled={!state.canEditRoute} onClick={() => request("refresh").catch(e => setError(e.message))}>{t("Refresh")}</button>
      </aside>}
      <div ref={canvas} className="graph-canvas">
        <ReactFlow<FlowNode> nodes={flowNodes} edges={renderedEdges.filter(e => project.edges.some(current => current.id === e.id))} nodeTypes={nodeTypes} fitView fitViewOptions={{ padding: .09 }}
          minZoom={.25} maxZoom={1.75} nodesConnectable={state.canEditRoute} deleteKeyCode={null}
          onNodesChange={changes => setFlowNodes(nodes => applyNodeChanges(changes.filter(c => c.type !== "remove"), nodes))}
          onNodeDragStop={(_, node) => changeNode(node.id, { position: node.position })}
          onConnect={connect} isValidConnection={c => !graphError({ ...project, edges: [...project.edges, { id: "candidate", source: c.source, target: c.target, gainDb: 0, muted: false }] }, state)}
          onNodeClick={(event, node) => { if ((event.target as HTMLElement).closest("button,input,select,label")) return; closeMenu(); details(node.id); if (node.data.node.kind === "bus") setMixId(node.id); }}
          onNodeContextMenu={(event, node) => { if ((event.target as HTMLElement).closest("input,select,textarea")) return; event.preventDefault(); openMenu(node.id, event.clientX, event.clientY, event.currentTarget as HTMLElement); }}
          onMoveStart={closeMenu}
          onEdgeClick={(_, edge) => { closeMenu(); setSelectedEdge(edge.id); setSelected(null); setInspector(true); }}
          onPaneClick={() => { closeMenu(); setSelected(null); setSelectedEdge(null); setInspector(false); }}>
          <Background variant={BackgroundVariant.Dots} gap={22} size={1.2} color="#ccddef" />
          <MiniMap<FlowNode> nodeColor={n => !n.data.node.enabled ? "#aab3bf" : n.data.node.muted ? "#edd5ac" : n.data.node.kind === "capture" ? "#bfeade" : n.data.node.kind === "output" ? "#dacffe" : "#c6d9fb"} pannable zoomable />
        </ReactFlow>
        {!project.nodes.length && <p className="graph-empty">{t("GraphEmpty")}</p>}
        <div className="graph-hint">{t(state.isRouting ? "GraphStopEdit" : "GraphConnectHint")}</div>
      </div>
      {inspector && (chosen || edge) && <NodeInspector key={chosen?.id ?? edge?.id} node={chosen} edge={edge} state={state} project={project}
        changeNode={changeNode} changeEdge={changeEdge} connect={connect} deleteNode={deleteNode} deleteEdge={deleteEdge}
        selectEdge={id => { setSelectedEdge(id); setSelected(null); }} onClose={() => setInspector(false)} />}
    </div>
    {menu && project.nodes.find(n => n.id === menu.nodeId) && <NodeContextMenu node={project.nodes.find(n => n.id === menu.nodeId)!} anchor={menu}
      canEditRoute={state.canEditRoute} change={patch => changeNode(menu.nodeId, patch)} inspect={() => details(menu.nodeId)} remove={() => deleteNode(menu.nodeId)} close={closeMenu} />}
    {dock && bus && <section className="mixer-dock"><header><SlidersHorizontal size={21} /><strong>{t("GraphSelectedMix")}</strong>
      <select aria-label={t("MixBus")} value={bus.id} onChange={e => setMixId(e.target.value)}>{project.nodes.filter(n => n.kind === "bus").map(n => <option key={n.id} value={n.id}>{nodeName(n)}</option>)}</select>
      <button className="icon-button" aria-label={t("Close")} onClick={() => setDock(false)}><X size={18} /></button></header>
      <div className="mixer-strips">{sends.map(e => { const source = project.nodes.find(n => n.id === e.source)!; return <div className="mixer-strip" key={e.id}>
        <strong>{nodeName(source)}</strong><output>{db(e.gainDb)}</output><div className="fader-track"><NodeMeter id={source.id} vertical /><Gain value={e.gainDb} label={nodeName(source) + " " + t("SendGain")} vertical onChange={gainDb => changeEdge(e.id, { gainDb })} /></div>
        <MuteButton muted={e.muted} onClick={() => changeEdge(e.id, { muted: !e.muted })} /></div>; })}
        <div className="mixer-strip master-strip"><strong>{t("MasterGain")}</strong><output>{db(bus.gainDb)}</output><div className="fader-track"><NodeMeter id={bus.id} vertical /><Gain value={bus.gainDb} label={t("MasterGain")} vertical onChange={gainDb => changeNode(bus.id, { gainDb })} /></div><MuteButton muted={bus.muted} onClick={() => changeNode(bus.id, { muted: !bus.muted })} /></div>
      </div>
    </section>}
  </section>;
}
