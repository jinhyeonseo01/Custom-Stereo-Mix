namespace CustomStereoMix.Core;

public sealed record AppSettings
{
    public string? Language { get; init; }
    public string? SourceDeviceId { get; init; }
    public string? TargetDeviceId { get; init; }
    public string? MicrophoneDeviceId { get; init; }
    public bool MinimizeToTray { get; init; } = true;
    // Null means this installation has not dismissed the setup guide yet.
    public bool? HideUsageOnStartup { get; init; }
    public ProcessingSettings Processing { get; init; } = ProcessingSettings.Default;
    public MicMixSettings Microphone { get; init; } = MicMixSettings.Default;
    public AudioEngineSettings Engine { get; init; } = AudioEngineSettings.Default;
}
