using CustomStereoMix.Core;
using NAudio.CoreAudioApi;

namespace CustomStereoMix.Audio;

public sealed class NaudioRouteController : IAudioRouteController, IAudioGraphController
{
    private readonly NaudioDeviceService _devices;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AudioRouteSession? _session;
    private GraphAudioSession? _graphSession;
    private GraphRuntimeSnapshot _lastGraphError = new(new Dictionary<string, GraphNodeMeter>(), new Dictionary<string, string>());
    public GraphRuntimeSnapshot GraphSnapshot => _graphSession?.Snapshot ?? _lastGraphError;
    public void UpdateGraphParameters(GraphProject graph) => _graphSession?.UpdateParameters(graph);

    public async Task StartGraphAsync(GraphProject graph, AudioEngineSettings engine, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is RouteState.Running or RouteState.Starting) return;
            _lastGraphError = new(new Dictionary<string, GraphNodeMeter>(), new Dictionary<string, string>());
            AudioSettingsValidator.Validate(engine, ProcessingSettings.Default, MicMixSettings.Default);
            if (GraphValidator.GetError(graph, true, _devices.GetRenderTargets(), _devices.GetMicrophones()) is { } issue)
                throw new InvalidOperationException().WithMessage(issue.Key);
            SetState(RouteState.Starting, new(MessageKey.StartingRouting));
            var session = new GraphAudioSession(_devices, graph, engine);
            _graphSession = session;
            session.LevelChanged += (_, snapshot) => LevelChanged?.Invoke(this, snapshot);
            session.Faulted += async (_, error) =>
            {
                await _gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (!ReferenceEquals(_graphSession, session)) return;
                    _lastGraphError = new(new Dictionary<string, GraphNodeMeter>(), session.Snapshot.Errors);
                    try { await DisposeSessionAsync().ConfigureAwait(false); }
                    finally { SetState(RouteState.Faulted, new(MessageKey.Detail, error)); }
                }
                finally { _gate.Release(); }
            };
            session.Start();
            SetState(RouteState.Running, new(MessageKey.GraphRunning));
        }
        catch (Exception error)
        {
            if (_graphSession is { } failed) _lastGraphError = new(new Dictionary<string, GraphNodeMeter>(), failed.Snapshot.Errors);
            try { await DisposeSessionAsync().ConfigureAwait(false); }
            finally { SetState(RouteState.Faulted, new(MessageKey.Detail, error)); }
            throw;
        }
        finally { _gate.Release(); }
    }

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
            if (error is not null) throw new InvalidOperationException().WithMessage(error.Value);
            SetState(RouteState.Starting, new(MessageKey.StartingRouting));
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
            SetState(RouteState.Running, new(MessageKey.Routing));
        }
        catch (Exception ex)
        {
            SetState(RouteState.Faulted, new(MessageKey.Detail, ex));
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
            _lastGraphError = new(new Dictionary<string, GraphNodeMeter>(), new Dictionary<string, string>());
            if (_session is null && _graphSession is null)
            {
                SetState(RouteState.Stopped, new(MessageKey.Ready));
                return;
            }

            SetState(RouteState.Stopping, new(MessageKey.StoppingRouting));
            await DisposeSessionAsync().ConfigureAwait(false);
            LevelChanged?.Invoke(this, LevelMeterSnapshot.Silent);
            SetState(RouteState.Stopped, new(MessageKey.Ready));
        }
        catch (Exception ex)
        {
            LevelChanged?.Invoke(this, LevelMeterSnapshot.Silent);
            SetState(RouteState.Faulted, new(MessageKey.Detail, ex));
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
        var graph = _graphSession;
        _graphSession = null;
        if (graph is not null) await graph.DisposeAsync().ConfigureAwait(false);
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
            SetState(RouteState.Faulted, new(MessageKey.Detail, exception));
        }
        finally { _gate.Release(); }
    }

    private void SetState(RouteState state, LocalizedMessage message)
    {
        State = state;
        StatusChanged?.Invoke(this, new RouteStatusSnapshot(state, message));
    }
}
