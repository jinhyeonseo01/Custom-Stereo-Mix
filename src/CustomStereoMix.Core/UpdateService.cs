namespace CustomStereoMix.Core;

public enum UpdateCheckState { NotConfigured, NoRelease, UpToDate, Available }

public sealed record UpdateCheckResult(UpdateCheckState State, LocalizedMessage Message, string? LatestVersion = null, Uri? ReleasePage = null, Uri? InstallerUrl = null);

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>Offline fallback for hosts which have not supplied a release provider.</summary>
public sealed class UnconfiguredUpdateService : IUpdateService
{
    public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new UpdateCheckResult(UpdateCheckState.NotConfigured,
            new(MessageKey.UpdateUnconfigured)));
    }
}
