import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, useSyncExternalStore, memo } from "react";
import { ReactFlow, ReactFlowProvider, Handle, Position, Background, BackgroundVariant, MiniMap,
  applyNodeChanges, useReactFlow, useUpdateNodeInternals, MarkerType, type Node, type NodeProps, type Connection } from "@xyflow/react";
import { SlidersHorizontal, Headphones, Music2, Monitor, Mic, Volume2, Plus, Maximize2, PanelLeft, PanelBottom, X, MoreHorizontal, Undo2, Redo2, ArrowDownToLine, ArrowUpFromLine, Settings2, LayoutGrid, ZoomIn, ZoomOut } from "../icons";
import "@xyflow/react/dist/style.css";
import "./advanced.css";
import { t } from "../i18n";
import { request, readState, readMeter, subscribeMeter } from "../bridge";
import type { AppState, AudioNodeConfig, AudioEdgeConfig, AudioNodeKind, GraphProject } from "../types";
import { createNode, deviceList, graphError, nodeName } from "./graph";
import { NodeMeter, Gain, GainValue, MuteButton } from "./AudioControls";

const db = (n: number) => n.toFixed(1) + " dB";
type NodeData = { node: AudioNodeConfig; deviceName: string; incoming: { edge: AudioEdgeConfig; source: AudioNodeConfig }[];
  changeNode: (id: string, patch: Partial<AudioNodeConfig>) => void;
  changeEdge: (id: string, patch: Partial<AudioEdgeConfig>) => void; details: (id: string) => void; offline: boolean };
