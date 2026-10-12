namespace CustomStereoMix.Audio;

/// <summary>Frame-aligned, independent consumer with bounded clock drift correction.
/// The small variable-rate conversion uses cubic interpolation, not a UI timer.</summary>
public sealed class AdaptiveAudioBuffer
{
    private readonly float[] _samples;
    private readonly object _gate = new();
    private readonly int _channels, _capacityFrames, _targetFrames;
    private long _write, _overruns, _underruns, _dropped;
    private double _read, _rate = 1;
    private bool _started;
    public AdaptiveAudioBuffer(int capacitySamples, int channels, int targetFrames)
    {
        if (channels < 1 || capacitySamples < channels * 8) throw new ArgumentOutOfRangeException(nameof(capacitySamples));
        _channels = channels; _capacityFrames = capacitySamples / channels;
        _targetFrames = Math.Clamp(targetFrames, 4, _capacityFrames / 2);
        _samples = new float[_capacityFrames * channels];
    }
    public AdaptiveBufferStatistics Statistics
    {
        get { lock (_gate) return new((int)Math.Max(0, _write - _read), (_rate - 1) * 1e6, _underruns, _overruns, _dropped); }
    }
    public void Clear()
    {
        lock (_gate) { Array.Clear(_samples); _read = _write = 0; _rate = 1; _started = false; }
    }
    public void Write(ReadOnlySpan<float> samples)
    {
        if (samples.Length % _channels != 0) throw new ArgumentException("Partial audio frame.");
        lock (_gate)
        {
            for (var i = 0; i < samples.Length; i += _channels)
            {
                var slot = (int)(_write % _capacityFrames) * _channels;
                samples.Slice(i, _channels).CopyTo(_samples.AsSpan(slot, _channels));
                _write++;
            }
            var oldest = Math.Max(0, _write - _capacityFrames + 2);
            if (_read < oldest) { _overruns++; _dropped += (long)Math.Ceiling(oldest - _read); _read = oldest; }
        }
    }
    public void Read(Span<float> destination, bool compensate = true)
    {
        if (destination.Length % _channels != 0) throw new ArgumentException("Partial audio frame.");
        lock (_gate)
        {
            destination.Clear();
            if (!_started)
            {
                if (_write - _read < _targetFrames + destination.Length / _channels + 2) return;
                _started = true;
            }
            if (compensate)
            {
                var error = (_write - _read - _targetFrames - destination.Length / _channels) / _targetFrames;
                var desired = 1 + Math.Clamp(error * .001, -.002, .002);
                _rate += (desired - _rate) * .02;
            }
            else _rate = 1;
            for (var i = 0; i < destination.Length; i += _channels)
            {
                if (_read + 2 >= _write) { _underruns++; _read = _write; _started = false; _rate = 1; break; }
                var frame = (long)_read; var t = (float)(_read - frame);
                for (var c = 0; c < _channels; c++)
                {
                    var a = Sample(frame - 1, c); var b = Sample(frame, c);
                    var d = Sample(frame + 1, c); var e = Sample(frame + 2, c);
                    // Catmull-Rom; identity at integer sample positions.
                    destination[i + c] = b + .5f * t * (d - a + t * (2 * a - 5 * b + 4 * d - e + t * (3 * (b - d) + e - a)));
                }
                _read += _rate;
            }
        }
    }
    private float Sample(long frame, int channel) => frame < Math.Max(0, _write - _capacityFrames) || frame >= _write
        ? 0 : _samples[(int)(frame % _capacityFrames) * _channels + channel];
}
public readonly record struct AdaptiveBufferStatistics(int QueuedFrames, double CorrectionPpm, long Underruns, long Overruns, long DroppedFrames);
