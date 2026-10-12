namespace CustomStereoMix.Core;

public sealed record ProcessingSettings(
    float GainDb = 0,
    bool CompressorEnabled = false,
    float CompressorThresholdDb = -12,
    float CompressorRatio = 4,
    bool LimiterEnabled = true)
{
    public static ProcessingSettings Default { get; } = new();
}
