using CustomStereoMix.Core;

namespace CustomStereoMix.State;

public sealed partial class MainWindowViewModel
{
    private GraphProject? _graph;
    private bool _advancedMode;
    private string? _simpleBusId;
    public string? SimpleBusId => Graph.Nodes.FirstOrDefault(n => n.Id == _simpleBusId && n.Kind == "bus")?.Id ?? Graph.Nodes.FirstOrDefault(n => n.Kind == "bus")?.Id;
    public void SetSimpleBus(string id)
    {
        if (!CanEditSettings || !Graph.Nodes.Any(n => n.Id == id && n.Kind == "bus")) throw new InvalidOperationException().WithMessage(MessageKey.GraphInvalid);
        _simpleBusId = id; OnPropertyChanged(nameof(SimpleBusId)); ScheduleSave();
    }
    private AppWindowSize _simpleWindow = new(760, 730), _advancedWindow = new(1440, 1000);
    public AppWindowSize WindowSize => AdvancedMode ? _advancedWindow : _simpleWindow;
    public void RememberWindowSize(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 320 || height < 320) return;
        if (AdvancedMode) _advancedWindow = new(width, height); else _simpleWindow = new(width, height);
        ScheduleSave();
    }
    public GraphProject Graph => _graph ?? new();
    public long GraphRevision { get; private set; }
    public bool IsSimpleGraph => GraphMigration.IsSimple(Graph);
    public bool AdvancedMode => _advancedMode;
    public GraphRuntimeSnapshot GraphSnapshot => (_router as IAudioGraphController)?.GraphSnapshot ??
        new(new Dictionary<string, GraphNodeMeter>(), new Dictionary<string, string>());
    private AudioEngineSettings CurrentEngine() => new((int)SampleRate, (int)Channels, (int)LatencyMilliseconds, (int)BufferMilliseconds);

    public void SetAdvancedMode(bool enabled)
    {
        if (!CanEditSettings) throw new InvalidOperationException().WithMessage(MessageKey.WaitSettings);
        _advancedMode = enabled;
        if (!enabled && IsSimpleGraph)
        {
            var wasInitialized = _initialized;
            _initialized = false;
            try
            {
                var bus = Graph.Nodes.Single(n => n.Kind == "bus");
                _limiterEnabled = bus.LimiterEnabled;
                var microphone = Graph.Edges.FirstOrDefault(e => e.Source == "mic");
                // Project current graph values back into the simple controls.
                GainDb = bus.GainDb; CompressorEnabled = bus.CompressorEnabled;
                CompressorThresholdDb = bus.CompressorThresholdDb; CompressorRatio = bus.CompressorRatio;
                IsMicMixEnabled = microphone is not null; MicGainDb = microphone?.GainDb ?? MicGainDb;
                SelectedSource = Sources.FirstOrDefault(d => d.Id == Graph.Nodes.FirstOrDefault(n => n.Id == "source")?.DeviceId);
                SelectedTarget = Targets.FirstOrDefault(d => d.Id == Graph.Nodes.FirstOrDefault(n => n.Id == "output")?.DeviceId);
                SelectedMicrophone = Microphones.FirstOrDefault(d => d.Id == Graph.Nodes.FirstOrDefault(n => n.Id == "mic")?.DeviceId);
            }
            finally { _initialized = wasInitialized; }
        }
        OnPropertyChanged(nameof(AdvancedMode));
        ScheduleSave();
    }

    public void SetGraph(GraphProject graph, long revision)
    {
        if (!CanEditSettings) throw new InvalidOperationException().WithMessage(MessageKey.WaitSettings);
        if (revision != GraphRevision) throw new InvalidOperationException().WithMessage(MessageKey.GraphStale);
        if (GraphValidator.GetError(graph, render: Targets.ToArray(), capture: Microphones.ToArray(), validateDevices: false) is { } error)
            throw new InvalidOperationException().WithMessage(error.Key);
        if (IsRouting && !GraphValidator.SameTopology(Graph, graph))
            throw new InvalidOperationException().WithMessage(MessageKey.StopBeforeChange);
        // Runtime parameters accept the same topology only. Validate before committing.
        if (IsRouting) (_router as IAudioGraphController)?.UpdateGraphParameters(graph);
        _graph = graph;
        GraphRevision++;
        OnPropertyChanged(nameof(Graph));
        RefreshComputedState();
        ScheduleSave();
    }

    public void SyncSimpleGraph()
    {
        if (!_initialized || _graph is null || !IsSimpleGraph || AdvancedMode) return;
        var updated = GraphMigration.FromSettings(CaptureSettings() with { Graph = null });
        // Keep the user's node placement and aliases when using simple controls.
        updated = updated with { Nodes = updated.Nodes.Select(n =>
            _graph.Nodes.FirstOrDefault(old => old.Id == n.Id) is { } old
                ? n with { Label = old.Label, Position = old.Position } : n).ToArray() };
        _graph = updated;
        GraphRevision++;
        if (IsRouting) (_router as IAudioGraphController)?.UpdateGraphParameters(updated);
        OnPropertyChanged(nameof(Graph));
        RefreshComputedState();
    }
}
