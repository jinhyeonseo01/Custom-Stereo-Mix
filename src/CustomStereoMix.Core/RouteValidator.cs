namespace CustomStereoMix.Core;

public static class RouteValidator
{
    public static string? GetError(AudioDeviceDescriptor? source, AudioDeviceDescriptor? target,
        AudioDeviceDescriptor? microphone, bool microphoneEnabled)
    {
        if (source is null || target is null)
            return "재생 장치와 보낼 장치를 선택하세요.";
        if (string.Equals(source.Id, target.Id, StringComparison.OrdinalIgnoreCase))
            return "되먹임을 방지하기 위해 재생 장치와 보낼 장치를 다르게 선택하세요.";
        if (!microphoneEnabled)
            return null;
        if (microphone is null)
            return "마이크를 선택한 뒤 혼합을 켜세요.";
        // Cable endpoint pairing is not reliably exposed by every driver. Reject virtual
        // recording inputs for microphone mix rather than risk feeding output back in.
        if (microphone.IsVirtualCableCandidate)
            return "마이크 혼합에는 실제 마이크를 선택하세요. 가상 케이블은 되먹임을 만들 수 있습니다.";
        return null;
    }
}
