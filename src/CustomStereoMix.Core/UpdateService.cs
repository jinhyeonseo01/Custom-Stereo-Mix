namespace CustomStereoMix.Core;

public enum UpdateCheckState { NotConfigured, NoRelease, UpToDate, Available }

public sealed record UpdateCheckResult(UpdateCheckState State, string Message, string? LatestVersion = null, Uri? ReleasePage = null, Uri? InstallerUrl = null);

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
            "업데이트 배포 준비 중입니다. 배포가 시작되면 여기에서 확인할 수 있습니다."));
    }
}
