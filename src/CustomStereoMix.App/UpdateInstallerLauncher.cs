using System.Diagnostics;
using System.Security.Cryptography;
using CustomStereoMix.Core;

namespace CustomStereoMix.App;

public static class UpdateInstallerLauncher
{
    public static string CacheDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CustomStereoMix", "Updates");

    public static void StartHelper(DownloadedUpdate update, string language)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "CustomStereoMix.App.exe")) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--launch-installer", update.Path, update.Sha256, Environment.ProcessId.ToString(), language }) start.ArgumentList.Add(argument);
        using var helper = Process.Start(start) ?? throw new InvalidOperationException("Could not start update helper.");
    }

    public static async Task RunHelperAsync(string[] arguments)
    {
        var localizer = new Localizer(LanguageCatalog.Resolve(arguments.Length == 5 ? arguments[4] : null));
        try
        {
            if (arguments.Length != 5 || !int.TryParse(arguments[3], out var pid) || pid == Environment.ProcessId) throw new InvalidDataException();
            var path = Path.GetFullPath(arguments[1]);
            if (!path.StartsWith(Path.GetFullPath(CacheDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !path.EndsWith("-Setup-x64.exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException();
            try
            {
                using var parent = Process.GetProcessById(pid);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await parent.WaitForExitAsync(timeout.Token);
            }
            catch (ArgumentException) { /* Parent has already exited. */ }
            await using var file = File.OpenRead(path);
            if (!string.Equals(Convert.ToHexStringLower(await SHA256.HashDataAsync(file)), arguments[2], StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException().WithMessage(MessageKey.UpdateChecksumFailed);
            file.Close();
            var setupLanguage = arguments[4] switch { "ko-KR" => "korean", "ja-JP" => "japanese", "zh-CN" => "chinesesimplified", _ => "english" };
            Process.Start(new ProcessStartInfo(path, $"/LANG={setupLanguage}") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(localizer.Text(MessageKey.UpdateLaunchFailed, ex), "Custom Stereo Mix", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
