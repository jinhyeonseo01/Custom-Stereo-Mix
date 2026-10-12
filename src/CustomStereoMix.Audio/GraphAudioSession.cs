using CustomStereoMix.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CustomStereoMix.Audio;

internal sealed class GraphAudioSession : IAsyncDisposable
{
    private sealed class Input : IDisposable
    {
        public required MMDevice Device;
        public required WasapiCapture Capture;
        public required StreamingAudioFormatConverter Converter;
        public required AdaptiveAudioBuffer Buffer;
        public required float[] Scratch;
        public void Dispose() { try { Capture.StopRecording(); } finally { Capture.Dispose(); Device.Dispose(); } }
    }
    private sealed class Output : IDisposable
    {
        public required MMDevice Device;
        public required WasapiOut Player;
        public required AdaptiveAudioBuffer Buffer;
        public volatile bool Failed;
        public void Dispose() { try { Player.Stop(); } finally { Player.Dispose(); Device.Dispose(); } }
    }
    private readonly Dictionary<string, Input> _inputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Output> _outputs = new();
    private readonly Dictionary<string, float[]> _sources = new();
    private readonly Dictionary<string, string> _errors = new();
    private readonly object _processGate = new();
    private readonly GraphProject _graph;
    private readonly AudioEngineSettings _engine;
    private readonly GraphMixer _mixer;
    private readonly NaudioDeviceService _devices;
    private string? _master;
    private Dictionary<string, bool> _outputLimiters;
    private int _lastBlockSamples;
    private int _diagnosticTicks;
    private Timer? _meterTimer;
    private volatile bool _disposed;
    private const int BlockFrames = 1024;
    public event EventHandler<LevelMeterSnapshot>? LevelChanged;
    public event EventHandler<Exception>? Faulted;
    public GraphRuntimeSnapshot Snapshot
    {
        get { lock (_processGate) return new(new Dictionary<string, GraphNodeMeter>(_mixer.Meters), new Dictionary<string, string>(_errors)); }
    }
    public GraphAudioSession(NaudioDeviceService devices, GraphProject graph, AudioEngineSettings engine)
    {
        _devices = devices; _graph = graph; _engine = engine; _mixer = new(graph, engine.Channels, BlockFrames, engine.SampleRate);
        _outputLimiters = graph.Nodes.Where(n => n.Kind == "output").ToDictionary(n => n.Id, n => n.LimiterEnabled);
    }

    public void Start()
    {
        var active = GraphValidator.ActiveNodeIds(_graph);
        foreach (var node in _graph.Nodes.Where(n => active.Contains(n.Id) && n.Kind is "loopback" or "capture"))
        {
            var key = node.Kind + ":" + node.DeviceId;
            if (!_inputs.TryGetValue(key, out var input))
            {
                var device = _devices.GetDevice(node.DeviceId!);
                WasapiCapture capture;
                try { capture = node.Kind == "loopback" ? new WasapiLoopbackCapture(device) : new WasapiCapture(device); }
                catch { device.Dispose(); throw; }
                try
                {
                    input = new() { Device = device, Capture = capture,
                        Converter = new(capture.WaveFormat, _engine.SampleRate, _engine.Channels),
                        Buffer = new(_engine.BufferSamples, _engine.Channels, _engine.SampleRate * _engine.LatencyMilliseconds / 2000),
                        Scratch = new float[BlockFrames * _engine.Channels] };
                }
                catch { try { capture.Dispose(); } finally { device.Dispose(); } throw; }
                _inputs.Add(key, input);
                var captured = input;
                capture.DataAvailable += (_, e) => { if (!_disposed && e.BytesRecorded > 0) captured.Buffer.Write(captured.Converter.Convert(e.Buffer.AsSpan(0, e.BytesRecorded))); };
                capture.RecordingStopped += (_, e) => { if (!_disposed && e.Exception is { } error) InputFailed(key, error); };
            }
            _sources[node.Id] = input.Scratch;
        }
        foreach (var node in _graph.Nodes.Where(n => active.Contains(n.Id) && n.Kind == "output"))
        {
            MMDevice? device = null; WasapiOut? player = null;
            try
            {
                device = _devices.GetDevice(node.DeviceId!);
                player = new(device, AudioClientShareMode.Shared, true, _engine.LatencyMilliseconds);
                var output = new Output { Device = device, Player = player,
                    Buffer = new(_engine.BufferSamples, _engine.Channels, _engine.SampleRate * _engine.LatencyMilliseconds / 2000) };
                _outputs.Add(node.Id, output);
                player.PlaybackStopped += (_, e) => { if (!_disposed && e.Exception is { } error) OutputFailed(node.Id, error); };
                player.Init(new Provider(this, node.Id));
                _master ??= node.Id;
            }
            catch (Exception error)
            {
                _outputs.Remove(node.Id);
                try { player?.Dispose(); } finally { device?.Dispose(); }
                _errors[node.Id] = error.Message;
                RouteDiagnosticLog.Write("graph output unavailable " + node.Id + ": " + error.Message);
            }
        }
        if (_outputs.Count == 0) throw new InvalidOperationException().WithMessage(MessageKey.GraphMissingDevice);
        foreach (var input in _inputs.Values) input.Capture.StartRecording();
        foreach (var output in _outputs)
            try { output.Value.Player.Play(); } catch (Exception error) { OutputFailed(output.Key, error); }
        if (_master is null) throw new InvalidOperationException().WithMessage(MessageKey.GraphMissingDevice);
        // Meter serialization and notifications run outside PCM callbacks.
        _meterTimer = new Timer(_ => EmitMeter(), null, 50, 50);
    }

