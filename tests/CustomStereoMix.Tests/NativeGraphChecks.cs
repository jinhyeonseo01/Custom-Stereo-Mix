using System.Text.Json;
using CustomStereoMix.Audio;
using CustomStereoMix.Core;

static class NativeGraphChecks
{
    public static void ListDevices()
    {
        using var devices = new NaudioDeviceService();
        var render = devices.GetLoopbackSources();
        var capture = devices.GetMicrophones();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            sources = render.Select(d => new { d.Id, d.Name, d.Channels, d.SampleRate, d.IsDefault, d.IsVirtualCableCandidate }),
            captureEndpoints = capture.Select(d => new { d.Id, d.Name, d.Channels, d.SampleRate, d.IsDefault, d.IsVirtualCableCandidate }),
            allowedInputs = capture.Select(d => new { d.Id, d.Name, d.ReturnPathGroup }),
            targets = devices.GetRenderTargets().Select(d => new { d.Id, d.Name })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static async Task SmokeAsync(bool includeVirtual = false, bool virtualInput = false)
    {
        using var devices = new NaudioDeviceService();
        var input = devices.GetMicrophones().FirstOrDefault(d => d.IsVirtualCableCandidate == virtualInput);
        var targets = devices.GetRenderTargets();
        var physical = targets.Where(d => !d.IsVirtualCableCandidate).Take(2);
        var outputs = (includeVirtual ? targets.Where(d => d.IsVirtualCableCandidate).Take(2).Concat(physical) : physical).ToArray();
        if (input is null || outputs.Length < 2) throw new InvalidOperationException("Need one physical input and two render endpoints.");
        var nodes = new List<AudioNodeConfig> {
            new() { Id = "input", Kind = "capture", DeviceId = input.Id },
            new() { Id = "bus1", Kind = "bus", Muted = true },
            new() { Id = "bus2", Kind = "bus", Muted = true }
        };
        nodes.AddRange(outputs.Select((d, i) => new AudioNodeConfig { Id = "output" + i, Kind = "output", DeviceId = d.Id, LimiterEnabled = true }));
        var loopback = devices.GetLoopbackSources().FirstOrDefault(d => !d.IsVirtualCableCandidate && outputs.All(o => o.Id != d.Id));
        if (loopback is not null) nodes.Add(new() { Id = "playback", Kind = "loopback", DeviceId = loopback.Id });
        var graph = new GraphProject { Nodes = nodes.ToArray(), Edges = [
            new() { Id = "send1", Source = "input", Target = "bus1" }, new() { Id = "send2", Source = "input", Target = "bus2" },
            new() { Id = "out1", Source = "bus1", Target = "output0" }, new() { Id = "out2", Source = "bus2", Target = "output1" }
        ] };
        for (var i = 2; i < outputs.Length; i++) graph = graph with { Edges = [..graph.Edges,
            new() { Id = "out" + (i + 1), Source = "bus1", Target = "output" + i }] };
        if (loopback is not null) graph = graph with { Edges = [..graph.Edges, new() { Id = "playback-send", Source = "playback", Target = "bus1" }] };
        var router = new NaudioRouteController(devices);
        try
        {
            await router.StartGraphAsync(graph, AudioEngineSettings.Default);
            await Task.Delay(5000);
            var snapshot = router.GraphSnapshot;
            if (router.State != RouteState.Running || (!includeVirtual && snapshot.Errors.Count != 0) ||
                outputs.Length - snapshot.Errors.Count < 2 || snapshot.Meters.Count != nodes.Count)
                throw new Exception("Native graph fault: " + JsonSerializer.Serialize(snapshot));
            if (snapshot.Meters["output0"].Peak != 0 || snapshot.Meters["output1"].Peak != 0)
                throw new Exception("Muted smoke graph emitted audio.");
            Console.WriteLine("PASS native capture + loopback -> two buses -> " + (outputs.Length - snapshot.Errors.Count) + " running WASAPI outputs (muted), 5 seconds.");
            Console.WriteLine("Input: " + input.Name);
            Console.WriteLine("Playback loopback: " + loopback?.Name);
            Console.WriteLine("Outputs: " + string.Join(" | ", outputs.Select(d => d.Name)));
            Console.WriteLine("Isolated errors: " + JsonSerializer.Serialize(snapshot.Errors));
        }
        finally { await router.StopAsync(); }
        if (router.State != RouteState.Stopped) throw new Exception("Native session did not stop.");
    }
}
