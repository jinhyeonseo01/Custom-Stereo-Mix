namespace CustomStereoMix.Core;

public static class GraphValidator
{
    public static GraphIssue? GetError(GraphProject graph, bool requireRoute = false,
        IReadOnlyList<AudioDeviceDescriptor>? render = null, IReadOnlyList<AudioDeviceDescriptor>? capture = null, bool validateDevices = true)
    {
        if (graph is null || graph.SchemaVersion != 2 || graph.Nodes is null || graph.Edges is null ||
            graph.Nodes.Length > 128 || graph.Edges.Length > 512 || graph.Name is null || graph.Name.Length > 120)
            return new(MessageKey.GraphInvalid);
        var nodes = new Dictionary<string, AudioNodeConfig>(StringComparer.Ordinal);
        foreach (var n in graph.Nodes)
        {
            if (n is null || string.IsNullOrWhiteSpace(n.Id) || n.Id.Length > 80 ||
                n.Kind is not ("loopback" or "capture" or "bus" or "output") || n.Label is null || n.Label.Length > 160 ||
                n.Position is null || !double.IsFinite(n.Position.X) || !double.IsFinite(n.Position.Y) ||
                Math.Abs(n.Position.X) > 100000 || Math.Abs(n.Position.Y) > 100000 ||
                !float.IsFinite(n.GainDb) || n.GainDb is < -60 or > 12 ||
                !float.IsFinite(n.PreferredMicrophoneGainDb) || n.PreferredMicrophoneGainDb is < -60 or > 12 ||
                !float.IsFinite(n.CompressorThresholdDb) || n.CompressorThresholdDb is < -60 or > 0 ||
                !float.IsFinite(n.CompressorRatio) || n.CompressorRatio is < 1 or > 20 ||
                n.DeviceId?.Length > 1024 || !nodes.TryAdd(n.Id, n)) return new(MessageKey.GraphInvalid, n?.Id);
        }
        var outputs = graph.Nodes.Where(n => n.Kind == "output" && !string.IsNullOrEmpty(n.DeviceId)).Select(n => n.DeviceId!).ToArray();
        if (outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != outputs.Length) return new(MessageKey.GraphDuplicate);
        var ids = new HashSet<string>(); var pairs = new HashSet<(string, string)>();
        foreach (var e in graph.Edges)
        {
            if (e is null || string.IsNullOrWhiteSpace(e.Id) || e.Id.Length > 80 || !ids.Add(e.Id) ||
                !nodes.TryGetValue(e.Source, out var s) || !nodes.TryGetValue(e.Target, out var t) ||
                !float.IsFinite(e.GainDb) || e.GainDb is < -60 or > 12)
                return new(MessageKey.GraphInvalid, EdgeId: e?.Id);
            if (!pairs.Add((e.Source, e.Target))) return new(MessageKey.GraphDuplicate, EdgeId: e.Id);
            if (!(s.Kind is "loopback" or "capture" && t.Kind == "bus" || s.Kind == "bus" && t.Kind == "output"))
                return new(MessageKey.GraphInvalid, EdgeId: e.Id);
        }
        // Validate physical return paths across ALL buses, even when muted.
        foreach (var output in graph.Nodes.Where(n => n.Kind == "output" && n.DeviceId is not null))
            foreach (var source in graph.Nodes.Where(n => n.Kind == "loopback" &&
                string.Equals(n.DeviceId, output.DeviceId, StringComparison.OrdinalIgnoreCase)))
                pairs.Add((output.Id, source.Id));
        if (render is not null && capture is not null)
            foreach (var output in graph.Nodes.Where(n => n.Kind == "output"))
            {
                var group = render.FirstOrDefault(d => string.Equals(d.Id, output.DeviceId, StringComparison.OrdinalIgnoreCase))?.ReturnPathGroup;
                if (group is null) continue;
                foreach (var source in graph.Nodes.Where(n => n.Kind == "capture"))
                    if (capture.FirstOrDefault(d => string.Equals(d.Id, source.DeviceId, StringComparison.OrdinalIgnoreCase))?.ReturnPathGroup == group)
                        pairs.Add((output.Id, source.Id));
            }
        var visiting = new HashSet<string>(); var visited = new HashSet<string>();
        bool Cycle(string id)
        {
            if (visited.Contains(id)) return false;
            if (!visiting.Add(id)) return true;
            foreach (var next in pairs.Where(p => p.Item1 == id).Select(p => p.Item2))
                if (Cycle(next)) return true;
            visiting.Remove(id); visited.Add(id); return false;
        }
        if (nodes.Keys.Any(Cycle)) return new(MessageKey.GraphCycle);
        var active = ActiveNodeIds(graph);
        if (requireRoute && !graph.Nodes.Any(n => n.Kind == "output" && active.Contains(n.Id))) return new(MessageKey.GraphIncomplete);
        if (validateDevices && render is not null && capture is not null)
            foreach (var n in graph.Nodes.Where(n => active.Contains(n.Id) && n.Kind != "bus"))
            {
                var d = (n.Kind == "capture" ? capture : render).FirstOrDefault(d => string.Equals(d.Id, n.DeviceId, StringComparison.OrdinalIgnoreCase));
                if (d is null) return new(MessageKey.GraphMissingDevice, n.Id);
            }
        return null;
    }

    public static HashSet<string> ActiveNodeIds(GraphProject graph)
    {
        var reachable = graph.Nodes.Where(n => n.Enabled && n.Kind is "loopback" or "capture").Select(n => n.Id).ToHashSet();
        foreach (var bus in graph.Nodes.Where(n => n.Enabled && n.Kind == "bus"))
            if (graph.Edges.Any(e => e.Target == bus.Id && reachable.Contains(e.Source))) reachable.Add(bus.Id);
        foreach (var output in graph.Nodes.Where(n => n.Enabled && n.Kind == "output"))
            if (graph.Edges.Any(e => e.Target == output.Id && reachable.Contains(e.Source))) reachable.Add(output.Id);
        var active = graph.Nodes.Where(n => n.Kind == "output" && reachable.Contains(n.Id)).Select(n => n.Id).ToHashSet();
        bool changed;
        do { changed = false; foreach (var e in graph.Edges) if (active.Contains(e.Target) && reachable.Contains(e.Source)) changed |= active.Add(e.Source); } while (changed);
        return active;
    }

    public static bool SameTopology(GraphProject a, GraphProject b) =>
        a.Nodes.Length == b.Nodes.Length && a.Edges.Length == b.Edges.Length &&
        a.Nodes.All(n => b.Nodes.Any(m => m.Id == n.Id && m.Kind == n.Kind && m.DeviceId == n.DeviceId && m.Enabled == n.Enabled)) &&
        a.Edges.All(e => b.Edges.Any(f => f.Id == e.Id && f.Source == e.Source && f.Target == e.Target));
}
