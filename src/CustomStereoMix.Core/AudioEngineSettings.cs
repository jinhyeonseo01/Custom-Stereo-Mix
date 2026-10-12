namespace CustomStereoMix.Core;

public sealed record AudioEngineSettings(
    int SampleRate = 48000,
    int Channels = 2,
    int LatencyMilliseconds = 50,
    int BufferMilliseconds = 250)
{
    public static AudioEngineSettings Default { get; } = new();

    // Round whole frames first; an odd stereo capacity can swap L/R after overflow.
    public int BufferSamples => Math.Max(1, (int)((long)SampleRate * BufferMilliseconds / 1000)) * Channels;
}
