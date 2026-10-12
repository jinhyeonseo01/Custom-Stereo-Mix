using System.Text.Json;
using CustomStereoMix.Audio;
using CustomStereoMix.Core;
using CustomStereoMix.State;

static class GraphTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static AudioNodeConfig N(string id, string kind, string? device = null) => new() { Id = id, Kind = kind, DeviceId = device };
    static AudioEdgeConfig E(string s, string t, float gain = 0) => new() { Id = s + "-" + t, Source = s, Target = t, GainDb = gain };
    static GraphProject Sample() => new() {
        Nodes = [N("a", "loopback", "speaker"), N("m", "capture", "mic"), N("b1", "bus"), N("b2", "bus"),
            N("o1", "output", "cable"), N("o2", "output", "other") with { LimiterEnabled = true }],
        Edges = [E("a", "b1"), E("m", "b1"), E("m", "b2"), E("b1", "o1"), E("b1", "o2"), E("b2", "o2")]
    };
    public static void Validation()
    {
        var graph = Sample();
        Check(GraphValidator.GetError(graph, true) is null, "Valid fanout rejected.");
        Check(GraphValidator.ActiveNodeIds(graph).Count == 6, "Active route pruning failed.");
        var loop = graph with { Nodes = graph.Nodes.Select(n => n.Id == "o2" ? n with { DeviceId = "speaker" } : n).ToArray() };
        Check(GraphValidator.GetError(loop)?.Key == MessageKey.GraphCycle, "Physical return path across buses accepted.");
        var duplicate = graph with { Nodes = graph.Nodes.Select(n => n.Id == "o2" ? n with { DeviceId = "CABLE" } : n).ToArray() };
        Check(GraphValidator.GetError(duplicate)?.Key == MessageKey.GraphDuplicate, "Duplicate render endpoint accepted.");
        Check(GraphValidator.GetError(graph with { Edges = [..graph.Edges, E("a", "b1") with { Id = "other" }] })?.Key == MessageKey.GraphDuplicate, "Duplicate send accepted.");
        Check(GraphValidator.GetError(graph with { Edges = [E("a", "o1")] })?.Key == MessageKey.GraphInvalid, "Invalid port roles accepted.");
        Check(GraphValidator.GetError(graph with { Nodes = graph.Nodes.Select(n => n.Id == "a" ? n with { GainDb = float.NaN } : n).ToArray() })?.Key == MessageKey.GraphInvalid, "Nonfinite gain accepted.");
        Check(GraphValidator.GetError(graph with { Edges = [] }, true)?.Key == MessageKey.GraphIncomplete, "Disconnected project executable.");
        Check(GraphValidator.GetError(graph with { Nodes = [..graph.Nodes, N("unused", "capture", "absent")] }, true) is null, "Unused nodes invalidated routes.");
        AudioDeviceDescriptor Device(string id, string name, AudioDeviceRole role) => new(id, name, role, 2, 48000, false, true, VirtualCableKind.VbCable);
        var render = new[] { Device("cable", "CABLE In 16ch (VB-Audio)", AudioDeviceRole.RenderTarget), Device("other", "CABLE-A Input (VB-Audio)", AudioDeviceRole.RenderTarget) };
        var capture = new[] { Device("cable-out", "CABLE Output (VB-Audio)", AudioDeviceRole.Microphone) };
        var virtualGraph = new GraphProject { Nodes = [N("input", "capture", "cable-out"), N("mix", "bus"), N("out", "output", "other")], Edges = [E("input", "mix"), E("mix", "out")] };
        Check(GraphValidator.GetError(virtualGraph, true, render, capture) is null, "Independent virtual input rejected.");
        var virtualLoop = virtualGraph with { Nodes = virtualGraph.Nodes.Select(n => n.Id == "out" ? n with { DeviceId = "cable" } : n).ToArray() };
        Check(GraphValidator.GetError(virtualLoop, true, render, capture)?.Key == MessageKey.GraphCycle, "Virtual cable alias feedback accepted.");
    }
    public static void Migration()
    {
        var settings = new AppSettings { SourceDeviceId = "speaker", TargetDeviceId = "cable", MicrophoneDeviceId = "mic",
            Processing = new(7, true, -18, 3, false), Microphone = new(false, -4) };
        var graph = GraphMigration.FromSettings(settings);
        Check(GraphMigration.IsSimple(graph), "Migrated graph not simple.");
        Check(!graph.Edges.Any(e => e.Source == "mic"), "Disabled microphone enabled on migration.");
        Check(graph.Nodes.Single(n => n.Kind == "bus").PreferredMicrophoneGainDb == -4, "Disabled microphone gain preference lost.");
        var bus = graph.Nodes.Single(n => n.Kind == "bus");
        Check(bus.GainDb == 7 && bus.CompressorEnabled && bus.CompressorRatio == 3 && !bus.LimiterEnabled, "DSP migration changed settings.");
        var enabled = GraphMigration.FromSettings(settings with { Microphone = new(true, -4) });
        Check(enabled.Edges.Single(e => e.Source == "mic").GainDb == -4, "Microphone send gain lost.");
        Check(!enabled.Nodes.Single(n => n.Kind == "output").LimiterEnabled, "Migration silently enabled a second limiter.");
    }
    public static void MixAndFanout()
    {
        var graph = Sample();
        var mixer = new GraphMixer(graph, 2, 32);
        var sources = new Dictionary<string, float[]> { ["a"] = Enumerable.Repeat(.25f, 64).ToArray(), ["m"] = Enumerable.Repeat(.5f, 64).ToArray() };
        mixer.Process(sources, 32);
        Check(mixer.Blocks["o1"].All(v => Math.Abs(v - .75) < .0001), "Broadcast mix wrong.");
        Check(mixer.Blocks["b2"].All(v => Math.Abs(v - .5) < .0001), "Independent microphone mix wrong.");
        Check(mixer.Blocks["o2"].All(v => v == 1), "Final combined output limiter failed.");
        Check(mixer.Meters["o2"].Clipped, "Final clipping not reported.");
        var buffer1 = new FloatRingBuffer(128); var buffer2 = new FloatRingBuffer(128);
        buffer1.Write(mixer.Blocks["o1"]); buffer2.Write(mixer.Blocks["o1"]);
        float[] out1 = new float[64], out2 = new float[64];
        buffer1.Read(out1); buffer2.Read(out2); Check(out1.SequenceEqual(out2), "Fanout consumers stole audio.");
        var muted = graph with { Edges = graph.Edges.Select(e => e.Source == "m" && e.Target == "b1" ? e with { Muted = true } : e).ToArray() };
        mixer.UpdateParameters(muted);
        for (int i = 0; i < 20; i++) mixer.Process(sources, 32);
        Check(Math.Abs(mixer.Blocks["o1"][0] - .25) < .001 && Math.Abs(mixer.Blocks["b2"][0] - .5) < .001, "Send mute leaked into other bus.");
        try { mixer.UpdateParameters(graph with { Edges = [] }); throw new Exception("Topology update accepted."); }
        catch (InvalidOperationException) { }
    }
    public static void AdaptiveClocks()
    {
        foreach (var ppm in new[] { -300, 300 })
        {
            var buffer = new AdaptiveAudioBuffer(48000, 2, 1200);
            float[] packet = new float[2000], output = new float[960];
            for (int i = 0; i < packet.Length; i += 2) { packet[i] = .25f; packet[i + 1] = -.5f; }
            buffer.Write(packet);
            buffer.Write(packet);
            var accumulated = 0d;
            for (int i = 0; i < 12000; i++) // Two minutes of simulated 10 ms callbacks.
            {
                accumulated += 480 * (1 + ppm / 1e6);
                var frames = (int)accumulated; accumulated -= frames;
                buffer.Write(packet.AsSpan(0, frames * 2));
                buffer.Read(output);
                Check(output.All(float.IsFinite), "Nonfinite clock correction.");
                if (i > 100) Check(Math.Abs(output[200] - .25) < .001 && Math.Abs(output[201] + .5) < .001, "Stereo channels drifted or underrun.");
            }
            var stats = buffer.Statistics;
            Check(stats.QueuedFrames > 100 && stats.QueuedFrames < 4000 && stats.Overruns == 0, "Clock queue grew without bound.");
            Check(Math.Abs(stats.CorrectionPpm) <= 2000.01, "Correction escaped bounds.");
        }
    }
    public static void ActivationAndMute()
    {
        var graph = Sample();
        var disabled = graph with { Nodes = graph.Nodes.Select(n => n.Id == "b1" ? n with { Enabled = false } : n).ToArray() };
        Check(GraphValidator.ActiveNodeIds(disabled).SetEquals(new[] { "m", "b2", "o2" }), "Disabled bus still activated its exclusive devices.");
        Check(disabled.Edges.SequenceEqual(graph.Edges), "Disabling changed saved connections.");
        var sources = new Dictionary<string, float[]> { ["a"] = Enumerable.Repeat(.25f, 64).ToArray(), ["m"] = Enumerable.Repeat(.5f, 64).ToArray() };
        var disabledMixer = new GraphMixer(disabled, 2, 32);
        disabledMixer.Process(sources, 32);
        Check(!disabledMixer.Blocks.ContainsKey("b1") && !disabledMixer.Blocks.ContainsKey("a") && !disabledMixer.Blocks.ContainsKey("o1"), "Disabled route allocated processing blocks.");
        Check(disabledMixer.Blocks["o2"].All(v => Math.Abs(v - .5) < .0001), "Disabled bus interrupted the other mix.");
        var muted = graph with { Nodes = graph.Nodes.Select(n => n.Id == "b1" ? n with { Muted = true } : n).ToArray() };
        Check(GraphValidator.SameTopology(graph, muted) && GraphValidator.ActiveNodeIds(muted).Count == 6, "Mute removed a processing route.");
        var mixer = new GraphMixer(graph, 2, 32);
        mixer.Process(sources, 32);
        mixer.UpdateParameters(muted);
        for (int i = 0; i < 20; i++) mixer.Process(sources, 32);
        Check(mixer.Blocks["o1"].All(v => Math.Abs(v) < .0001) && mixer.Blocks["o2"].All(v => Math.Abs(v - .5) < .0001), "Node mute silenced the wrong mix.");
        Check(!GraphValidator.SameTopology(graph, disabled), "Activation was treated as a live gain parameter.");
    }
    public static void BridgeAndPersistence() => RunBridge().GetAwaiter().GetResult();
    public static void StoreMigrationBackup() => RunStore().GetAwaiter().GetResult();
    static async Task RunStore()
    {
        var folder = Path.Combine(Path.GetTempPath(), "StereoGraphStore-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "settings.json");
        var store = new JsonSettingsStore(path);
        var old = new AppSettings { SourceDeviceId = "speaker", TargetDeviceId = "cable" };
        await store.SaveAsync(old);
        var original = await File.ReadAllTextAsync(path);
        var current = old with { Graph = GraphMigration.FromSettings(old), AdvancedMode = true };
        await store.SaveAsync(current);
        await store.SaveAsync(current with { Graph = current.Graph! with { Name = "edited" } });
        Check(await File.ReadAllTextAsync(path + ".v1.bak") == original, "Permanent v1 migration backup overwritten.");
        Check((await store.LoadAsync()).Graph?.Name == "edited", "Graph JSON roundtrip failed.");
        try { await store.SaveAsync(current with { AdvancedWindow = new(double.NaN, 1000) }); throw new Exception("Invalid window persisted."); }
        catch (InvalidDataException) { }
        Check((await store.LoadAsync()).Graph?.Name == "edited", "Invalid write changed stored project.");
        foreach (var file in Directory.GetFiles(folder)) File.Delete(file);
        Directory.Delete(folder);
    }
    static async Task RunBridge()
    {
        var store = new MemoryGraphSettings();
        var router = new TestGraphRouter();
        var model = new MainWindowViewModel(new FakeDevices(), router, store);
        await model.InitializeAsync();
        var bridge = new UiBridge(model);
        var inputs = bridge.Snapshot().GetProperty("microphones");
        Check(inputs.GetArrayLength() == 2 && inputs.EnumerateArray().Any(d => d.GetProperty("id").GetString() == "cable-out"), "Bridge omitted virtual capture endpoint.");
        Check(model.SelectedMicrophone?.Id == "mic", "Default physical microphone preference lost.");
        async Task<JsonElement> Call(string command, object payload) => JsonSerializer.SerializeToElement(await bridge.HandleAsync(JsonSerializer.SerializeToElement(new { id = Guid.NewGuid().ToString("N"), command, payload })));
        var graph = model.Graph with { Nodes = [..model.Graph.Nodes, N("second-bus", "bus")] };
        Check((await Call("graph", new { project = graph, revision = model.GraphRevision })).GetProperty("ok").GetBoolean(), "Graph patch failed.");
        Check(!(await Call("graph", new { project = graph, revision = -1 })).GetProperty("ok").GetBoolean(), "Stale revision accepted.");
        await Call("mode", new { advanced = true });
        await model.StartCommand.ExecuteAsync(null);
        Check(router.Started == 1 && model.IsRouting, "Graph start not used.");
        await Call("mode", new { advanced = false });
        Check(model.IsRouting && router.Stops == 0 && model.Graph.Nodes.Length == graph.Nodes.Length, "Mode switch collapsed or stopped graph.");
        var changed = graph with { Nodes = graph.Nodes.Select(n => n.Kind == "bus" ? n with { GainDb = -6 } : n).ToArray() };
        Check((await Call("graph", new { project = changed, revision = model.GraphRevision })).GetProperty("ok").GetBoolean(), "Runtime gain rejected.");
        Check(!(await Call("graph", new { project = changed with { Edges = [] }, revision = model.GraphRevision })).GetProperty("ok").GetBoolean(), "Runtime topology changed.");
        var disabledWhileRunning = changed with { Nodes = changed.Nodes.Select(n => n.Id == "mix" ? n with { Enabled = false } : n).ToArray() };
        Check(!(await Call("graph", new { project = disabledWhileRunning, revision = model.GraphRevision })).GetProperty("ok").GetBoolean() && model.Graph.Nodes.Single(n => n.Id == "mix").Enabled, "Running activation changed or mutated saved state.");
        await model.ShutdownAsync();
        Check(store.Value.Graph?.Nodes.Length == graph.Nodes.Length && !store.Value.AdvancedMode, "Graph/mode not saved.");
        var restarted = new MainWindowViewModel(new FakeDevices(), new TestGraphRouter(), store);
        await restarted.InitializeAsync();
        Check(restarted.Graph.Nodes.Single(n => n.Id == "mix").GainDb == -6, "Restart lost graph parameters.");
        await restarted.ShutdownAsync();
    }
    sealed class MemoryGraphSettings : ISettingsStore
    {
        public AppSettings Value = new();
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) { Value = settings; return Task.CompletedTask; }
    }
    sealed class TestGraphRouter : IAudioRouteController, IAudioGraphController
    {
        public RouteState State { get; private set; }
        public int Started, Stops;
        public event EventHandler<RouteStatusSnapshot>? StatusChanged;
        public event EventHandler<LevelMeterSnapshot>? LevelChanged { add { } remove { } }
        public GraphRuntimeSnapshot GraphSnapshot => new(new Dictionary<string, GraphNodeMeter>(), new Dictionary<string, string>());
        public Task StartGraphAsync(GraphProject project, AudioEngineSettings engine, CancellationToken cancellationToken = default) { Started++; State = RouteState.Running; StatusChanged?.Invoke(this, new(State, new(MessageKey.Routing))); return Task.CompletedTask; }
        public Task StartAsync(RouteConfig config, CancellationToken cancellationToken = default) => throw new Exception("Legacy route used.");
        public Task StopAsync(CancellationToken cancellationToken = default) { Stops++; State = RouteState.Stopped; StatusChanged?.Invoke(this, new(State, new(MessageKey.Ready))); return Task.CompletedTask; }
        public void UpdateGraphParameters(GraphProject graph) { }
        public void UpdateProcessing(ProcessingSettings settings) { }
        public void UpdateMicrophoneMix(MicMixSettings settings) { }
    }
}
