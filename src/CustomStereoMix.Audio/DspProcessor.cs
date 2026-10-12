using CustomStereoMix.Core;

namespace CustomStereoMix.Audio;

public static class DspProcessor
{
    public static void Apply(Span<float> samples, ProcessingSettings settings)
    {
        var gain = DbToLinear(settings.GainDb);
        var threshold = DbToLinear(settings.CompressorThresholdDb);
        var ratio = Math.Max(1, settings.CompressorRatio);

        for (var i = 0; i < samples.Length; i++)
        {
            var value = samples[i] * gain;
            if (settings.CompressorEnabled)
            {
                value = Compress(value, threshold, ratio);
            }

            samples[i] = settings.LimiterEnabled ? Math.Clamp(value, -1f, 1f) : value;
        }
    }

    public static LevelMeterSnapshot Meter(ReadOnlySpan<float> samples, int points = 96)
    {
        if (samples.IsEmpty)
        {
            return LevelMeterSnapshot.Silent;
        }

        double sum = 0;
        var peak = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var abs = Math.Abs(samples[i]);
            peak = Math.Max(peak, abs);
            sum += samples[i] * samples[i];
        }

        var rms = (float)Math.Sqrt(sum / samples.Length);
        return new LevelMeterSnapshot(Math.Clamp(rms, 0, 1), Math.Clamp(peak, 0, 1), Downsample(samples, points));
    }

    public static float DbToLinear(float db) => (float)Math.Pow(10, db / 20);

    private static float Compress(float value, float threshold, float ratio)
    {
        var abs = Math.Abs(value);
        if (abs <= threshold)
        {
            return value;
        }

        var compressed = threshold + ((abs - threshold) / ratio);
        return value < 0 ? -compressed : compressed;
    }

    private static float[] Downsample(ReadOnlySpan<float> samples, int points)
    {
        var output = new float[Math.Max(1, points)];
        if (samples.IsEmpty)
        {
            return output;
        }

        for (var i = 0; i < output.Length; i++)
        {
            var start = i * samples.Length / output.Length;
            var end = Math.Max(start + 1, (i + 1) * samples.Length / output.Length);
            var peak = 0f;
            for (var sample = start; sample < end && sample < samples.Length; sample++)
            {
                peak = Math.Max(peak, Math.Abs(samples[sample]));
            }

            output[i] = peak;
        }

        return output;
    }
}
