using CustomStereoMix.Audio;
using CustomStereoMix.Core;
using NAudio.Wave;
using CustomStereoMix.State;

if (args.Contains("--list-audio-devices")) { NativeGraphChecks.ListDevices(); return 0; }
if (args.Contains("--graph-audio-smoke")) { await NativeGraphChecks.SmokeAsync(); return 0; }
if (args.Contains("--graph-virtual-input-smoke")) { await NativeGraphChecks.SmokeAsync(virtualInput: true); return 0; }
if (args.Contains("--graph-partial-smoke")) { await NativeGraphChecks.SmokeAsync(true); return 0; }

var tests = new (string Name, Action Body)[]
{
    ("graph validates roles, duplicate endpoints and physical feedback across buses", GraphTests.Validation),
    ("legacy settings migrate without enabling microphone or changing DSP", GraphTests.Migration),
    ("graph mixing fans out, sums buses at output and isolates send mute", GraphTests.MixAndFanout),
    ("independent audio queues correct positive and negative clock drift", GraphTests.AdaptiveClocks),
    ("graph bridge revisions, mode switches, live gains and persistence", GraphTests.BridgeAndPersistence),
    ("graph settings preserve v1 backup and reject invalid window geometry", GraphTests.StoreMigrationBackup),
    ("installer download verifies checksum, size and source and cleans incomplete files", UpdateDownloadTests.Integrity),
    ("download preserves audio controls and shutdown cancels it; startup resets cleanly", UpdateDownloadTests.CommandGateAndCancellation),
    ("four language resources preserve keys, placeholders and dynamic errors", LocalizationTests.Resources),
    ("installer language seed resolves with safe OS fallback", LocalizationTests.InstallerSeed),
    ("language switches preserve routing and persist through restart and reset", LocalizationTests.PersistenceAndRouting),
    ("ring buffer fills underrun with silence", RingBufferUnderrun),
    ("ring buffer drops oldest samples on overflow", RingBufferOverflow),
    ("dsp limiter clamps output", DspLimiter),
    ("virtual cable classifier detects common cables", CableClassifier),
    ("format converter preserves float samples", FloatConverter),
    ("surround center and rear channels survive stereo downmix", SurroundDownmix),
    ("extensible speaker mask drives left-right mapping", ExtensibleMask),
    ("canceled save preserves last valid settings", SettingsCancellation),
    ("corrupt settings recover backup or defaults", SettingsRecovery),
    ("null and invalid settings do not break startup", InvalidSettings),
    ("feedback routes are rejected", FeedbackValidation),
    ("runtime gain and tray settings persist on shutdown", ViewModelPersistence),
    ("startup recovers devices after settings read failure", ViewModelRecovery),
    ("resampling preserves duration and packet continuity", ResamplingContinuity),
    ("downsampling rejects frequencies above target Nyquist", ResamplingFilter),
    ("buffer statistics account for lost and missing samples", BufferDiagnostics),
    ("settings reset stops audio and persists all defaults", SettingsReset),
    ("update placeholder reports unconfigured without claiming latest", UpdatePlaceholder),
    ("usage guide preference survives restart with first-run defaults", UsageGuidePreference),
    ("stereo buffer capacity preserves complete frames", FrameAlignedBuffer),
    ("non-finite capture samples become silence", NonFiniteCapture),
    ("UI bridge validates whole patches and keeps state on reload", BridgeValidation),
    ("shutdown waits for pending start before stopping audio", ShutdownDuringStart),
    ("shutdown waits for pending settings reset", ShutdownDuringReset),
    ("release tags use strict numeric versions", GitHubUpdateTests.ReleaseTags),
    ("GitHub releases compare numeric versions and validate installer assets", GitHubUpdateTests.ReleaseResponses),
    ("update checks handle missing releases, API failures and cancellation", GitHubUpdateTests.ErrorResponses),
    ("startup update popup appears once and can be reopened manually", GitHubUpdateTests.StartupPopup),
    ("startup update failure leaves audio controls usable", GitHubUpdateTests.StartupFailure),
    ("update network request does not hold the audio command gate", GitHubUpdateTests.CommandGate),
    ("update links are restricted to the configured repository", GitHubUpdateTests.LinkValidation)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

return failures == 0 ? 0 : 1;

static void RingBufferUnderrun()
{
    var buffer = new FloatRingBuffer(4);
    buffer.Write([0.25f, 0.5f]);
    Span<float> output = stackalloc float[4];
    var read = buffer.Read(output);
    Equal(2, read);
    Near(0.25f, output[0]);
    Near(0.5f, output[1]);
    Near(0, output[2]);
    Near(0, output[3]);
}

static void RingBufferOverflow()
{
    var buffer = new FloatRingBuffer(4);
    buffer.Write([1, 2, 3, 4, 5, 6]);
    Span<float> output = stackalloc float[4];
    var read = buffer.Read(output);
    Equal(4, read);
    Near(3, output[0]);
    Near(4, output[1]);
    Near(5, output[2]);
    Near(6, output[3]);
}

static void DspLimiter()
{
    float[] samples = [0.8f, -0.8f];
    DspProcessor.Apply(samples, new ProcessingSettings(GainDb: 12));
    Near(1, samples[0]);
    Near(-1, samples[1]);
}

static void CableClassifier()
{
    Equal(VirtualCableKind.VbCable, VirtualCableClassifier.Classify("CABLE Input (VB-Audio Virtual Cable)"));
    Equal(VirtualCableKind.VirtualAudioCable, VirtualCableClassifier.Classify("Line 1 (Virtual Audio Cable)"));
    Equal(VirtualCableKind.Voicemeeter, VirtualCableClassifier.Classify("VoiceMeeter Input"));
    Equal(VirtualCableKind.None, VirtualCableClassifier.Classify("Speakers (Realtek Audio)"));
}

static void FloatConverter()
{
    float[] input = [0.25f, -0.5f, 0.75f, -1f];
    var bytes = new byte[input.Length * sizeof(float)];
    Buffer.BlockCopy(input, 0, bytes, 0, bytes.Length);
    var output = AudioFormatConverter.ToInterleavedFloat(bytes, WaveFormat.CreateIeeeFloatWaveFormat(48000, 2), 48000, 2);
    Equal(input.Length, output.Length);
    for (var i = 0; i < input.Length; i++)
    {
        Near(input[i], output[i]);
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"expected {expected}, got {actual}");
    }
}

