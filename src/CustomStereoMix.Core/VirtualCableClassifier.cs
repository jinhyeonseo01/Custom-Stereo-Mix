namespace CustomStereoMix.Core;

public static class VirtualCableClassifier
{
    // Known endpoint aliases share a physical cable; unknown names use a
    // conservative driver-family group rather than pretending they are unrelated.
    public static string? ReturnPathGroup(string name)
    {
        var kind = Classify(name);
        if (kind == VirtualCableKind.None) return null;
        var normalized = name.ToUpperInvariant();
        if (kind == VirtualCableKind.VbCable)
        {
            var cable = System.Text.RegularExpressions.Regex.Match(normalized, @"\bCABLE(?:-[A-Z])?\b");
            if (cable.Success) return "vb:" + cable.Value;
        }
        if (kind == VirtualCableKind.VirtualAudioCable)
        {
            var line = System.Text.RegularExpressions.Regex.Match(normalized, @"\bLINE\s*\d+\b");
            if (line.Success) return "vac:" + line.Value.Replace(" ", "");
        }
        return "virtual:" + kind;
    }

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
