namespace CustomStereoMix.Core;

public static class GraphMigration
{
    public static GraphProject FromSettings(AppSettings settings)
    {
        var nodes = new List<AudioNodeConfig>
        {
            new() { Id = "source", Kind = "loopback", DeviceId = settings.SourceDeviceId, Position = new(0, 0) },
            new() { Id = "mix", Kind = "bus", Position = new(470, 0),
                PreferredMicrophoneId = settings.MicrophoneDeviceId is null ? null : "mic", PreferredMicrophoneGainDb = settings.Microphone.GainDb,
                GainDb = settings.Processing.GainDb, CompressorEnabled = settings.Processing.CompressorEnabled,
                CompressorThresholdDb = settings.Processing.CompressorThresholdDb,
                CompressorRatio = settings.Processing.CompressorRatio, LimiterEnabled = settings.Processing.LimiterEnabled },
            new() { Id = "output", Kind = "output", DeviceId = settings.TargetDeviceId, Position = new(1040, 0) }
        };
        var edges = new List<AudioEdgeConfig> { new() { Id = "source-mix", Source = "source", Target = "mix" }, new() { Id = "mix-output", Source = "mix", Target = "output" } };
        if (settings.MicrophoneDeviceId is not null)
        {
            nodes.Add(new() { Id = "mic", Kind = "capture", DeviceId = settings.MicrophoneDeviceId, Position = new(0, 240) });
            if (settings.Microphone.Enabled)
                edges.Add(new() { Id = "mic-mix", Source = "mic", Target = "mix", GainDb = settings.Microphone.GainDb });
        }
        return new() { Nodes = nodes.ToArray(), Edges = edges.ToArray() };
    }

    public static bool IsSimple(GraphProject graph) =>
        graph.Nodes.Any(n => n.Id == "source" && n.Kind == "loopback") &&
        graph.Nodes.Any(n => n.Id == "mix" && n.Kind == "bus") &&
        graph.Nodes.Any(n => n.Id == "output" && n.Kind == "output") &&
        graph.Nodes.All(n => n.Id is "source" or "mix" or "output" or "mic") &&
        graph.Nodes.All(n => n.Enabled && !n.Muted &&
            (n.Kind == "bus" ? n.GainDb is >= -12 and <= 12 && n.CompressorThresholdDb is >= -40 and <= -3 && n.CompressorRatio is >= 1 and <= 10
                : n.GainDb == 0 && !n.CompressorEnabled && !n.LimiterEnabled)) &&
        graph.Nodes.Count(n => n.Kind == "bus") == 1 &&
        graph.Edges.All(e => !e.Muted && (e.Source == "mic" ? e.GainDb is >= -12 and <= 12 : e.GainDb == 0)) &&
        graph.Edges.All(e => (e.Source == "source" || e.Source == "mic") && e.Target == "mix" || e.Source == "mix" && e.Target == "output");
}
