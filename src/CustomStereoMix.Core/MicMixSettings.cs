namespace CustomStereoMix.Core;

public sealed record MicMixSettings(bool Enabled = false, float GainDb = 0)
{
    public static MicMixSettings Default { get; } = new();
}
