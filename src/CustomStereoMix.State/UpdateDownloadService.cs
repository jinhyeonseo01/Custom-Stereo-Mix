using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CustomStereoMix.Core;

namespace CustomStereoMix.State;

public sealed class UpdateDownloadService(HttpClient client, string cacheDirectory) : IUpdateDownloadService
{
    public async Task<DownloadedUpdate> DownloadAsync(UpdateCheckResult release, IProgress<UpdateDownloadProgress> progress, CancellationToken cancellationToken)
    {
        if (release.State != UpdateCheckState.Available || !ReleaseVersion.TryParseTag(release.LatestVersion, out var version))
            throw new InvalidDataException().WithMessage(MessageKey.InvalidInstallerLink);
        var name = $"CustomStereoMix-{version.ToString(3)}-Setup-x64.exe";
        var expected = new Uri($"https://github.com/{GitHubReleaseUpdateService.Repository}/releases/download/{release.LatestVersion}/{name}");
        if (release.InstallerUrl != expected) throw new InvalidDataException().WithMessage(MessageKey.InvalidInstallerLink);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var token = timeout.Token;
        var directory = Path.Combine(cacheDirectory, release.LatestVersion!);
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, name);
        var partial = destination + ".partial";
        try
        {
            using var checksumResponse = await client.GetAsync(expected.AbsoluteUri + ".sha256", HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            checksumResponse.EnsureSuccessStatusCode();
            await using var checksumStream = await checksumResponse.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            var checksumBytes = new byte[4097];
            var count = 0;
            while (count < checksumBytes.Length)
            {
                var read = await checksumStream.ReadAsync(checksumBytes.AsMemory(count), token).ConfigureAwait(false);
                if (read == 0) break;
                count += read;
            }
            var match = Regex.Match(System.Text.Encoding.UTF8.GetString(checksumBytes, 0, count).Trim(), @"\A([a-fA-F0-9]{64})\s+\*?" + Regex.Escape(name) + @"\z");
            if (count > 4096 || !match.Success) throw new InvalidDataException().WithMessage(MessageKey.UpdateChecksumFailed);
            var hash = match.Groups[1].Value.ToLowerInvariant();
            using var response = await client.GetAsync(expected, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            const long maximumSize = 512 * 1024 * 1024;
            var total = response.Content.Headers.ContentLength;
            if (total is <= 0 or > maximumSize) throw new InvalidDataException().WithMessage(MessageKey.UpdateDownloadFailed);
            await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920];
                long received = 0;
                var lastReport = Environment.TickCount64;
                progress.Report(new(0, total));
                int read;
                while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    received += read;
                    if (received > maximumSize) throw new InvalidDataException().WithMessage(MessageKey.UpdateDownloadFailed);
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    if (Environment.TickCount64 - lastReport >= 100) { progress.Report(new(received, total)); lastReport = Environment.TickCount64; }
                }
                if (received == 0 || total is not null && received != total) throw new InvalidDataException().WithMessage(MessageKey.UpdateDownloadFailed);
                progress.Report(new(received, total, true));
            }
            await using var file = File.OpenRead(partial);
            var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, token).ConfigureAwait(false));
            if (actual != hash) throw new InvalidDataException().WithMessage(MessageKey.UpdateChecksumFailed);
            file.Close();
            File.Move(partial, destination, overwrite: true);
            return new(destination, hash);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException().WithMessage(MessageKey.UpdateTimeout); }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
}
