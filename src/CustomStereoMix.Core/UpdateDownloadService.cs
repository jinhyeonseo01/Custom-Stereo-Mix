namespace CustomStereoMix.Core;

public sealed record UpdateDownloadProgress(long Received, long? Total, bool Verifying = false);
public sealed record DownloadedUpdate(string Path, string Sha256);
public interface IUpdateDownloadService
{
    Task<DownloadedUpdate> DownloadAsync(UpdateCheckResult release, IProgress<UpdateDownloadProgress> progress, CancellationToken cancellationToken);
}