static byte[] FloatBytes(float[] samples)
{
    var bytes = new byte[samples.Length * sizeof(float)];
    Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
    return bytes;
}

static void SurroundDownmix()
{
    foreach (var channels in new[] { 6, 8 })
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48000, channels);
        for (var channel = 0; channel < channels; channel++)
        {
            var samples = new float[channels]; samples[channel] = 0.5f;
            var stereo = AudioFormatConverter.ToInterleavedFloat(FloatBytes(samples), format, 48000, 2);
            if (stereo.All(s => s <= 0)) throw new Exception($"Channel {channel} disappeared.");
            if (channel == 2) Near(stereo[0], stereo[1]);
        }
        var full = AudioFormatConverter.ToInterleavedFloat(FloatBytes(Enumerable.Repeat(1f, channels).ToArray()), format, 48000, 2);
        if (full.Any(s => s > 1.00001f)) throw new Exception("Downmix exceeds full scale.");
    }
}

static void ExtensibleMask()
{
    // A nonstandard three-channel layout FL + FR + side-left, not FL + FR + center.
    var format = new WaveFormatExtensible(48000, 32, 3);
    typeof(WaveFormatExtensible).GetField("dwChannelMask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
        .SetValue(format, 0x203);
    var output = AudioFormatConverter.ToInterleavedFloat(FloatBytes([0, 0, 0.5f]), format, 48000, 2);
    if (output[0] <= 0) throw new Exception("Side-left speaker missing.");
    Near(0, output[1]);
}

static string TemporarySettingsPath() => Path.Combine(Path.GetTempPath(), "CustomStereoMix.Tests", Guid.NewGuid().ToString("N"), "settings.json");

static void SettingsCancellation()
{
    var path = TemporarySettingsPath(); var store = new JsonSettingsStore(path);
    store.SaveAsync(new AppSettings { Processing = new ProcessingSettings(GainDb: 4) }).GetAwaiter().GetResult();
    using var canceled = new CancellationTokenSource(); canceled.Cancel();
    try { store.SaveAsync(new AppSettings(), canceled.Token).GetAwaiter().GetResult(); throw new Exception("Cancellation was ignored."); }
    catch (OperationCanceledException) { }
    Near(4, store.LoadAsync().GetAwaiter().GetResult().Processing.GainDb);
    Equal(0, Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Length);
}

static void SettingsRecovery()
{
    var path = TemporarySettingsPath(); var store = new JsonSettingsStore(path);
    store.SaveAsync(new AppSettings { Processing = new ProcessingSettings(GainDb: 3) }).GetAwaiter().GetResult();
    store.SaveAsync(new AppSettings { Processing = new ProcessingSettings(GainDb: 6) }).GetAwaiter().GetResult();
    Near(6, store.LoadAsync().GetAwaiter().GetResult().Processing.GainDb);
    File.WriteAllText(path, "{");
    Near(3, store.LoadAsync().GetAwaiter().GetResult().Processing.GainDb);
    if (store.LoadWarning is null) throw new Exception("Recovery warning missing.");
    var missingBackup = TemporarySettingsPath(); Directory.CreateDirectory(Path.GetDirectoryName(missingBackup)!);
    File.WriteAllText(missingBackup, "");
    var defaults = new JsonSettingsStore(missingBackup).LoadAsync().GetAwaiter().GetResult();
    Equal(48000, defaults.Engine.SampleRate);
}

static void InvalidSettings()
{
    var path = TemporarySettingsPath(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    foreach (var json in new[] { "null", "{\"processing\":null}", "{\"engine\":{\"channels\":0}}" })
    {
        File.WriteAllText(path, json); var store = new JsonSettingsStore(path);
        Equal(2, store.LoadAsync().GetAwaiter().GetResult().Engine.Channels);
        if (store.LoadWarning is null) throw new Exception("Invalid settings warning missing.");
    }
}

static AudioDeviceDescriptor Device(string id, bool virtualCable = false) => new(id, id, AudioDeviceRole.LoopbackSource,
    2, 48000, !virtualCable, virtualCable, virtualCable ? VirtualCableKind.VbCable : VirtualCableKind.None);

static void FeedbackValidation()
{
    var source = Device("headphone"); var target = Device("cable", true); var mic = Device("mic");
    if (RouteValidator.GetError(source, source, null, false) is null) throw new Exception("Self-loop accepted.");
    if (RouteValidator.GetError(source, target, target, true) is null) throw new Exception("Cable microphone accepted.");
    Equal<MessageKey?>(null, RouteValidator.GetError(source, target, mic, true));
}

static void ViewModelPersistence()
{
    var path = TemporarySettingsPath(); var store = new JsonSettingsStore(path); var router = new FakeRouter();
    var vm = new MainWindowViewModel(new FakeDevices(), router, store);
    vm.InitializeAsync().GetAwaiter().GetResult();
    vm.GainDb = 7; vm.CompressorEnabled = true; vm.MinimizeToTray = false;
    vm.ShutdownAsync().GetAwaiter().GetResult();
    var saved = store.LoadAsync().GetAwaiter().GetResult();
    Near(7, saved.Processing.GainDb); Equal(true, saved.Processing.CompressorEnabled); Equal(false, saved.MinimizeToTray);
    Equal(true, router.Stopped); Equal("mic", saved.MicrophoneDeviceId);
}

static void ViewModelRecovery()
{
    var vm = new MainWindowViewModel(new FakeDevices(), new FakeRouter(), new FailingLoadStore());
    vm.InitializeAsync().GetAwaiter().GetResult();
    if (vm.Sources.Count == 0 || vm.Targets.Count == 0 || !vm.HasNotice || !vm.IsStartEnabled) throw new Exception("Startup did not recover.");
}

static void ResamplingContinuity()
{
    var samples = Enumerable.Range(0, 44100).SelectMany(i => new[] { (float)Math.Sin(i * 2 * Math.PI * 1000 / 44100), (float)Math.Cos(i * 2 * Math.PI * 700 / 44100) }).ToArray();
    var bytes = new byte[samples.Length * 4];
    Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
    var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    var whole = AudioFormatConverter.ToInterleavedFloat(bytes, format, 48000, 2);
    var stream = new StreamingAudioFormatConverter(format, 48000, 2);
    var chunked = new List<float>();
    int[] sizes = [1, 7, 13, 128, 441];
    var offset = 0; var packet = 0;
    while (offset < bytes.Length)
    {
        var length = Math.Min(bytes.Length - offset, sizes[packet++ % sizes.Length] * format.BlockAlign);
        chunked.AddRange(stream.Convert(bytes.AsSpan(offset, length)));
        offset += length;
    }
    chunked.AddRange(stream.Flush());
    Equal(96000, whole.Length);
    Equal(whole.Length, chunked.Count);
    for (var i = 0; i < whole.Length; i++) Near(whole[i], chunked[i], 0.00002f);
    Equal(0, stream.Flush().Length);
}

static void ResamplingFilter()
{
    static double Energy(int frequency)
    {
        var samples = Enumerable.Range(0, 9600).Select(i => (float)Math.Sin(i * 2 * Math.PI * frequency / 96000)).ToArray();
        var bytes = new byte[samples.Length * 4]; Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        var output = AudioFormatConverter.ToInterleavedFloat(bytes, WaveFormat.CreateIeeeFloatWaveFormat(96000, 1), 48000, 1);
        Equal(4800, output.Length);
        return Math.Sqrt(output.Skip(256).SkipLast(256).Average(value => (double)value * value));
    }
    if (Energy(1000) < 0.65 || Energy(30000) > 0.02) throw new Exception("Sinc filter did not preserve speech-band audio and reject aliasing.");
}

static void BufferDiagnostics()
{
    var buffer = new FloatRingBuffer(4);
    buffer.Write([0.5f]); buffer.Write([1, 2, 3, 4, 5, 6]);
    var statistics = buffer.Statistics;
    Equal(1L, statistics.Overflows); Equal(3L, statistics.DroppedSamples); Equal(4, statistics.QueuedSamples);
    Span<float> output = stackalloc float[6]; buffer.Read(output);
    statistics = buffer.Statistics;
    Equal(1L, statistics.Underruns); Equal(2L, statistics.MissingSamples); Equal(0, statistics.QueuedSamples);
    buffer.Clear(); Equal(default(BufferStatistics), buffer.Statistics);
}

static void SettingsReset()
{
    var store = new JsonSettingsStore(TemporarySettingsPath()); var router = new FakeRouter();
    var vm = new MainWindowViewModel(new FakeDevices(), router, store);
    vm.InitializeAsync().GetAwaiter().GetResult();
    vm.GainDb = 10; vm.MicGainDb = 6; vm.CompressorEnabled = true; vm.CompressorRatio = 9;
    vm.SampleRate = 96000; vm.Channels = 1; vm.LatencyMilliseconds = 100; vm.MinimizeToTray = false;
    vm.SetUsageGuidePreference(true);
    vm.ResetSettingsCommand.ExecuteAsync(null).GetAwaiter().GetResult();
    var saved = store.LoadAsync().GetAwaiter().GetResult();
    Equal(ProcessingSettings.Default, saved.Processing);
    Equal(MicMixSettings.Default, saved.Microphone);
    Equal(AudioEngineSettings.Default, saved.Engine);
    Equal(true, saved.MinimizeToTray); Equal(true, router.Stopped);
    Equal("headphone", saved.SourceDeviceId); Equal("cable", saved.TargetDeviceId);
    Equal(true, vm.HasSettingsMessage); Equal(true, vm.IsStartEnabled);
    Equal<bool?>(null, saved.HideUsageOnStartup); Equal(true, vm.ShouldShowUsageOnStartup);
}

static void UsageGuidePreference()
{
    var store = new JsonSettingsStore(TemporarySettingsPath());
    MainWindowViewModel Load()
    {
        var vm = new MainWindowViewModel(new FakeDevices(), new FakeRouter(), store);
        vm.InitializeAsync().GetAwaiter().GetResult(); return vm;
    }
    var first = Load();
    Equal(true, first.ShouldShowUsageOnStartup); Equal(true, first.DontShowUsageAgain);
    first.SetUsageGuidePreference(true); first.ShutdownAsync().GetAwaiter().GetResult();
    var hidden = Load();
    Equal(false, hidden.ShouldShowUsageOnStartup); Equal(true, hidden.DontShowUsageAgain);
    hidden.SetUsageGuidePreference(false); hidden.ShutdownAsync().GetAwaiter().GetResult();
    var repeated = Load();
    Equal(true, repeated.ShouldShowUsageOnStartup); Equal(false, repeated.DontShowUsageAgain);
    repeated.ShutdownAsync().GetAwaiter().GetResult();
}

static void UpdatePlaceholder()
{
    var result = new UnconfiguredUpdateService().CheckAsync().GetAwaiter().GetResult();
    Equal(UpdateCheckState.NotConfigured, result.State);
    Equal<string?>(null, result.LatestVersion); Equal<Uri?>(null, result.ReleasePage);
    var vm = new MainWindowViewModel(new FakeDevices(), new FakeRouter(), new JsonSettingsStore(TemporarySettingsPath()));
    vm.CheckUpdatesCommand.ExecuteAsync(null).GetAwaiter().GetResult();
    Equal(vm.Text(result.Message.Key, result.Message.Arguments), vm.UpdateMessage);
}

static void FrameAlignedBuffer()
{
    foreach (var rate in new[] { 22050, 44100, 48000, 192000 })
    foreach (var milliseconds in new[] { 80, 81, 250 })
    {
        var engine = new AudioEngineSettings(rate, 2, 50, milliseconds);
        Equal(0, engine.BufferSamples % 2);
    }
    try { AudioSettingsValidator.Validate(new AudioEngineSettings(48000, 2, 200, 80), ProcessingSettings.Default, MicMixSettings.Default); throw new Exception("Undersized buffer accepted."); }
    catch (InvalidDataException) { }
}

static void NonFiniteCapture()
{
    var output = AudioFormatConverter.ToInterleavedFloat(FloatBytes([float.NaN, float.PositiveInfinity, float.NegativeInfinity, .25f]), WaveFormat.CreateIeeeFloatWaveFormat(48000, 1), 48000, 1);
    Near(0, output[0]); Near(0, output[1]); Near(0, output[2]); Near(.25f, output[3]);
}

static void BridgeValidation()
{
    var vm = new MainWindowViewModel(new FakeDevices(), new FakeRouter(), new JsonSettingsStore(TemporarySettingsPath()));
    vm.InitializeAsync().GetAwaiter().GetResult(); var bridge = new UiBridge(vm);
    System.Text.Json.JsonElement Request(string command, object? payload = null)
    {
        var message = System.Text.Json.JsonSerializer.SerializeToElement(new { id = Guid.NewGuid().ToString("N"), command, payload = payload ?? new { } });
        return System.Text.Json.JsonSerializer.SerializeToElement(bridge.HandleAsync(message).GetAwaiter().GetResult());
    }
    Equal(true, Request("ready").GetProperty("ok").GetBoolean());
    Equal("help", bridge.Snapshot().GetProperty("view").GetString());
    Equal(false, Request("patch", new { gainDb = 3, channels = 0 }).GetProperty("ok").GetBoolean());
    Near(0, (float)vm.GainDb); Equal(2m, vm.Channels);
    Equal(false, Request("patch", new { latencyMilliseconds = 250, bufferMilliseconds = 80 }).GetProperty("ok").GetBoolean());
    Equal(50m, vm.LatencyMilliseconds);
    Equal(false, Request("launchAnything").GetProperty("ok").GetBoolean());
    Equal(true, Request("patch", new { gainDb = 4 }).GetProperty("ok").GetBoolean());
    Near(4, (float)vm.GainDb);
    Request("guideDraft", new { dontShowAgain = false }); Request("closeView"); Request("ready");
    Equal("main", bridge.Snapshot().GetProperty("view").GetString());
    Equal(true, vm.ShouldShowUsageOnStartup);
    vm.ShutdownAsync().GetAwaiter().GetResult();
}

static void ShutdownDuringStart()
{
    async Task Run()
    {
        var store = new GatedSaveStore(); var router = new OrderedRouter();
        var vm = new MainWindowViewModel(new FakeDevices(), router, store);
        await vm.InitializeAsync();
        var starting = vm.StartCommand.ExecuteAsync(null);
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Equal(false, vm.CanEditSettings);
        var exiting = vm.ShutdownAsync();
        await Task.Delay(40);
        Equal(0, router.Calls.Count);
        store.Release.TrySetResult();
        await Task.WhenAll(starting, exiting).WaitAsync(TimeSpan.FromSeconds(2));
        Equal("start,stop", string.Join(',', router.Calls));
        Equal(true, exiting.Result); Equal(RouteState.Stopped, router.State);
    }
    Run().GetAwaiter().GetResult();
}

static void ShutdownDuringReset()
{
    async Task Run()
    {
        var store = new GatedSaveStore(); var router = new OrderedRouter();
        var vm = new MainWindowViewModel(new FakeDevices(), router, store);
        await vm.InitializeAsync();
        var resetting = vm.ResetSettingsCommand.ExecuteAsync(null);
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var exiting = vm.ShutdownAsync();
        await Task.Delay(40);
        Equal(false, exiting.IsCompleted);
        Equal(1, router.Calls.Count);
        store.Release.TrySetResult();
        await Task.WhenAll(resetting, exiting).WaitAsync(TimeSpan.FromSeconds(2));
        Equal(true, exiting.Result); Equal(RouteState.Stopped, router.State);
        Equal("stop,stop", string.Join(',', router.Calls));
    }
    Run().GetAwaiter().GetResult();
}

static void Near(float expected, float actual, float tolerance = 0.0001f)
{
    if (Math.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException($"expected {expected}, got {actual}");
    }
}

sealed class FakeDevices : IAudioDeviceService
{
    private static AudioDeviceDescriptor Make(string id, AudioDeviceRole role, bool cable = false) =>
        new(id, id, role, 2, 48000, !cable, cable, cable ? VirtualCableKind.VbCable : VirtualCableKind.None);
    public IReadOnlyList<AudioDeviceDescriptor> GetLoopbackSources() => [Make("headphone", AudioDeviceRole.LoopbackSource)];
    public IReadOnlyList<AudioDeviceDescriptor> GetRenderTargets() => [Make("cable", AudioDeviceRole.RenderTarget, true)];
    public IReadOnlyList<AudioDeviceDescriptor> GetMicrophones() => [Make("cable-out", AudioDeviceRole.Microphone, true), Make("mic", AudioDeviceRole.Microphone)];
}
sealed class FakeRouter : IAudioRouteController
{
    public bool Stopped;
    public RouteState State => RouteState.Stopped;
    public event EventHandler<RouteStatusSnapshot>? StatusChanged { add { } remove { } }
    public event EventHandler<LevelMeterSnapshot>? LevelChanged { add { } remove { } }
    public Task StartAsync(RouteConfig config, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public async Task StopAsync(CancellationToken cancellationToken = default) { await Task.Delay(10, cancellationToken); Stopped = true; }
    public void UpdateProcessing(ProcessingSettings settings) { }
    public void UpdateMicrophoneMix(MicMixSettings settings) { }
}
sealed class FailingLoadStore : ISettingsStore
{
    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => throw new IOException("Simulated read error");
    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class GatedSaveStore : ISettingsStore
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _first = true;
    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AppSettings());
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (!_first) return;
        _first = false; Entered.TrySetResult(); await Release.Task;
    }
}

sealed class OrderedRouter : IAudioRouteController
{
    public List<string> Calls { get; } = new();
    public RouteState State { get; private set; }
    public event EventHandler<RouteStatusSnapshot>? StatusChanged;
    public event EventHandler<LevelMeterSnapshot>? LevelChanged { add { } remove { } }
    public Task StartAsync(RouteConfig config, CancellationToken cancellationToken = default)
    {
        Calls.Add("start"); State = RouteState.Running; StatusChanged?.Invoke(this, new(State, new(MessageKey.Routing))); return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("stop"); State = RouteState.Stopped; StatusChanged?.Invoke(this, new(State, new(MessageKey.Ready))); return Task.CompletedTask;
    }
    public void UpdateProcessing(ProcessingSettings settings) { }
    public void UpdateMicrophoneMix(MicMixSettings settings) { }
}
