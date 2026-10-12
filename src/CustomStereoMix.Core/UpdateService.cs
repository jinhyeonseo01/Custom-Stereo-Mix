namespace CustomStereoMix.Core;

public enum UpdateCheckState { NotConfigured, UpToDate, Available }

public sealed record UpdateCheckResult(UpdateCheckState State, string Message, string? LatestVersion = null, Uri? ReleasePage = null);

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>Replace this service with a GitHub Releases provider when a release repository is configured.</summary>
public sealed class UnconfiguredUpdateService : IUpdateService
{
    public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new UpdateCheckResult(UpdateCheckState.NotConfigured,
            "업데이트 배포 준비 중입니다. 배포가 시작되면 여기에서 확인할 수 있습니다."));
    }
}
