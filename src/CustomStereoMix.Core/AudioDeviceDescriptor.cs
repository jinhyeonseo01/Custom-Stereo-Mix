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
    public string? ReturnPathGroup => VirtualCableClassifier.ReturnPathGroup(Name);
    public string Summary
    {
        get
        {
            var defaultBadge = IsDefault ? " · Default" : string.Empty;
            return $"{SampleRate / 1000.0:0.##} kHz · {Channels} ch{defaultBadge}";
        }
    }

    public string DisplayName => $"{Name}  ({Summary})";
}