type FlowNode = Node<NodeData, "audio">;
const AudioNode = memo(function AudioNode({ id, data, selected }: NodeProps<FlowNode>) {
  const { node: n, incoming } = data;
  const update = useUpdateNodeInternals();
  const handles = incoming.map(i => i.edge.id).join("|");
  useLayoutEffect(() => update(id), [id, handles, update]);
  const Icon = n.kind === "bus" ? SlidersHorizontal : n.kind === "capture" ? Mic : n.kind === "output" ? (data.deviceName.toLowerCase().includes("cable") ? Monitor : Headphones) : n.deviceId === "music" ? Music2 : Volume2;
  const title = nodeName(n);
  return <section className={`audio-node kind-${n.kind} ${selected ? "selected" : ""} ${n.muted || !n.enabled ? "muted" : ""} ${data.offline ? "offline" : ""}`} aria-label={title}>
    <header className="audio-node-heading">
      <div className="audio-node-icon"><Icon size={28} strokeWidth={1.8} /></div>
      <div className="audio-node-copy"><span className="node-role">{t(n.kind === "bus" ? "MixBus" : n.kind === "capture" ? "InputAudio" : n.kind === "output" ? "OutputAudio" : "PlaybackAudio")}</span>
        <strong>{title}</strong>{n.kind !== "bus" && <small title={data.deviceName}>{data.deviceName || t("SelectDevice")}{n.kind === "loopback" && " · " + t("GraphLoopback")}</small>}</div>
      <button className="node-more nodrag nopan" aria-label={t("Inspector")} title={t("Inspector")} onClick={() => data.details(id)}><MoreHorizontal size={20} /></button>
    </header>
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
      <div className="bus-master"><div><span>{t("MasterGain")}</span><NodeMeter id={id} /></div>
        <div><Gain value={n.gainDb} label={title + " " + t("MasterGain")} onChange={gainDb => data.changeNode(id, { gainDb })} /><GainValue value={n.gainDb} label={title + " " + t("GraphGainValue")} onChange={gainDb => data.changeNode(id, { gainDb })} /><MuteButton muted={n.muted} onClick={() => data.changeNode(id, { muted: !n.muted })} /></div></div>
    </> : <div className="device-node-meter"><NodeMeter id={id} />{data.offline && <span className="offline-label">{t("GraphOffline")}</span>}</div>}
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
  const allNodes = useMemo<FlowNode[]>(() => project.nodes.map(n => ({
    id: n.id, type: "audio", position: n.position, selected: n.id === selected,
    data: { node: n, deviceName: deviceList(state, n.kind).find(d => d.id === n.deviceId)?.name ?? "",
      offline: n.kind !== "bus" && !deviceList(state, n.kind).some(d => d.id === n.deviceId),
      incoming: project.edges.filter(e => e.target === n.id).map(edge => ({ edge, source: project.nodes.find(s => s.id === edge.source)! })),
      changeNode, changeEdge, details },
  })), [project, state.sources, state.targets, state.microphones, state.settings.language, selected, changeNode, changeEdge, details]);
  useEffect(() => setFlowNodes(allNodes), [allNodes]);
  const flowEdges = useMemo(() => project.edges.map(e => ({ ...e, sourceHandle: "out",
    targetHandle: project.nodes.find(n => n.id === e.target)?.kind === "bus" ? e.id : "in",
    label: project.nodes.find(n => n.id === e.source)?.kind === "bus" ? undefined : db(e.gainDb),
    selected: selectedEdge === e.id, style: { stroke: e.muted ? "#a6b1c3" : project.nodes.find(n => n.id === e.source)?.kind === "capture" ? "#17b8a5" : "#4c80f5", strokeWidth: 3, strokeDasharray: e.muted ? "6 5" : undefined },
    markerEnd: { type: MarkerType.ArrowClosed, width: 14, height: 14 }, labelStyle: { fill: "#314766", fontSize: 12 },
    labelBgStyle: { fill: "#fff", stroke: "#d8e5f5" }, labelBgPadding: [8, 5] as [number, number], labelBgBorderRadius: 6,
  })), [project, selectedEdge]);
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
  function devicePicker(n: AudioNodeConfig) {
    return <label className="graph-field"><span>{t("SelectDevice")}</span><select value={n.deviceId ?? ""} disabled={!state.canEditRoute}
      onChange={e => changeNode(n.id, { deviceId: e.target.value })}><option value="">{t("SelectDevice")}</option>
      {!deviceList(state, n.kind).some(d => d.id === n.deviceId) && n.deviceId && <option value={n.deviceId}>{t("GraphOffline")}</option>}
      {deviceList(state, n.kind).map(d => <option key={d.id} value={d.id}>{d.name}</option>)}</select></label>;
  }
  function panelContent(n: AudioNodeConfig) {
    return <>
      <label className="graph-field"><span>{t("NodeName")}</span><input value={n.label} maxLength={160} placeholder={nodeName(n)} onChange={e => changeNode(n.id, { label: e.target.value })} /></label>
      {n.kind !== "bus" && devicePicker(n)}
      <div className="panel-gain"><span>{t(n.kind === "bus" ? "MasterGain" : "OutputGain")}</span><Gain value={n.gainDb} label={t("OutputGain")} onChange={gainDb => changeNode(n.id, { gainDb })} /><output>{db(n.gainDb)}</output></div>
      <label className="graph-check"><input type="checkbox" checked={n.muted} onChange={e => changeNode(n.id, { muted: e.target.checked })} />{t("Mute")}</label>
      {n.kind === "bus" || n.kind === "output" ? <>
        <label className="graph-check"><input type="checkbox" checked={n.limiterEnabled} onChange={e => changeNode(n.id, { limiterEnabled: e.target.checked })} />{t("Limiter")}</label>
        <label className="graph-check"><input type="checkbox" checked={n.compressorEnabled} onChange={e => changeNode(n.id, { compressorEnabled: e.target.checked })} />{t("ReduceLoudSounds")}</label>
        {n.compressorEnabled && <div className="compressor-controls">
          <label>{t("CompressorThreshold")}<input type="number" min={-60} max={0} value={n.compressorThresholdDb} onChange={e => changeNode(n.id, { compressorThresholdDb: Number(e.target.value) })} /></label>
          <label>{t("CompressorRatio")}<input type="number" min={1} max={20} value={n.compressorRatio} onChange={e => changeNode(n.id, { compressorRatio: Number(e.target.value) })} /></label></div>}
      </> : null}
      <label className="graph-check"><input type="checkbox" checked={n.enabled} disabled={!state.canEditRoute} onChange={e => changeNode(n.id, { enabled: e.target.checked })} />{t("GraphEnabled")}</label>
      {n.kind !== "output" && <label className="graph-field"><span>{t("GraphDestination")}</span><select value="" disabled={!state.canEditRoute} onChange={e => connect({ source: n.id, target: e.target.value })}>
        <option value="">{t("GraphConnect")}</option>{project.nodes.filter(d => d.kind === (n.kind === "bus" ? "output" : "bus") && !project.edges.some(e => e.source === n.id && e.target === d.id)).map(d => <option key={d.id} value={d.id}>{nodeName(d)}</option>)}</select></label>}
      <button className="button danger small" disabled={!state.canEditRoute} onClick={() => deleteNode(n.id)}>{t("NodeDelete")}</button>
    </>;
  }
  return <section className="graph-workspace" aria-label={t("AudioRouting")} onKeyDown={e => {
    if ((e.target as HTMLElement).closest("input, select, textarea")) return;
    if (e.ctrlKey && e.key.toLowerCase() === "z") { e.preventDefault(); e.shiftKey ? redo() : undo(); }
    if (e.ctrlKey && e.key.toLowerCase() === "y") { e.preventDefault(); redo(); }
    if (e.key === "Delete") { if (selectedEdge) deleteEdge(selectedEdge); else if (selected) deleteNode(selected); }
    if (e.key === "Escape") { setSelected(null); setSelectedEdge(null); setInspector(false); setAddOpen(false); }
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
      <div ref={canvas} className={`graph-canvas ${chosen?.kind === "output" && !inspector ? "has-output-popover" : ""}`}>
        <ReactFlow<FlowNode> nodes={flowNodes} edges={renderedEdges.filter(e => project.edges.some(current => current.id === e.id))} nodeTypes={nodeTypes} fitView fitViewOptions={{ padding: .09 }}
          minZoom={.25} maxZoom={1.75} nodesConnectable={state.canEditRoute} deleteKeyCode={null}
          onNodesChange={changes => setFlowNodes(nodes => applyNodeChanges(changes.filter(c => c.type !== "remove"), nodes))}
          onNodeDragStop={(_, node) => changeNode(node.id, { position: node.position })}
          onConnect={connect} isValidConnection={c => !graphError({ ...project, edges: [...project.edges, { id: "candidate", source: c.source, target: c.target, gainDb: 0, muted: false }] }, state)}
          onNodeClick={(_, node) => { setSelected(node.id); setSelectedEdge(null); if (node.data.node.kind === "bus") setMixId(node.id); }}
          onEdgeClick={(_, edge) => { setSelectedEdge(edge.id); setSelected(null); setInspector(true); }}
          onPaneClick={() => { setSelected(null); setSelectedEdge(null); setInspector(false); }}>
          <Background variant={BackgroundVariant.Dots} gap={22} size={1.2} color="#ccddef" />
          <MiniMap<FlowNode> nodeColor={n => n.data.node.kind === "capture" ? "#bfeade" : n.data.node.kind === "output" ? "#dacffe" : "#c6d9fb"} pannable zoomable />
        </ReactFlow>
        {!project.nodes.length && <p className="graph-empty">{t("GraphEmpty")}</p>}
        <div className="graph-hint">{t(state.isRouting ? "GraphStopEdit" : "GraphConnectHint")}</div>
        {chosen?.kind === "output" && !inspector && <aside className="output-popover" aria-label={t("GraphOutputSettings")}>
          <header><strong>{t("GraphOutputSettings")}</strong><button className="icon-button" aria-label={t("Close")} onClick={() => setSelected(null)}><X size={18} /></button></header>
          {devicePicker(chosen)}<div className="panel-gain"><span>{t("OutputGain")}</span><Gain value={chosen.gainDb} label={t("OutputGain")} onChange={gainDb => changeNode(chosen.id, { gainDb })} /><output>{db(chosen.gainDb)}</output></div>
          <label className="graph-check"><span>{t("Limiter")}</span><input type="checkbox" checked={chosen.limiterEnabled} onChange={e => changeNode(chosen.id, { limiterEnabled: e.target.checked })} /></label>
          <button className="button small" onClick={() => setInspector(true)}><Settings2 size={15} />{t("Inspector")}</button>
        </aside>}
      </div>
      {inspector && (chosen || edge) && <aside className="graph-inspector">
        <header><strong>{chosen ? nodeName(chosen) : t("SendGain")}</strong><button className="icon-button" aria-label={t("Close")} onClick={() => setInspector(false)}><X size={18} /></button></header>
        {chosen ? panelContent(chosen) : edge ? <>
          <div className="edge-route"><span>{nodeName(project.nodes.find(n => n.id === edge.source)!)}</span><span>→ {nodeName(project.nodes.find(n => n.id === edge.target)!)}</span></div>
          <div className="graph-field"><span>{t("SendGain")}</span><div className="edge-gain-row">
            <Gain value={edge.gainDb} label={t("SendGain")} onChange={gainDb => changeEdge(edge.id, { gainDb })} />
            <GainValue value={edge.gainDb} label={t("GraphGainValue")} onChange={gainDb => changeEdge(edge.id, { gainDb })} /></div></div>
          <label className="graph-check"><input type="checkbox" checked={edge.muted} onChange={e => changeEdge(edge.id, { muted: e.target.checked })} />{t("Mute")}</label>
          <button className="button danger small" disabled={!state.canEditRoute} onClick={() => deleteEdge(edge.id)}>{t("EdgeDelete")}</button>
        </> : null}
      </aside>}
    </div>
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
