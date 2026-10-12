using CustomStereoMix.Core;

namespace CustomStereoMix.Audio;

/// <summary>One block is computed once, then independently fanned out to device queues.</summary>
public sealed class GraphMixer
{
    private GraphProject _project;
    private readonly AudioNodeConfig[] _order;
    private readonly Dictionary<string, float[]> _blocks;
    private readonly Dictionary<string, AudioEdgeConfig[]> _incoming;
    private sealed class GainRamp(float initial)
    {
        public float Current = initial, Target = initial, Step;
        public int Remaining;
        public void SetTarget(float target, int frames)
        {
            if (Target == target) return;
            Target = target; Remaining = frames; Step = (target - Current) / frames;
        }
        public float Next()
        {
            if (Remaining > 0) { Current += Step; if (--Remaining == 0) Current = Target; }
            return Current;
        }
    }
    private readonly Dictionary<string, GainRamp> _gains = new();
    private readonly int _channels, _rampFrames;
    private sealed record EdgePlan(string Source, string Key, float Gain);
    private sealed record NodePlan(AudioNodeConfig Node, string Key, EdgePlan[] Inputs, ProcessingSettings Dsp);
    private NodePlan[] _plan;
    public IReadOnlyDictionary<string, float[]> Blocks => _blocks;
    public Dictionary<string, GraphNodeMeter> Meters { get; } = new();

    public GraphMixer(GraphProject graph, int channels, int maxFrames = 1024, int sampleRate = 48000)
    {
        if (GraphValidator.GetError(graph) is { } issue) throw new ArgumentException().WithMessage(issue.Key);
        _project = graph; _channels = channels; _rampFrames = Math.Max(1, sampleRate / 100);
        var active = GraphValidator.ActiveNodeIds(graph);
        _order = graph.Nodes.Where(n => active.Contains(n.Id)).OrderBy(n => n.Kind == "bus" ? 1 : n.Kind == "output" ? 2 : 0).ToArray();
        _blocks = _order.ToDictionary(n => n.Id, _ => new float[maxFrames * channels]);
        _incoming = _order.ToDictionary(n => n.Id, n => graph.Edges.Where(e => e.Target == n.Id && active.Contains(e.Source)).ToArray());
        foreach (var n in _order) _gains["n:" + n.Id] = new(n.Muted ? 0 : DspProcessor.DbToLinear(n.GainDb));
        foreach (var e in graph.Edges) _gains["e:" + e.Id] = new(e.Muted ? 0 : DspProcessor.DbToLinear(e.GainDb));
        _plan = CompileParameters(graph);
    }

    public void UpdateParameters(GraphProject graph)
    {
        if (GraphValidator.GetError(graph) is { } issue) throw new ArgumentException().WithMessage(issue.Key);
        if (!GraphValidator.SameTopology(_project, graph)) throw new InvalidOperationException().WithMessage(MessageKey.StopBeforeChange);
        Volatile.Write(ref _project, graph);
        Volatile.Write(ref _plan, CompileParameters(graph));
    }

    private NodePlan[] CompileParameters(GraphProject graph)
    {
        var nodes = graph.Nodes.ToDictionary(n => n.Id);
        var edges = graph.Edges.ToDictionary(e => e.Id);
        return _order.Select(original =>
        {
            var node = nodes[original.Id];
            return new NodePlan(node, "n:" + node.Id, _incoming[node.Id].Select(e =>
                new EdgePlan(e.Source, "e:" + e.Id, edges[e.Id].Muted ? 0 : DspProcessor.DbToLinear(edges[e.Id].GainDb))).ToArray(),
                new(0, node.CompressorEnabled, node.CompressorThresholdDb, node.CompressorRatio, node.LimiterEnabled));
        }).ToArray();
    }

    public void Process(IReadOnlyDictionary<string, float[]> sources, int frames)
    {
        var plan = Volatile.Read(ref _plan);
        var count = frames * _channels;
        foreach (var item in plan)
        {
            var node = item.Node;
            var block = _blocks[node.Id].AsSpan(0, count);
            block.Clear();
            if (node.Kind is "capture" or "loopback")
            {
                if (sources.TryGetValue(node.Id, out var data)) data.AsSpan(0, Math.Min(count, data.Length)).CopyTo(block);
            }
            else foreach (var edge in item.Inputs)
            {
                Add(_blocks[edge.Source].AsSpan(0, count), block, edge.Key, edge.Gain);
            }
            Gain(block, item.Key, node.Muted ? 0 : DspProcessor.DbToLinear(node.GainDb));
            var peakBefore = 0f; foreach (var value in block) peakBefore = Math.Max(peakBefore, Math.Abs(value));
            if (node.Kind is "bus" or "output")
                DspProcessor.Apply(block, item.Dsp);
            var sum = 0d; var peak = 0f;
            foreach (var value in block) { sum += value * value; peak = Math.Max(peak, Math.Abs(value)); }
            Meters[node.Id] = new((float)Math.Sqrt(sum / Math.Max(1, count)), peak, peakBefore > 1);
        }
    }

    private void Add(ReadOnlySpan<float> source, Span<float> destination, string key, float target)
    {
        var ramp = _gains[key];
        ramp.SetTarget(target, _rampFrames);
        for (var i = 0; i < destination.Length; i += _channels)
        {
            var gain = ramp.Next();
            for (var c = 0; c < _channels; c++) destination[i + c] += source[i + c] * gain;
        }
    }
    private void Gain(Span<float> block, string key, float target)
    {
        var ramp = _gains[key]; ramp.SetTarget(target, _rampFrames);
        for (var i = 0; i < block.Length; i += _channels)
        {
            var gain = ramp.Next();
            for (var c = 0; c < _channels; c++) block[i + c] *= gain;
        }
    }
}
