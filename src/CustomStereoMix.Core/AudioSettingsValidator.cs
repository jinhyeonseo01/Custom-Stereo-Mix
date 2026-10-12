namespace CustomStereoMix.Core;

public static class AudioSettingsValidator
{
    public static void Validate(AudioEngineSettings? engine, ProcessingSettings? processing, MicMixSettings? mic)
    {
        if (engine is null || processing is null || mic is null || engine.SampleRate is < 22050 or > 192000 ||
            engine.Channels is < 1 or > 2 || engine.LatencyMilliseconds is < 10 or > 250 ||
            engine.BufferMilliseconds is < 80 or > 1000 || engine.BufferMilliseconds < engine.LatencyMilliseconds ||
            !float.IsFinite(processing.GainDb) || processing.GainDb is < -12 or > 12 ||
            !float.IsFinite(processing.CompressorThresholdDb) || processing.CompressorThresholdDb is < -40 or > -3 ||
            !float.IsFinite(processing.CompressorRatio) || processing.CompressorRatio is < 1 or > 10 ||
            !float.IsFinite(mic.GainDb) || mic.GainDb is < -12 or > 12)
            throw new InvalidDataException("오디오 설정 범위를 확인해 주세요. 버퍼 용량은 지연 값 이상이어야 합니다.");
    }
}
