namespace CustomStereoMix.Core;

public static class VirtualCableClassifier
{
    public static VirtualCableKind Classify(string deviceName)
    {
        var normalized = deviceName.ToLowerInvariant();
        if (normalized.Contains("hifi cable") || normalized.Contains("hi-fi cable"))
        {
            return VirtualCableKind.VbHiFiCable;
        }

        if (normalized.Contains("vb-audio") || normalized.Contains("vb-cable") || normalized.Contains("cable input"))
        {
            return VirtualCableKind.VbCable;
        }

        if (normalized.Contains("voicemeeter"))
        {
            return VirtualCableKind.Voicemeeter;
        }

        if (normalized.Contains("virtual audio cable") || normalized.Contains("line 1") || normalized.Contains("line 2"))
        {
            return VirtualCableKind.VirtualAudioCable;
        }

        if (normalized.Contains("virtual") && normalized.Contains("cable"))
        {
            return VirtualCableKind.UnknownVirtualCable;
        }

        return VirtualCableKind.None;
    }
}
