using System.Text.Json;
using CustomStereoMix.Core;

namespace CustomStereoMix.State;

/// <summary>Commands and display state only. PCM audio never crosses this bridge.</summary>
public sealed class UiBridge(MainWindowViewModel model, Func<Task>? openCableWebsite = null, Func<Uri, Task>? openUpdateWebsite = null)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _commands = new(1, 1);
    private bool _viewInitialized;
    private string _view = "main";
    private bool _guideDraft = true;
    private long _revision;
    public event EventHandler? Changed;

    public void OpenView(string view)
    {
        if (view is not ("main" or "settings" or "help")) throw new ArgumentException("Unknown view.");
        if (_view == "help" && view != "help") model.SetUsageGuidePreference(_guideDraft);
        if (view == "help" && _view != "help") _guideDraft = model.DontShowUsageAgain;
        _view = view;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void CommitGuideOnClose()
    {
        if (_view == "help") model.SetUsageGuidePreference(_guideDraft);
    }

    public JsonElement Snapshot() => JsonSerializer.SerializeToElement(new
    {
        revision = Interlocked.Increment(ref _revision), view = _view, guideDraft = _guideDraft,
        graph = model.Graph, graphRevision = model.GraphRevision, advancedMode = model.AdvancedMode,
        simpleGraph = model.IsSimpleGraph,
        simpleBusId = model.SimpleBusId,
        sources = model.Sources.Select(Device), targets = model.Targets.Select(Device), microphones = model.Microphones.Select(Device),
        sourceDeviceId = model.SelectedSource?.Id, targetDeviceId = model.SelectedTarget?.Id,
        microphoneDeviceId = model.SelectedMicrophone?.Id,
        model.StatusText, model.TargetMessage, model.NoticeText, model.IsRouting,
        model.CanEditRoute, model.CanEditSettings, model.IsStartEnabled, model.IsStopEnabled,
        model.CanResetSettings, model.EngineLine, model.TrayHint, model.AppVersion,
        model.UpdateMessage, model.SettingsMessage, model.IsUpdateDialogOpen,
        model.IsDownloadingUpdate, model.DownloadProgress, model.DownloadMessage,
        updateDownloaded = model.DownloadedUpdate is not null,
        latestVersion = model.UpdateResult?.LatestVersion,
        hasUpdateInstaller = model.UpdateResult?.State == Core.UpdateCheckState.Available && model.UpdateResult.InstallerUrl is not null,
        hasReleasePage = model.UpdateResult?.ReleasePage is not null,
        busy = model.StartCommand.IsRunning || model.StopCommand.IsRunning || model.ResetSettingsCommand.IsRunning,
        updating = model.IsCheckingUpdates,
        settings = new
        {
            model.Language, model.StartWithWindows, model.GainDb, model.CompressorEnabled, model.CompressorThresholdDb, model.CompressorRatio,
            model.IsMicMixEnabled, model.MicGainDb, model.SampleRate, model.Channels,
            model.LatencyMilliseconds, model.BufferMilliseconds, model.MinimizeToTray, model.CloseToTray
        }
    }, JsonOptions);

    private object Device(Core.AudioDeviceDescriptor device) => new
    {
        device.Id, device.Name, device.IsDefault, device.IsVirtualCableCandidate, device.ReturnPathGroup,
        summary = model.Text(MessageKey.DeviceSummary, device.SampleRate / 1000.0, device.Channels, device.IsDefault ? model.Text(MessageKey.DefaultDevice) : "")
    };
    public object Meter()
    {
        var graph = model.GraphSnapshot;
        return new { model.LevelPercent, model.SignalState, samples = model.Waveform, nodes = graph.Meters, errors = graph.Errors };
    }

    public async Task<object> HandleAsync(JsonElement message)
    {
        string? id = null;
        await _commands.WaitAsync();
        try
        {
            id = message.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(id) || id.Length > 80) throw new InvalidOperationException().WithMessage(MessageKey.InvalidRequestId);
            var command = message.GetProperty("command").GetString();
            var payload = message.TryGetProperty("payload", out var p) ? p : default;
            switch (command)
            {
                case "ready":
                    if (!_viewInitialized)
                    {
                        _viewInitialized = true;
                        _view = model.ShouldShowUsageOnStartup ? "help" : "main";
                        _guideDraft = model.DontShowUsageAgain;
                    }
                    break;
                case "start": await RunCommand(model.StartCommand); break;
                case "stop": await RunCommand(model.StopCommand); break;
                case "refresh": await RunCommand(model.RefreshDevicesCommand); break;
                case "reset": await RunCommand(model.ResetSettingsCommand); break;
                case "checkUpdates":
                    if (!model.CheckUpdatesCommand.CanExecute(null)) throw new InvalidOperationException().WithMessage(MessageKey.WaitUpdate);
                    // Network I/O must not hold the command gate used by Start/Stop.
                    _ = model.CheckUpdatesCommand.ExecuteAsync(null);
                    break;
                case "dismissUpdate": model.DismissUpdate(); break;
                case "openRelease": await OpenReleasePageAsync(); break;
                case "downloadUpdate": model.DownloadUpdate(); break;
                case "cancelUpdateDownload": model.CancelUpdateDownload(); break;
                case "patch": ApplyPatch(payload); break;
                case "mode": model.SetAdvancedMode(payload.GetProperty("advanced").GetBoolean()); break;
                case "simpleBus": model.SetSimpleBus(payload.GetProperty("id").GetString()!); break;
                case "graph":
                    model.SetGraph(payload.GetProperty("project").Deserialize<GraphProject>(JsonOptions)
                        ?? throw new InvalidOperationException().WithMessage(MessageKey.GraphInvalid),
                        payload.GetProperty("revision").GetInt64());
                    break;
                case "openHelp": OpenView("help"); break;
                case "openSettings": OpenView("settings"); break;
                case "closeView": OpenView("main"); break;
                case "guideDraft": _guideDraft = payload.GetProperty("dontShowAgain").GetBoolean(); break;
                case "openCableWebsite":
                    if (openCableWebsite is null) throw new InvalidOperationException().WithMessage(MessageKey.CannotOpenInstallPage);
                    await openCableWebsite();
                    break;
                default: throw new InvalidOperationException().WithMessage(MessageKey.UnsupportedRequest);
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return new { type = "response", id, ok = true, state = Snapshot() };
        }
        catch (Exception ex)
        {
            return new { type = "response", id, ok = false, error = model.Describe(ex), state = Snapshot() };
        }
        finally { _commands.Release(); }
    }

    private async Task OpenReleasePageAsync()
    {
        var url = model.UpdateResult?.ReleasePage;
        if (url is null || openUpdateWebsite is null || !GitHubReleaseUpdateService.IsReleaseLink(url))
            throw new InvalidOperationException().WithMessage(MessageKey.CannotOpenUpdateLink);
        await openUpdateWebsite(url);
    }

    private static async Task RunCommand(CommunityToolkit.Mvvm.Input.IAsyncRelayCommand command)
    {
        if (!command.CanExecute(null)) throw new InvalidOperationException().WithMessage(MessageKey.ActionUnavailable);
        await command.ExecuteAsync(null);
    }

    private void ApplyPatch(JsonElement payload)
    {
        if (!model.IsSimpleGraph && payload.EnumerateObject().Any(f => f.Name is "sourceDeviceId" or "targetDeviceId" or "microphoneDeviceId" or "isMicMixEnabled" or "gainDb" or "micGainDb" or "compressorEnabled" or "compressorThresholdDb" or "compressorRatio"))
            throw new InvalidOperationException().WithMessage(MessageKey.GraphSimpleReadOnly);
        if (!model.CanEditSettings) throw new InvalidOperationException().WithMessage(MessageKey.WaitSettings);
        var actions = new List<Action>();
        foreach (var field in payload.EnumerateObject())
        {
            var name = field.Name;
            var value = field.Value;
            if (name is "sourceDeviceId" or "targetDeviceId" or "microphoneDeviceId" or "isMicMixEnabled" or
                "sampleRate" or "channels" or "latencyMilliseconds" or "bufferMilliseconds")
                if (!model.CanEditRoute) throw new InvalidOperationException().WithMessage(MessageKey.StopBeforeChange);
            // Validate the complete patch before applying any part of it.
            switch (name)
            {
                case "language":
                {
                    var language = value.GetString();
                    if (!Core.LanguageCatalog.IsSupported(language)) throw new InvalidOperationException().WithMessage(MessageKey.UnsupportedLanguage);
                    actions.Add(() => model.Language = language!);
                    break;
                }
                case "gainDb": { var n = Number(value, -12, 12); actions.Add(() => model.GainDb = n); break; }
                case "micGainDb": { var n = Number(value, -12, 12); actions.Add(() => model.MicGainDb = n); break; }
                case "compressorThresholdDb": { var n = Number(value, -40, -3); actions.Add(() => model.CompressorThresholdDb = n); break; }
                case "compressorRatio": { var n = Number(value, 1, 10); actions.Add(() => model.CompressorRatio = n); break; }
                case "sampleRate": { var n = Number(value, 22050, 192000, integer: true); actions.Add(() => model.SampleRate = (decimal)n); break; }
                case "channels": { var n = Number(value, 1, 2, integer: true); actions.Add(() => model.Channels = (decimal)n); break; }
                case "latencyMilliseconds": { var n = Number(value, 10, 250, integer: true); actions.Add(() => model.LatencyMilliseconds = (decimal)n); break; }
                case "bufferMilliseconds": { var n = Number(value, 80, 1000, integer: true); actions.Add(() => model.BufferMilliseconds = (decimal)n); break; }
                case "compressorEnabled": { var b = value.GetBoolean(); actions.Add(() => model.CompressorEnabled = b); break; }
                case "isMicMixEnabled": { var b = value.GetBoolean(); actions.Add(() => model.IsMicMixEnabled = b); break; }
                case "minimizeToTray": { var b = value.GetBoolean(); actions.Add(() => model.MinimizeToTray = b); break; }
                case "closeToTray": { var b = value.GetBoolean(); actions.Add(() => model.CloseToTray = b); break; }
                case "startWithWindows": { var b = value.GetBoolean(); actions.Add(() => model.StartWithWindows = b); break; }
                case "sourceDeviceId": { var d = FindDevice(model.Sources, value); actions.Add(() => model.SelectedSource = d); break; }
                case "targetDeviceId": { var d = FindDevice(model.Targets, value); actions.Add(() => model.SelectedTarget = d); break; }
                case "microphoneDeviceId": { var d = FindDevice(model.Microphones, value); actions.Add(() => model.SelectedMicrophone = d); break; }
                default: throw new InvalidOperationException().WithMessage(MessageKey.CannotChangeSetting, name);
            }
        }
        double Proposed(string key, double fallback) => payload.TryGetProperty(key, out var n) ? n.GetDouble() : fallback;
        Core.AudioSettingsValidator.Validate(new Core.AudioEngineSettings(
            (int)Proposed("sampleRate", (double)model.SampleRate), (int)Proposed("channels", (double)model.Channels),
            (int)Proposed("latencyMilliseconds", (double)model.LatencyMilliseconds), (int)Proposed("bufferMilliseconds", (double)model.BufferMilliseconds)),
            new Core.ProcessingSettings((float)Proposed("gainDb", model.GainDb), model.CompressorEnabled,
                (float)Proposed("compressorThresholdDb", model.CompressorThresholdDb), (float)Proposed("compressorRatio", model.CompressorRatio)),
            new Core.MicMixSettings(model.IsMicMixEnabled, (float)Proposed("micGainDb", model.MicGainDb)));
        foreach (var action in actions) action();
        model.SyncSimpleGraph();
    }

    private static Core.AudioDeviceDescriptor FindDevice(IEnumerable<Core.AudioDeviceDescriptor> devices, JsonElement value) =>
        devices.FirstOrDefault(d => string.Equals(d.Id, value.GetString(), StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException().WithMessage(MessageKey.DeviceNotFound);

    private static double Number(JsonElement value, double min, double max, bool integer = false)
    {
        var n = value.GetDouble();
        if (!double.IsFinite(n) || n < min || n > max || (integer && n != Math.Truncate(n)))
            throw new InvalidOperationException().WithMessage(MessageKey.InvalidRange, min, max);
        return n;
    }
}
