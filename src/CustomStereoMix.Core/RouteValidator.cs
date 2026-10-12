namespace CustomStereoMix.Core;

public static class RouteValidator
{
    public static MessageKey? GetError(AudioDeviceDescriptor? source, AudioDeviceDescriptor? target,
        AudioDeviceDescriptor? microphone, bool microphoneEnabled)
    {
        if (source is null || target is null)
            return MessageKey.SelectRoute;
        if (string.Equals(source.Id, target.Id, StringComparison.OrdinalIgnoreCase))
            return MessageKey.PreventFeedback;
        if (!microphoneEnabled)
            return null;
        if (microphone is null)
            return MessageKey.SelectMicrophone;
        // Cable endpoint pairing is not reliably exposed by every driver. Reject virtual
        // recording inputs for microphone mix rather than risk feeding output back in.
        if (microphone.IsVirtualCableCandidate)
            return MessageKey.PhysicalMicrophone;
        return null;
    }
}
