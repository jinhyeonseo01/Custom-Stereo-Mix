namespace CustomStereoMix.Audio;

internal static class RouteDiagnosticLog
{
    private static readonly object Gate = new();
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CustomStereoMix");

    public static void Write(string message)
    {
        // Called on route setup/cleanup or a timer, never on the audio data callback.
        // A missing/unwritable log must not prevent audio from starting or stopping.
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, "audio-diagnostics.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1_048_576)
                    File.Move(path, path + ".1", overwrite: true);
                var line = message.Replace('\r', ' ').Replace('\n', ' ');
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {line}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