    public void UpdateParameters(GraphProject graph)
    {
        _mixer.UpdateParameters(graph);
        Volatile.Write(ref _outputLimiters, graph.Nodes.Where(n => n.Kind == "output").ToDictionary(n => n.Id, n => n.LimiterEnabled));
    }
    private void InputFailed(string key, Exception error)
    {
        lock (_processGate)
        {
            _inputs[key].Buffer.Clear();
            foreach (var node in _graph.Nodes.Where(n => string.Equals(n.Kind + ":" + n.DeviceId, key, StringComparison.OrdinalIgnoreCase))) _errors[node.Id] = error.Message;
        }
        RouteDiagnosticLog.Write("graph capture fault: " + error.Message);
    }
    private void OutputFailed(string id, Exception error)
    {
        bool fatal;
        lock (_processGate)
        {
            if (!_outputs.ContainsKey(id)) return;
            _outputs[id].Failed = true; _errors[id] = error.Message;
            if (_master == id) _master = _outputs.FirstOrDefault(p => !p.Value.Failed).Key;
            fatal = _master is null;
        }
        RouteDiagnosticLog.Write("graph output fault: " + error.Message);
        if (fatal) Faulted?.Invoke(this, error);
    }
    private void EmitMeter()
    {
        if (_disposed) return;
        LevelMeterSnapshot level;
        string? diagnostics = null;
        lock (_processGate)
        {
            level = _master is not null && _lastBlockSamples > 0
                ? DspProcessor.Meter(_mixer.Blocks[_master].AsSpan(0, _lastBlockSamples)) : LevelMeterSnapshot.Silent;
            if (++_diagnosticTicks % 100 == 0)
                diagnostics = "graph buffers " + string.Join(" | ", _inputs.Select(p => p.Key + "=" + p.Value.Buffer.Statistics)
                    .Concat(_outputs.Select(p => p.Key + "=" + p.Value.Buffer.Statistics)));
        }
        if (diagnostics is not null) RouteDiagnosticLog.Write(diagnostics);
        LevelChanged?.Invoke(this, level);
    }
    private int Read(string id, byte[] buffer, int offset, int count)
    {
        var samples = count / sizeof(float); samples -= samples % _engine.Channels;
        lock (_processGate)
        {
            if (_disposed || _outputs[id].Failed) { Array.Clear(buffer, offset, count); return count; }
            var output = _outputs[id]; var position = 0;
            while (position < samples)
            {
                var take = Math.Min(BlockFrames * _engine.Channels, samples - position);
                if (id == _master)
                {
                    foreach (var input in _inputs.Values) input.Buffer.Read(input.Scratch.AsSpan(0, take));
                    _mixer.Process(_sources, take / _engine.Channels);
                    _lastBlockSamples = take;
                    foreach (var pair in _outputs)
                        if (!pair.Value.Failed && pair.Key != _master) pair.Value.Buffer.Write(_mixer.Blocks[pair.Key].AsSpan(0, take));
                }
                // Each render endpoint owns its own consumer. No shared read cursor.
                var floats = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(buffer.AsSpan(offset + position * sizeof(float), take * sizeof(float)));
                if (id == _master) _mixer.Blocks[id].AsSpan(0, take).CopyTo(floats);
                else output.Buffer.Read(floats);
                if (Volatile.Read(ref _outputLimiters).GetValueOrDefault(id))
                    for (var i = 0; i < floats.Length; i++) floats[i] = Math.Clamp(floats[i], -1, 1);
                position += take;
            }
            if (samples * sizeof(float) < count) Array.Clear(buffer, offset + samples * sizeof(float), count - samples * sizeof(float));
        }
        return count;
    }
    private sealed class Provider(GraphAudioSession session, string id) : IWaveProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(session._engine.SampleRate, session._engine.Channels);
        public int Read(byte[] buffer, int offset, int count) => session.Read(id, buffer, offset, count);
    }
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_meterTimer is not null) await _meterTimer.DisposeAsync();
        var errors = new List<Exception>();
        await Task.Run(() =>
        {
            foreach (var resource in _inputs.Values.Cast<IDisposable>().Concat(_outputs.Values))
                try { resource.Dispose(); } catch (Exception error) { errors.Add(error); }
        });
        if (errors.Count > 0) throw new AggregateException(errors);
    }
}
