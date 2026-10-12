namespace CustomStereoMix.Audio;

public sealed class FloatRingBuffer
{
    private readonly float[] _buffer;
    private readonly object _gate = new();
    private int _readIndex;
    private int _writeIndex;
    private int _count;
    private long _underruns, _overflows, _missingSamples, _droppedSamples;

    public BufferStatistics Statistics
    {
        get { lock (_gate) return new(_count, _underruns, _overflows, _missingSamples, _droppedSamples); }
    }

    public FloatRingBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _buffer = new float[capacity];
    }

    public int Capacity => _buffer.Length;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_buffer);
            _readIndex = 0;
            _writeIndex = 0;
            _count = 0;
            _underruns = _overflows = _missingSamples = _droppedSamples = 0;
        }
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        lock (_gate)
        {
            var dropped = Math.Max(0L, (long)_count + samples.Length - _buffer.Length);
            if (dropped > 0) { _overflows++; _droppedSamples += dropped; }
            if (samples.Length >= _buffer.Length)
            {
                samples = samples[^_buffer.Length..];
                _readIndex = 0;
                _writeIndex = 0;
                _count = 0;
            }

            var overflow = Math.Max(0, _count + samples.Length - _buffer.Length);
            if (overflow > 0)
            {
                _readIndex = (_readIndex + overflow) % _buffer.Length;
                _count -= overflow;
            }

            foreach (var sample in samples)
            {
                _buffer[_writeIndex] = sample;
                _writeIndex = (_writeIndex + 1) % _buffer.Length;
            }

            _count += samples.Length;
        }
    }

    public int Read(Span<float> destination)
    {
        lock (_gate)
        {
            var read = Math.Min(destination.Length, _count);
            if (read < destination.Length) { _underruns++; _missingSamples += destination.Length - read; }
            for (var i = 0; i < read; i++)
            {
                destination[i] = _buffer[_readIndex];
                _readIndex = (_readIndex + 1) % _buffer.Length;
            }

            _count -= read;
            destination[read..].Clear();
            return read;
        }
    }
}

public readonly record struct BufferStatistics(int QueuedSamples, long Underruns, long Overflows, long MissingSamples, long DroppedSamples);
