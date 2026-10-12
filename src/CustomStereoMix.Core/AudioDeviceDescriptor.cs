namespace CustomStereoMix.Core;

public sealed record AudioDeviceDescriptor(
    string Id,
    string Name,
    AudioDeviceRole Role,
    int Channels,
    int SampleRate,
    bool IsDefault,
    bool IsVirtualCableCandidate,
    VirtualCableKind VirtualCableKind)
{
    public string Summary
    {
        get
        {
            var defaultBadge = IsDefault ? " · 기본" : string.Empty;
            return $"{SampleRate / 1000.0:0.##} kHz · {Channels}채널{defaultBadge}";
        }
    }

    public string DisplayName => $"{Name}  ({Summary})";
}
