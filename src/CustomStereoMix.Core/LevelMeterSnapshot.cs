namespace CustomStereoMix.Core;

public sealed record LevelMeterSnapshot(float Rms, float Peak, IReadOnlyList<float> Waveform)
{
    public static LevelMeterSnapshot Silent { get; } = new(0, 0, Array.Empty<float>());
}
