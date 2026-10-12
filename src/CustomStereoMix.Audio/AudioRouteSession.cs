using CustomStereoMix.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CustomStereoMix.Audio;

internal sealed class AudioRouteSession : IAsyncDisposable
{
    private readonly MMDevice _source;
    private readonly MMDevice _target;
    private readonly MMDevice? _microphone;
    private readonly RouteConfig _config;
    private readonly FloatRingBuffer _sourceBuffer;
    private readonly FloatRingBuffer _micBuffer;
    private readonly RouteWaveProvider _provider;
    private WasapiLoopbackCapture? _sourceCapture;
    private WasapiCapture? _micCapture;
    private WasapiOut? _output;
    private StreamingAudioFormatConverter? _sourceConverter;
    private StreamingAudioFormatConverter? _micConverter;
    private Timer? _diagnosticTimer;
    private readonly string _routeId = Guid.NewGuid().ToString("N")[..8];
    private volatile bool _disposed;

    public AudioRouteSession(MMDevice source, MMDevice target, MMDevice? microphone, RouteConfig config)
    {
        _source = source;
        _target = target;
        _microphone = microphone;
        _config = config;
        _sourceBuffer = new FloatRingBuffer(config.Engine.BufferSamples);
        _micBuffer = new FloatRingBuffer(config.Engine.BufferSamples);
        _provider = new RouteWaveProvider(_sourceBuffer, _micBuffer, config.Engine, config.Processing, config.Microphone);
        _provider.LevelChanged += (_, snapshot) => LevelChanged?.Invoke(this, snapshot);
    }

    public event EventHandler<LevelMeterSnapshot>? LevelChanged;
    public event EventHandler<Exception>? Faulted;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _sourceCapture = new WasapiLoopbackCapture(_source);
        _sourceConverter = new StreamingAudioFormatConverter(_sourceCapture.WaveFormat, _config.Engine.SampleRate, _config.Engine.Channels);
        _sourceCapture.DataAvailable += OnSourceDataAvailable;
        _sourceCapture.RecordingStopped += OnRecordingStopped;

        if (_config.Microphone.Enabled && _microphone is not null)
        {
            _micCapture = new WasapiCapture(_microphone);
            _micConverter = new StreamingAudioFormatConverter(_micCapture.WaveFormat, _config.Engine.SampleRate, _config.Engine.Channels);
            _micCapture.DataAvailable += OnMicDataAvailable;
            _micCapture.RecordingStopped += OnRecordingStopped;
        }

        _output = new WasapiOut(_target, AudioClientShareMode.Shared, useEventSync: true, _config.Engine.LatencyMilliseconds);
        _output.PlaybackStopped += OnPlaybackStopped;
        _output.Init(_provider);
        _output.Play();
        _sourceCapture.StartRecording();
        _micCapture?.StartRecording();
        RouteDiagnosticLog.Write($"route={_routeId} started source={_source.ID} [{_source.FriendlyName}] format={_sourceCapture.WaveFormat} target={_target.ID} [{_target.FriendlyName}] mix-format={_target.AudioClient.MixFormat} engine={_provider.WaveFormat} latency-ms={_config.Engine.LatencyMilliseconds} buffer-ms={_config.Engine.BufferMilliseconds} microphone={_microphone?.ID ?? "off"} mic-format={_micCapture?.WaveFormat}");
        _diagnosticTimer = new Timer(_ => { if (!_disposed) LogBuffers("buffers"); }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        return Task.CompletedTask;
    }

    public void UpdateProcessing(ProcessingSettings settings) => _provider.UpdateProcessing(settings);

    public void UpdateMicrophone(MicMixSettings settings) => _provider.UpdateMicrophone(settings);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _diagnosticTimer?.Dispose();
        await Task.Run(() =>
        {
            TryStopCapture(_micCapture);
            TryStopCapture(_sourceCapture);
            TryStopOutput(_output);
            LogBuffers("stopped");
            var errors = new List<Exception>();
            foreach (var resource in new IDisposable?[] { _micCapture, _sourceCapture, _output, _microphone, _source, _target })
            {
                try { resource?.Dispose(); }
                catch (Exception ex) { errors.Add(ex); }
            }
            if (errors.Count > 0) throw new AggregateException("오디오 장치 정리 중 오류가 발생했습니다.", errors);
        }).ConfigureAwait(false);
    }

    private void OnSourceDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_disposed || _sourceConverter is null || e.BytesRecorded <= 0)
        {
            return;
        }

        var samples = _sourceConverter.Convert(e.Buffer.AsSpan(0, e.BytesRecorded));
        _sourceBuffer.Write(samples);
    }

    private void OnMicDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_disposed || _micConverter is null || e.BytesRecorded <= 0)
        {
            return;
        }

        var samples = _micConverter.Convert(e.Buffer.AsSpan(0, e.BytesRecorded));
        _micBuffer.Write(samples);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (!_disposed && e.Exception is not null)
        {
            RouteDiagnosticLog.Write($"route={_routeId} capture-fault {e.Exception.Message}");
            Faulted?.Invoke(this, e.Exception);
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (!_disposed && e.Exception is not null)
        {
            RouteDiagnosticLog.Write($"route={_routeId} playback-fault {e.Exception.Message}");
            Faulted?.Invoke(this, e.Exception);
        }
    }

    private void LogBuffers(string reason) => RouteDiagnosticLog.Write($"route={_routeId} {reason} source={_sourceBuffer.Statistics} microphone={_micBuffer.Statistics}");

    private static void TryStopCapture(WasapiCapture? capture)
    {
        if (capture is null)
        {
            return;
        }

        try
        {
            capture.StopRecording();
        }
        catch
        {
        }
    }

    private static void TryStopOutput(WasapiOut? output)
    {
        if (output is null)
        {
            return;
        }

        try
        {
            output.Stop();
        }
        catch
        {
        }
    }
}
