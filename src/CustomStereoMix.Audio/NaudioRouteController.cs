using CustomStereoMix.Core;
using NAudio.CoreAudioApi;

namespace CustomStereoMix.Audio;

public sealed class NaudioRouteController : IAudioRouteController
{
    private readonly NaudioDeviceService _devices;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AudioRouteSession? _session;

    public NaudioRouteController(NaudioDeviceService devices)
    {
        _devices = devices;
    }

    public RouteState State { get; private set; } = RouteState.Stopped;

    public event EventHandler<RouteStatusSnapshot>? StatusChanged;
    public event EventHandler<LevelMeterSnapshot>? LevelChanged;

    public async Task StartAsync(RouteConfig config, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        MMDevice? source = null, target = null, microphone = null;
        try
        {
            if (State is RouteState.Running or RouteState.Starting)
            {
                return;
            }

            AudioSettingsValidator.Validate(config.Engine, config.Processing, config.Microphone);
            var error = RouteValidator.GetError(
                _devices.GetLoopbackSources().FirstOrDefault(d => d.Id == config.SourceDeviceId),
                _devices.GetRenderTargets().FirstOrDefault(d => d.Id == config.TargetDeviceId),
                _devices.GetMicrophones().FirstOrDefault(d => d.Id == config.MicrophoneDeviceId), config.Microphone.Enabled);
            if (error is not null) throw new InvalidOperationException(error);
            SetState(RouteState.Starting, "연결 시작 중");
            source = _devices.GetDevice(config.SourceDeviceId);
            target = _devices.GetDevice(config.TargetDeviceId);
            microphone = config.Microphone.Enabled && !string.IsNullOrWhiteSpace(config.MicrophoneDeviceId)
                ? _devices.GetDevice(config.MicrophoneDeviceId)
                : null;

            var session = new AudioRouteSession(source, target, microphone, config);
            _session = session;
            source = target = microphone = null; // Ownership transferred to the session.
            session.LevelChanged += (_, snapshot) => LevelChanged?.Invoke(this, snapshot);
            session.Faulted += async (_, exception) => await FaultAsync(session, exception).ConfigureAwait(false);
            await session.StartAsync(cancellationToken).ConfigureAwait(false);
            SetState(RouteState.Running, "연결 중");
        }
        catch (Exception ex)
        {
            SetState(RouteState.Faulted, ex.Message);
            if (_session is not null)
            {
                await DisposeSessionAsync().ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            try { microphone?.Dispose(); }
            finally
            {
                try { target?.Dispose(); }
                finally { try { source?.Dispose(); } finally { _gate.Release(); } }
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is null)
            {
                SetState(RouteState.Stopped, "대기 중");
                return;
            }

            SetState(RouteState.Stopping, "연결 중지 중");
            await DisposeSessionAsync().ConfigureAwait(false);
            LevelChanged?.Invoke(this, LevelMeterSnapshot.Silent);
            SetState(RouteState.Stopped, "대기 중");
        }
        catch (Exception ex)
        {
            LevelChanged?.Invoke(this, LevelMeterSnapshot.Silent);
            SetState(RouteState.Faulted, ex.Message);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void UpdateProcessing(ProcessingSettings settings) => _session?.UpdateProcessing(settings);

    public void UpdateMicrophoneMix(MicMixSettings settings) => _session?.UpdateMicrophone(settings);

    private async Task DisposeSessionAsync()
    {
        var session = _session;
        _session = null;
        if (session is not null) await session.DisposeAsync().ConfigureAwait(false);
    }

    private async Task FaultAsync(AudioRouteSession sender, Exception exception)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // A late callback from an old route must not stop its replacement.
            if (!ReferenceEquals(sender, _session)) return;
            try { await DisposeSessionAsync().ConfigureAwait(false); }
            catch (Exception cleanupError) { exception = new AggregateException(exception, cleanupError); }
            LevelChanged?.Invoke(this, LevelMeterSnapshot.Silent);
            SetState(RouteState.Faulted, exception.Message);
        }
        finally { _gate.Release(); }
    }

    private void SetState(RouteState state, string message)
    {
        State = state;
        StatusChanged?.Invoke(this, new RouteStatusSnapshot(state, message));
    }
}
