using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using CustomStereoMix.Core;
using CustomStereoMix.State;

static class UpdateDownloadTests
{
    static readonly UpdateCheckResult Release = new(UpdateCheckState.Available, new(MessageKey.UpdateAvailable, "v9.9.9"), "v9.9.9", InstallerUrl: new("https://github.com/jinhyeonseo01/Custom-Stereo-Mix/releases/download/v9.9.9/CustomStereoMix-9.9.9-Setup-x64.exe"));
    public static void Integrity() => Run(async () =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "stereomix-download-test-" + Guid.NewGuid());
        var data = new byte[170000]; Random.Shared.NextBytes(data);
        var hash = Convert.ToHexStringLower(SHA256.HashData(data));
        try
        {
            foreach (var mode in new[] { "valid", "corrupt", "wrong-name", "oversized-checksum", "truncated", "http-error" })
            {
                using var client = new HttpClient(new Handler((request, _) =>
                {
                    if (mode == "http-error") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                    var checksum = mode == "corrupt" ? new string('0', 64) : hash;
                    var name = mode == "wrong-name" ? "other.exe" : "CustomStereoMix-9.9.9-Setup-x64.exe";
                    var content = request.RequestUri!.AbsoluteUri.EndsWith(".sha256")
                        ? (HttpContent)new StringContent(mode == "oversized-checksum" ? new string('a', 5000) : $"{checksum}  {name}\n") : new ByteArrayContent(data);
                    if (mode == "truncated" && content is ByteArrayContent) content.Headers.ContentLength = data.Length + 1;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
                }));
                var progress = new Reporter();
                try
                {
                    var result = await new UpdateDownloadService(client, directory).DownloadAsync(Release, progress, default);
                    Assert(mode == "valid" && result.Sha256 == hash && File.ReadAllBytes(result.Path).SequenceEqual(data), "Invalid download accepted");
                    Assert(progress.Values.Any(p => p.Verifying) && progress.Values.Any(p => p.Received == 0), "Download progress missing");
                    File.Delete(result.Path);
                }
                catch (Exception ex) when (mode != "valid" && ex is InvalidDataException or HttpRequestException) { }
                Assert(!Directory.EnumerateFiles(directory, "*.partial", SearchOption.AllDirectories).Any(), "Partial file survived failure");
            }
            using var forbidden = new HttpClient(new Handler((_, _) => throw new Exception("Unsafe URL contacted server")));
            try { await new UpdateDownloadService(forbidden, directory).DownloadAsync(Release with { InstallerUrl = new("https://evil.example/setup.exe") }, new Reporter(), default); throw new Exception("Unsafe URL accepted"); }
            catch (InvalidDataException) { }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    });

    public static void CommandGateAndCancellation() => Run(async () =>
    {
        var download = new SlowDownload(); var startup = new MemoryStartupService(); var router = new OrderedRouter();
        var vm = new MainWindowViewModel(new FakeDevices(), router, new Store(), new Updates(), startup: startup, downloads: download);
        await vm.InitializeAsync(); await vm.CheckUpdatesOnStartupAsync();
        vm.StartWithWindows = true; Assert(startup.IsEnabled, "Startup option did not update service");
        var bridge = new UiBridge(vm);
        JsonElement Message(string c) => JsonSerializer.SerializeToElement(new { id = Guid.NewGuid().ToString(), command = c });
        await bridge.HandleAsync(Message("downloadUpdate")).WaitAsync(TimeSpan.FromSeconds(1));
        Assert(vm.IsDownloadingUpdate && !vm.CheckUpdatesCommand.CanExecute(null), "Download state was not active");
        await bridge.HandleAsync(Message("start")).WaitAsync(TimeSpan.FromSeconds(1));
        Assert(router.State == RouteState.Running, "Download blocked audio start");
        vm.DismissUpdate(); Assert(vm.IsUpdateDialogOpen, "Active download was hidden");
        Assert(await vm.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(1)), "Shutdown did not cancel download");
        Assert(download.Cancelled && !vm.IsDownloadingUpdate && vm.DownloadedUpdate is null, "Cancellation installed incomplete download");
        var reset = new MainWindowViewModel(new FakeDevices(), new OrderedRouter(), new Store(), startup: startup);
        await reset.InitializeAsync(); Assert(reset.StartWithWindows, "Startup state did not load");
        await reset.ResetSettingsCommand.ExecuteAsync(null); Assert(!startup.IsEnabled, "Reset did not disable startup");
        await reset.ShutdownAsync();
    });
    static void Run(Func<Task> task) => task().GetAwaiter().GetResult();
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    sealed class Reporter : IProgress<UpdateDownloadProgress> { public List<UpdateDownloadProgress> Values = []; public void Report(UpdateDownloadProgress value) => Values.Add(value); }
    sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token); }
    sealed class Updates : IUpdateService { public Task<UpdateCheckResult> CheckAsync(CancellationToken token = default) => Task.FromResult(Release); }
    sealed class Store : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken token = default) => Task.FromResult(new AppSettings());
        public Task SaveAsync(AppSettings settings, CancellationToken token = default) => Task.CompletedTask;
    }
    sealed class SlowDownload : IUpdateDownloadService
    {
        public bool Cancelled;
        public async Task<DownloadedUpdate> DownloadAsync(UpdateCheckResult result, IProgress<UpdateDownloadProgress> progress, CancellationToken token)
        {
            try { await Task.Delay(Timeout.Infinite, token); throw new Exception("Unreachable"); }
            catch (OperationCanceledException) { Cancelled = true; throw; }
        }
    }
}
