using System.Net;
using System.Text.Json;
using CustomStereoMix.Core;
using CustomStereoMix.State;

static class GitHubUpdateTests
{
    public static void ReleaseTags()
    {
        Assert(ReleaseVersion.TryParseTag("v1.10.0", out var ten), "Valid tag rejected");
        Assert(ReleaseVersion.TryParseTag("v1.9.9", out var nine) && ten > nine, "String ordering used");
        Assert(ReleaseVersion.TryParseTag("v65535.0.0", out _), "Assembly boundary rejected");
        foreach (var invalid in new[] { null, "1.2.3", "V1.2.3", "v01.2.3", "v1.2.3-beta", "v1.2.3.4", "v65536.0.0", "v-1.2.3", "v1.2.3\n" })
            Assert(!ReleaseVersion.TryParseTag(invalid, out _), $"Invalid tag accepted: {invalid}");
    }

    public static void ReleaseResponses() => Run(async () =>
    {
        async Task<UpdateCheckResult> Check(string tag, bool asset = true, string? url = null, bool draft = false, bool prerelease = false)
        {
            using var client = new HttpClient(new Handler((request, _) =>
            {
                Assert(request.RequestUri?.AbsoluteUri == $"https://api.github.com/repos/{GitHubReleaseUpdateService.Repository}/releases/latest", "Wrong API repository");
                Assert(request.Headers.UserAgent.Count > 0, "GitHub User-Agent missing");
                return Task.FromResult(JsonResponse(ReleaseJson(tag, asset, url, draft, prerelease)));
            }));
            return await new GitHubReleaseUpdateService(client, new Version(1, 9, 9, 0)).CheckAsync();
        }
        var available = await Check("v1.10.0");
        Assert(available.State == UpdateCheckState.Available && available.LatestVersion == "v1.10.0", "New numeric version not found");
        Assert(available.InstallerUrl == Installer("v1.10.0"), "Installer link missing");
        Assert((await Check("v1.9.9")).State == UpdateCheckState.UpToDate, "Same version offered");
        Assert((await Check("v1.8.0")).State == UpdateCheckState.UpToDate, "Downgrade offered");
        Assert((await Check("v1.10.0", asset: false)).State == UpdateCheckState.NoRelease, "Incomplete installer release offered");
        Assert((await Check("v1.10.0", draft: true)).State == UpdateCheckState.NoRelease, "Draft offered");
        Assert((await Check("v1.10.0", prerelease: true)).State == UpdateCheckState.NoRelease, "Prerelease offered");
        foreach (var unsafeUrl in new[] { "https://example.com/setup.exe", "https://github.com/other/repo/releases/download/v1.10.0/setup.exe" })
        {
            try { await Check("v1.10.0", url: unsafeUrl); throw new Exception("Unsafe installer URL accepted"); }
            catch (InvalidDataException) { }
        }
        try { await Check("v1.10.0-beta", asset: false); throw new Exception("Malformed version accepted"); }
        catch (InvalidDataException) { }
    });

