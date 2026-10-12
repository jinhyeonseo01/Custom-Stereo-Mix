using NAudio.Dmo;
using NAudio.Wave;
using System.Numerics;

namespace CustomStereoMix.Audio;

public static class AudioFormatConverter
{
    public static float[] ToInterleavedFloat(ReadOnlySpan<byte> source, WaveFormat sourceFormat, int targetSampleRate, int targetChannels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSampleRate);
        if (targetChannels is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(targetChannels));
        if (sourceFormat.Channels <= 0 || sourceFormat.SampleRate <= 0 || sourceFormat.BlockAlign <= 0)
        {
            return Array.Empty<float>();
        }

        if (sourceFormat.SampleRate != targetSampleRate)
        {
            var converter = new StreamingAudioFormatConverter(sourceFormat, targetSampleRate, targetChannels);
            return [.. converter.Convert(source), .. converter.Flush()];
        }

        var sourceFrames = source.Length / sourceFormat.BlockAlign;
        if (sourceFrames <= 0)
        {
            return Array.Empty<float>();
        }

        var targetFrames = sourceFrames;
        var output = new float[targetFrames * targetChannels];
        var matrix = CreateDownmixMatrix(sourceFormat, targetChannels);

        for (var frame = 0; frame < targetFrames; frame++)
        {
            var sourceFrame = frame;
            for (var channel = 0; channel < targetChannels; channel++)
            {
                var value = 0f;
                for (var sourceChannel = 0; sourceChannel < sourceFormat.Channels; sourceChannel++)
                    value += ReadSample(source, sourceFormat, sourceFrame, sourceChannel) * matrix[channel, sourceChannel];
                output[(frame * targetChannels) + channel] = value;
            }
        }

        return output;
    }

    private static float[,] CreateDownmixMatrix(WaveFormat format, int targetChannels)
    {
        var matrix = new float[targetChannels, format.Channels];
        if (format.Channels == 1)
        {
            for (var target = 0; target < targetChannels; target++) matrix[target, 0] = 1;
            return matrix;
        }
        if (format.Channels == 2)
        {
            if (targetChannels == 1) matrix[0, 0] = matrix[0, 1] = 0.5f;
            else { matrix[0, 0] = 1; matrix[1, 1] = 1; }
            return matrix;
        }
        var mask = GetChannelMask(format);
        var sourceIndex = 0;
        for (var bit = 0; bit < 32 && sourceIndex < format.Channels; bit++)
        {
            var speaker = 1u << bit;
            if ((mask & speaker) == 0) continue;
            var (left, right) = speaker switch
            {
                0x1 => (1f, 0f), // front left
                0x2 => (0f, 1f), // front right
                0x10 or 0x40 or 0x200 or 0x1000 or 0x8000 => (0.70710678f, 0f),
                0x20 or 0x80 or 0x400 or 0x4000 or 0x20000 => (0f, 0.70710678f),
                0x4 => (0.70710678f, 0.70710678f), // center/dialogue
                _ => (0.5f, 0.5f) // LFE, back/top center, unspecified speakers
            };
            if (targetChannels == 1) matrix[0, sourceIndex] = (left + right) * 0.5f;
            else { matrix[0, sourceIndex] = left; matrix[1, sourceIndex] = right; }
            sourceIndex++;
        }
        for (; sourceIndex < format.Channels; sourceIndex++)
            for (var target = 0; target < targetChannels; target++) matrix[target, sourceIndex] = 0.5f;
        // Leave headroom when multiple full-scale channels play together.
        for (var target = 0; target < targetChannels; target++)
        {
            var total = 0f;
            for (var channel = 0; channel < format.Channels; channel++) total += matrix[target, channel];
            for (var channel = 0; channel < format.Channels; channel++) matrix[target, channel] /= Math.Max(1, total);
        }
        return matrix;
    }

    private static uint GetChannelMask(WaveFormat format)
    {
        if (format is WaveFormatExtensible extensible)
        {
            // NAudio 2.x doesn't expose dwChannelMask; Serialize writes the RIFF size
            // then WAVEFORMATEXTENSIBLE, whose mask starts at byte 4 + 20.
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            extensible.Serialize(writer);
            var bytes = stream.ToArray();
            var mask = BitConverter.ToUInt32(bytes, 24);
            if (BitOperations.PopCount(mask) == format.Channels) return mask;
        }
        return format.Channels switch
        {
            3 => 0x7, 4 => 0x33, 5 => 0x37, 6 => 0x3F, 7 => 0x70F, 8 => 0x63F,
            _ => format.Channels < 32 ? (1u << format.Channels) - 1 : uint.MaxValue
        };
    }

    private static float ReadSample(ReadOnlySpan<byte> source, WaveFormat format, int frame, int channel)
    {
        var bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
        var offset = (frame * format.BlockAlign) + (channel * bytesPerSample);
        if (offset + bytesPerSample > source.Length)
        {
            return 0;
        }

        if (IsIeeeFloat(format) && bytesPerSample == 4)
        {
            var sample = BitConverter.ToSingle(source.Slice(offset, 4));
            return float.IsFinite(sample) ? Math.Clamp(sample, -1f, 1f) : 0;
        }

        if (!IsPcm(format))
        {
            return 0;
        }

        return bytesPerSample switch
        {
            1 => (source[offset] - 128) / 128f,
            2 => BitConverter.ToInt16(source.Slice(offset, 2)) / 32768f,
            3 => ReadInt24(source.Slice(offset, 3)) / 8388608f,
            4 => BitConverter.ToInt32(source.Slice(offset, 4)) / 2147483648f,
            _ => 0
        };
    }

    private static int ReadInt24(ReadOnlySpan<byte> bytes)
    {
        var value = bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }

        return value;
    }

    private static bool IsIeeeFloat(WaveFormat format) =>
        format.Encoding == WaveFormatEncoding.IeeeFloat ||
        (format is WaveFormatExtensible extensible && extensible.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT);

    private static bool IsPcm(WaveFormat format) =>
        format.Encoding == WaveFormatEncoding.Pcm ||
        (format is WaveFormatExtensible extensible && extensible.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_PCM);
}
