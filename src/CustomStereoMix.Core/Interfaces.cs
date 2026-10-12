namespace CustomStereoMix.Core;

public interface IAudioDeviceService
{
    IReadOnlyList<AudioDeviceDescriptor> GetLoopbackSources();
    IReadOnlyList<AudioDeviceDescriptor> GetRenderTargets();
    IReadOnlyList<AudioDeviceDescriptor> GetMicrophones();
}

public interface IAudioRouteController
{
    RouteState State { get; }
    event EventHandler<RouteStatusSnapshot>? StatusChanged;
    event EventHandler<LevelMeterSnapshot>? LevelChanged;

    Task StartAsync(RouteConfig config, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    void UpdateProcessing(ProcessingSettings settings);
    void UpdateMicrophoneMix(MicMixSettings settings);
}

public interface ISettingsStore
{
    string? LoadWarning => null;
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
