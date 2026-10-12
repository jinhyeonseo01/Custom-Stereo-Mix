using System.Text.RegularExpressions;

namespace CustomStereoMix.Core;

public static partial class ReleaseVersion
{
    [GeneratedRegex(@"^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (tag is null || tag.Length > 20 || !TagPattern().IsMatch(tag) ||
            !Version.TryParse(tag[1..], out var parsed) ||
            parsed.Major > ushort.MaxValue || parsed.Minor > ushort.MaxValue || parsed.Build > ushort.MaxValue)
            return false;
        version = parsed;
        return true;
    }
}
