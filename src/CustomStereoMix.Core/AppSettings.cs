namespace CustomStereoMix.Core;
public sealed record AppWindowSize(double Width, double Height);

public sealed record AppSettings
{
    public GraphProject? Graph { get; init; }
    public bool AdvancedMode { get; init; }
    public string? SimpleBusId { get; init; }
    public AppWindowSize SimpleWindow { get; init; } = new(760, 730);
    public AppWindowSize AdvancedWindow { get; init; } = new(1440, 1000);
    public string? Language { get; init; }
    public string? SourceDeviceId { get; init; }
    public string? TargetDeviceId { get; init; }
    public string? MicrophoneDeviceId { get; init; }
    public bool MinimizeToTray { get; init; } = true;
    // Missing in older settings: preserve the previous shared close/minimize policy.
    public bool? CloseToTray { get; init; }
    // Null means this installation has not dismissed the setup guide yet.
    public bool? HideUsageOnStartup { get; init; }
    public ProcessingSettings Processing { get; init; } = ProcessingSettings.Default;
    public MicMixSettings Microphone { get; init; } = MicMixSettings.Default;
    public AudioEngineSettings Engine { get; init; } = AudioEngineSettings.Default;
}
