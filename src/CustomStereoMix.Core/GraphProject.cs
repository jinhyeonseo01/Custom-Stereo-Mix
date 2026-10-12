namespace CustomStereoMix.Core;

public sealed record GraphPosition(double X = 0, double Y = 0);
public sealed record AudioNodeConfig
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Kind { get; init; } = "bus";
    public string Label { get; init; } = "";
    public string? DeviceId { get; init; }
    public string? PreferredMicrophoneId { get; init; }
    public float PreferredMicrophoneGainDb { get; init; }
    public GraphPosition Position { get; init; } = new();
    public float GainDb { get; init; }
    public bool Muted { get; init; }
    public bool Enabled { get; init; } = true;
    public bool LimiterEnabled { get; init; }
    public bool CompressorEnabled { get; init; }
    public float CompressorThresholdDb { get; init; } = -12;
    public float CompressorRatio { get; init; } = 4;
}
public sealed record AudioEdgeConfig
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Source { get; init; } = "";
    public string Target { get; init; } = "";
    public float GainDb { get; init; }
    public bool Muted { get; init; }
}
public sealed record GraphProject
{
    public int SchemaVersion { get; init; } = 2;
    public string Name { get; init; } = "Custom Stereo Mix";
    public AudioNodeConfig[] Nodes { get; init; } = [];
    public AudioEdgeConfig[] Edges { get; init; } = [];
}
public sealed record GraphIssue(MessageKey Key, string? NodeId = null, string? EdgeId = null);
public readonly record struct GraphNodeMeter(float Rms, float Peak, bool Clipped = false);
public sealed record GraphRuntimeSnapshot(IReadOnlyDictionary<string, GraphNodeMeter> Meters, IReadOnlyDictionary<string, string> Errors);
public interface IAudioGraphController
{
    Task StartGraphAsync(GraphProject project, AudioEngineSettings engine, CancellationToken cancellationToken = default);
    void UpdateGraphParameters(GraphProject project);
    GraphRuntimeSnapshot GraphSnapshot { get; }
}
