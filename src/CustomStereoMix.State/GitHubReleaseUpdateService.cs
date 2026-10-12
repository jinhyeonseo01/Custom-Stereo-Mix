using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using CustomStereoMix.Core;

namespace CustomStereoMix.State;

/// <summary>Reads the public stable release; never downloads or executes an installer.</summary>
public sealed class GitHubReleaseUpdateService(HttpClient client, Version installedVersion) : IUpdateService
{
    public const string Repository = "jinhyeonseo01/Custom-Stereo-Mix";
    public static readonly Uri ReleasesPage = new($"https://github.com/{Repository}/releases");
    private static readonly Uri LatestReleaseApi = new($"https://api.github.com/repos/{Repository}/releases/latest");
    private readonly Version _installed = new(installedVersion.Major, installedVersion.Minor, Math.Max(0, installedVersion.Build));

    public static bool IsReleaseLink(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "https" &&
        uri.Host == "github.com" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) &&
        (uri.AbsolutePath == $"/{Repository}/releases" || uri.AbsolutePath.StartsWith($"/{Repository}/releases/", StringComparison.Ordinal));

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CustomStereoMix", _installed.ToString(3)));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(UpdateCheckState.NoRelease, "아직 공개된 정식 릴리즈가 없습니다.", ReleasePage: ReleasesPage);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                throw new HttpRequestException("GitHub 조회가 제한되었습니다. 잠시 후 다시 확인해 주세요.");
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
            var release = document.RootElement;
            if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
                return new(UpdateCheckState.NoRelease, "공개된 정식 릴리즈를 찾지 못했습니다.", ReleasePage: ReleasesPage);
            var tag = release.GetProperty("tag_name").GetString();
            if (!ReleaseVersion.TryParseTag(tag, out var latest))
                throw new InvalidDataException("릴리즈 버전 형식이 올바르지 않습니다. vX.Y.Z 형식이 필요합니다.");
            var page = new Uri($"https://github.com/{Repository}/releases/tag/{tag}");
            if (latest <= _installed)
                return new(UpdateCheckState.UpToDate, "현재 버전에서 사용할 수 있는 새 업데이트가 없습니다.", tag, page);

            var installerName = $"CustomStereoMix-{latest.ToString(3)}-Setup-x64.exe";
            var expectedUrl = new Uri($"https://github.com/{Repository}/releases/download/{tag}/{installerName}");
            if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.GetProperty("name").GetString() != installerName ||
                        asset.GetProperty("state").GetString() != "uploaded" || asset.GetProperty("size").GetInt64() <= 0) continue;
                    if (!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var url) || url != expectedUrl)
                        throw new InvalidDataException("설치기 링크가 지정된 릴리즈 저장소와 일치하지 않습니다.");
                    return new(UpdateCheckState.Available, $"새 버전 {tag}을 사용할 수 있습니다.", tag, page, url);
                }
            }
            return new(UpdateCheckState.NoRelease, $"{tag} 릴리즈의 Windows x64 설치기가 아직 준비되지 않았습니다.", tag, page);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("업데이트 서버 응답이 지연되고 있습니다. 잠시 후 다시 확인해 주세요.");
        }
    }
}
