using CustomStereoMix.Core;
using NAudio.CoreAudioApi;

namespace CustomStereoMix.Audio;

public sealed class NaudioDeviceService : IAudioDeviceService, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public IReadOnlyList<AudioDeviceDescriptor> GetLoopbackSources() =>
        Enumerate(DataFlow.Render, AudioDeviceRole.LoopbackSource, virtualOnly: false);

    public IReadOnlyList<AudioDeviceDescriptor> GetRenderTargets() =>
        Enumerate(DataFlow.Render, AudioDeviceRole.RenderTarget, virtualOnly: false)
            .OrderByDescending(device => device.IsVirtualCableCandidate)
            .ThenBy(device => device.VirtualCableKind == VirtualCableKind.None)
            .ThenByDescending(device => device.IsDefault)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public IReadOnlyList<AudioDeviceDescriptor> GetMicrophones() =>
        Enumerate(DataFlow.Capture, AudioDeviceRole.Microphone, virtualOnly: false);

    public MMDevice GetDevice(string id) => _enumerator.GetDevice(id);

    public void Dispose() => _enumerator.Dispose();

    private IReadOnlyList<AudioDeviceDescriptor> Enumerate(DataFlow flow, AudioDeviceRole role, bool virtualOnly)
    {
        var defaultId = TryGetDefaultId(flow);
        var devices = _enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
        var results = new List<AudioDeviceDescriptor>();

        foreach (var device in devices)
        {
            using var ownedDevice = device;
            var cableKind = VirtualCableClassifier.Classify(device.FriendlyName);
            if (virtualOnly && cableKind == VirtualCableKind.None)
            {
                continue;
            }

            var format = TryGetMixFormat(device);
            results.Add(new AudioDeviceDescriptor(
                device.ID,
                device.FriendlyName,
                role,
                Math.Max(1, format?.Channels ?? 2),
                Math.Max(1, format?.SampleRate ?? 48000),
                string.Equals(device.ID, defaultId, StringComparison.OrdinalIgnoreCase),
                cableKind != VirtualCableKind.None,
                cableKind));
        }

        return results
            .OrderByDescending(device => device.IsDefault)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private string? TryGetDefaultId(DataFlow flow)
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            return device.ID;
        }
        catch
        {
            return null;
        }
    }

    private static NAudio.Wave.WaveFormat? TryGetMixFormat(MMDevice device)
    {
        try
        {
            return device.AudioClient.MixFormat;
        }
        catch
        {
            return null;
        }
    }
}