    public static void ErrorResponses() => Run(async () =>
    {
        foreach (var status in new[] { HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.BadGateway })
        {
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status))));
            var service = new GitHubReleaseUpdateService(client, new Version(1, 0, 0));
            if (status == HttpStatusCode.NotFound)
            {
                Assert((await service.CheckAsync()).State == UpdateCheckState.NoRelease, "No-release response claimed latest");
                continue;
            }
            try { await service.CheckAsync(); throw new Exception("HTTP error ignored"); }
            catch (HttpRequestException) { }
        }
        using var invalidJsonClient = new HttpClient(new Handler((_, _) => Task.FromResult(JsonResponse("not JSON"))));
        try { await new GitHubReleaseUpdateService(invalidJsonClient, new Version(1, 0, 0)).CheckAsync(); throw new Exception("Invalid JSON accepted"); }
        catch (JsonException) { }
        using var slowClient = new HttpClient(new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return JsonResponse("{}"); }));
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        try { await new GitHubReleaseUpdateService(slowClient, new Version(1, 0, 0)).CheckAsync(cancel.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
    });

    public static void StartupPopup() => Run(async () =>
    {
        var updates = new Updates(_ => Task.FromResult(Available()));
        var vm = Model(updates); await vm.InitializeAsync();
        await vm.CheckUpdatesOnStartupAsync();
        Assert(vm.IsUpdateDialogOpen && updates.Calls == 1, "Startup did not show update");
        vm.DismissUpdate(); await vm.CheckUpdatesOnStartupAsync();
        Assert(!vm.IsUpdateDialogOpen && updates.Calls == 1, "Startup repeated on reload");
        await vm.CheckUpdatesCommand.ExecuteAsync(null);
        Assert(vm.IsUpdateDialogOpen && updates.Calls == 2, "Manual check did not reopen update");
        await vm.ShutdownAsync();
    });

    public static void StartupFailure() => Run(async () =>
    {
        foreach (var result in new[] { new UpdateCheckResult(UpdateCheckState.NoRelease, "없음"), new UpdateCheckResult(UpdateCheckState.UpToDate, "최신") })
        {
            var vm = Model(new Updates(_ => Task.FromResult(result))); await vm.InitializeAsync(); await vm.CheckUpdatesOnStartupAsync();
            Assert(!vm.IsUpdateDialogOpen && vm.IsStartEnabled, "Unnecessary startup popup or disabled audio");
            await vm.ShutdownAsync();
        }
        var failing = Model(new Updates(_ => throw new HttpRequestException("Offline")));
        await failing.InitializeAsync(); await failing.CheckUpdatesOnStartupAsync();
        Assert(!failing.IsUpdateDialogOpen && failing.IsStartEnabled && failing.UpdateMessage.Contains("실패"), "Network failure interrupted startup");
        await failing.ShutdownAsync();
    });

    public static void CommandGate() => Run(async () =>
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<UpdateCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updates = new Updates(async token => { entered.TrySetResult(); return await release.Task.WaitAsync(token); });
        var router = new OrderedRouter(); var vm = Model(updates, router); await vm.InitializeAsync();
        var bridge = new UiBridge(vm);
        await bridge.HandleAsync(Message("checkUpdates")).WaitAsync(TimeSpan.FromSeconds(1));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert(vm.IsCheckingUpdates, "Network request was not active");
        await bridge.HandleAsync(Message("start")).WaitAsync(TimeSpan.FromSeconds(1));
        Assert(router.State == RouteState.Running, "Network blocked Start");
        await bridge.HandleAsync(Message("stop")).WaitAsync(TimeSpan.FromSeconds(1));
        Assert(router.State == RouteState.Stopped, "Network blocked Stop");
        Assert(await vm.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(1)), "Shutdown did not cancel network check");
        Assert(!vm.IsUpdateDialogOpen && !vm.IsCheckingUpdates, "Canceled check produced a popup");
    });

    public static void LinkValidation() => Run(async () =>
    {
        foreach (var url in new[] { "https://example.com", "http://github.com/jinhyeonseo01/Custom-Stereo-Mix/releases", "https://github.com/jinhyeonseo01/Custom-Stereo-Mix-other/releases", "https://github.com@evil.example/jinhyeonseo01/Custom-Stereo-Mix/releases", "file:///C:/setup.exe" })
            Assert(!GitHubReleaseUpdateService.IsReleaseLink(new Uri(url)), "External link accepted");
        var vm = Model(new Updates(_ => Task.FromResult(Available()))); await vm.InitializeAsync(); await vm.CheckUpdatesOnStartupAsync();
        var opened = new List<Uri>();
        var bridge = new UiBridge(vm, openUpdateWebsite: uri => { opened.Add(uri); return Task.CompletedTask; });
        await bridge.HandleAsync(Message("downloadUpdate"));
        await bridge.HandleAsync(Message("openRelease"));
        Assert(opened.Count == 2 && opened[0] == Installer("v1.10.0") && opened[1].AbsolutePath.EndsWith("/tag/v1.10.0"), "Bridge used unvalidated frontend URL");
        await bridge.HandleAsync(Message("dismissUpdate")); Assert(!vm.IsUpdateDialogOpen, "Dialog was not dismissed");
        await vm.ShutdownAsync();
    });

    static MainWindowViewModel Model(IUpdateService updates, IAudioRouteController? router = null) => new(new FakeDevices(), router ?? new OrderedRouter(), new MemoryStore(), updates);
    static JsonElement Message(string command) => JsonSerializer.SerializeToElement(new { id = Guid.NewGuid().ToString("N"), command });
    static Uri Installer(string tag) => new($"https://github.com/{GitHubReleaseUpdateService.Repository}/releases/download/{tag}/CustomStereoMix-{tag[1..]}-Setup-x64.exe");
    static UpdateCheckResult Available() => new(UpdateCheckState.Available, "새 버전", "v1.10.0", new Uri($"https://github.com/{GitHubReleaseUpdateService.Repository}/releases/tag/v1.10.0"), Installer("v1.10.0"));
    static string ReleaseJson(string tag, bool asset, string? url, bool draft, bool prerelease) => JsonSerializer.Serialize(new
    {
        tag_name = tag, draft, prerelease,
        assets = asset ? new[] { new { name = $"CustomStereoMix-{tag[1..]}-Setup-x64.exe", state = "uploaded", size = 100, browser_download_url = url ?? Installer(tag).AbsoluteUri } } : [],
    });
    static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
    static void Run(Func<Task> body) => body().GetAwaiter().GetResult();
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => response(request, token);
    }
    sealed class Updates(Func<CancellationToken, Task<UpdateCheckResult>> check) : IUpdateService
    {
        public int Calls;
        public Task<UpdateCheckResult> CheckAsync(CancellationToken token = default) { Calls++; return check(token); }
    }
    sealed class MemoryStore : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AppSettings());
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
