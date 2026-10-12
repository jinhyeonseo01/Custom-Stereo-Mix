namespace CustomStereoMix.Core;

public sealed record RouteConfig(
    string SourceDeviceId,
    string TargetDeviceId,
    string? MicrophoneDeviceId,
    AudioEngineSettings Engine,
    ProcessingSettings Processing,
    MicMixSettings Microphone);
