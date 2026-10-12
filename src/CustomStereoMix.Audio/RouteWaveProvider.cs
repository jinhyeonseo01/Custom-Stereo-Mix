using CustomStereoMix.Core;
using NAudio.Wave;

namespace CustomStereoMix.Audio;

internal sealed class RouteWaveProvider : IWaveProvider
{
    private readonly FloatRingBuffer _sourceBuffer;
    private readonly FloatRingBuffer _micBuffer;
    private readonly object _settingsGate = new();
    private readonly TimeSpan _meterInterval = TimeSpan.FromMilliseconds(50);
    private readonly WaveFormat _waveFormat;
    private float[] _sourceScratch = [];
    private float[] _micScratch = [];
    private float[] _mixScratch = [];
    private DateTimeOffset _lastMeterAt = DateTimeOffset.MinValue;
    private ProcessingSettings _processing;
    private MicMixSettings _mic;

    public RouteWaveProvider(FloatRingBuffer sourceBuffer, FloatRingBuffer micBuffer, AudioEngineSettings engine, ProcessingSettings processing, MicMixSettings mic)
    {
        _sourceBuffer = sourceBuffer;
        _micBuffer = micBuffer;
        _processing = processing;
        _mic = mic;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(engine.SampleRate, engine.Channels);
    }

    public event EventHandler<LevelMeterSnapshot>? LevelChanged;

    public WaveFormat WaveFormat => _waveFormat;

    public void UpdateProcessing(ProcessingSettings settings)
    {
        lock (_settingsGate)
        {
            _processing = settings;
        }
    }

    public void UpdateMicrophone(MicMixSettings settings)
    {
        lock (_settingsGate)
        {
            _mic = settings;
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        var sampleCount = count / sizeof(float);
        EnsureScratch(sampleCount);

        _sourceBuffer.Read(_sourceScratch.AsSpan(0, sampleCount));

        ProcessingSettings processing;
        MicMixSettings mic;
        lock (_settingsGate)
        {
            processing = _processing;
            mic = _mic;
        }

        if (mic.Enabled) _micBuffer.Read(_micScratch.AsSpan(0, sampleCount));
        else _micScratch.AsSpan(0, sampleCount).Clear();

        var micGain = mic.Enabled ? DspProcessor.DbToLinear(mic.GainDb) : 0;
        for (var i = 0; i < sampleCount; i++)
        {
            _mixScratch[i] = _sourceScratch[i] + (_micScratch[i] * micGain);
        }

        DspProcessor.Apply(_mixScratch.AsSpan(0, sampleCount), processing);
        Buffer.BlockCopy(_mixScratch, 0, buffer, offset, sampleCount * sizeof(float));
        MaybeEmitMeter(_mixScratch.AsSpan(0, sampleCount));
        return sampleCount * sizeof(float);
    }

    private void MaybeEmitMeter(ReadOnlySpan<float> samples)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastMeterAt < _meterInterval)
        {
            return;
        }

        _lastMeterAt = now;
        LevelChanged?.Invoke(this, DspProcessor.Meter(samples));
    }

    private void EnsureScratch(int sampleCount)
    {
        if (_sourceScratch.Length >= sampleCount)
        {
            return;
        }

        _sourceScratch = new float[sampleCount];
        _micScratch = new float[sampleCount];
        _mixScratch = new float[sampleCount];
    }
}
