using NAudio.Dsp;
using NAudio.Wave;

namespace CustomStereoMix.Audio;

/// <summary>One converter per capture stream; retains filter history and fractional sample position.</summary>
public sealed class StreamingAudioFormatConverter
{
    private readonly WaveFormat _sourceFormat;
    private readonly int _targetRate;
    private readonly int _channels;
    private readonly WdlResampler? _resampler;
    private long _inputFrames;
    private long _outputFrames;
    private bool _finished;

    public StreamingAudioFormatConverter(WaveFormat sourceFormat, int targetSampleRate, int targetChannels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSampleRate);
        if (sourceFormat.SampleRate <= 0 || sourceFormat.BlockAlign <= 0) throw new ArgumentException("Invalid capture format.", nameof(sourceFormat));
        if (targetChannels is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(targetChannels));
        _sourceFormat = sourceFormat;
        _targetRate = targetSampleRate;
        _channels = targetChannels;
        if (sourceFormat.SampleRate == targetSampleRate) return;
        _resampler = new WdlResampler();
        _resampler.SetMode(interp: true, filtercnt: 0, sinc: true);
        _resampler.SetFeedMode(true);
        _resampler.SetRates(sourceFormat.SampleRate, targetSampleRate);
    }

    public float[] Convert(ReadOnlySpan<byte> source)
    {
        if (_finished) throw new InvalidOperationException("The capture stream has been flushed.");
        var mixed = AudioFormatConverter.ToInterleavedFloat(source, _sourceFormat, _sourceFormat.SampleRate, _channels);
        var frames = mixed.Length / _channels;
        _inputFrames += frames;
        if (_resampler is null || frames == 0) return mixed;
        var requested = _resampler.ResamplePrepare(frames, _channels, out var input, out var inputOffset);
        if (requested != frames) throw new InvalidOperationException("Resampler could not accept the capture packet.");
        mixed.CopyTo(input, inputOffset);
        // Include pending filter history; output is trimmed to the returned frame count.
        var capacity = (int)Math.Ceiling((frames + 128) * (double)_targetRate / _sourceFormat.SampleRate) + 8;
        var output = new float[capacity * _channels];
        var produced = _resampler.ResampleOut(output, 0, frames, capacity, _channels);
        _outputFrames += produced;
        Array.Resize(ref output, produced * _channels);
        return output;
    }

    public float[] Flush()
    {
        if (_finished) return [];
        _finished = true;
        if (_resampler is null || _inputFrames == 0) return [];
        var expected = (long)Math.Round(_inputFrames * (double)_targetRate / _sourceFormat.SampleRate);
        var remaining = checked((int)Math.Max(0, expected - _outputFrames));
        if (remaining == 0) return [];
        _resampler.ResamplePrepare(1, _channels, out _, out _);
        var output = new float[remaining * _channels];
        var produced = _resampler.ResampleOut(output, 0, 0, remaining, _channels);
        Array.Resize(ref output, produced * _channels);
        return output;
    }
}
