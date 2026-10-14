using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CustomStereoMix.Core;

namespace CustomStereoMix.State;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IAudioDeviceService _devices;
    private readonly IAudioRouteController _router;
    private readonly ISettingsStore _settings;
    private readonly IUpdateService _updates;
    private readonly IUpdateDownloadService? _downloads;
    private CancellationTokenSource? _downloadCancellation;
    private Task _downloadTask = Task.CompletedTask;
    private bool _isDownloadingUpdate;
    private UpdateDownloadProgress? _downloadProgress;
    private LocalizedMessage _downloadMessage = LocalizedMessage.Empty;
    private readonly IUiDispatcher _dispatcher;
    private bool _resetting;
    private LocalizedMessage _settingsMessage = LocalizedMessage.Empty;
    private LocalizedMessage _updateMessage = new(MessageKey.UpdateIntro);
    private Task _updateTask = Task.CompletedTask;
    private CancellationTokenSource? _updateCancellation;
    private bool _startupUpdatesChecked;
    private bool _isCheckingUpdates;
    private bool _isUpdateDialogOpen;
    private UpdateCheckResult? _updateResult;
    private bool _initialized;
    private bool _shuttingDown;
    private bool _minimizeToTray = true;
    private bool _closeToTray = true;
    private bool? _hideUsageOnStartup;
    private LocalizedMessage _noticeText = LocalizedMessage.Empty;
    private AppSettings _loadedSettings = new();
    private CancellationTokenSource? _saveCancellation;
    private Task _pendingSave = Task.CompletedTask;
    private AudioDeviceDescriptor? _selectedSource;
    private AudioDeviceDescriptor? _selectedTarget;
    private AudioDeviceDescriptor? _selectedMicrophone;
    private LocalizedMessage _statusText = new(MessageKey.Ready);
    private LocalizedMessage _targetMessage = new(MessageKey.NoCableTarget);
    private LocalizedMessage _monitorCaption = new(MessageKey.WaitingPlayback);
    private LocalizedMessage _signalState = new(MessageKey.NoSignal);
    private IReadOnlyList<float> _waveform = Array.Empty<float>();
    private double _levelPercent;
    private double _gainDb;
    private bool _compressorEnabled;
    private bool _limiterEnabled = true;
    private double _compressorThresholdDb = -12;
    private double _compressorRatio = 4;
    private bool _isMicMixEnabled;
    private double _micGainDb;
    private decimal _sampleRate = 48000;
    private decimal _channels = 2;
    private decimal _latencyMilliseconds = 50;
    private decimal _bufferMilliseconds = 250;
    private bool _isRouting;
    private readonly string _initialLanguage;
    private string _language;
    private Localizer _localizer;
    private readonly IStartupService _startup;
    private bool _startWithWindows;
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            _startup.SetEnabled(value);
            SetProperty(ref _startWithWindows, value);
        }
    }
    public string Text(MessageKey key, params object?[] args) => _localizer.Text(key, args);
    public string Describe(Exception exception) => _localizer.Describe(exception);

    public string Language
    {
        get => _language;
        set
        {
            if (!LanguageCatalog.IsSupported(value)) throw new ArgumentException("Unsupported language.");
            if (_language == value) return;
            _language = value;
            _localizer = new Localizer(value);
            OnPropertyChanged();
            foreach (var property in new[] { nameof(StatusText), nameof(TargetMessage), nameof(NoticeText), nameof(UpdateMessage), nameof(DownloadMessage), nameof(SettingsMessage), nameof(SignalState), nameof(MonitorCaption), nameof(EngineLine), nameof(TrayHint), nameof(UpdateButtonText) }) OnPropertyChanged(property);
            ScheduleSave();
        }
    }

    public MainWindowViewModel(IAudioDeviceService devices, IAudioRouteController router, ISettingsStore settings, IUpdateService? updates = null, IUiDispatcher? dispatcher = null, string? initialLanguage = null, IStartupService? startup = null, IUpdateDownloadService? downloads = null)
    {
        _downloads = downloads;
        _startup = startup ?? new MemoryStartupService();
        _initialLanguage = LanguageCatalog.Resolve(initialLanguage, System.Globalization.CultureInfo.CurrentUICulture.Name);
        _language = _initialLanguage;
        _localizer = new Localizer(_language);
        _devices = devices;
        _router = router;
        _settings = settings;
        _updates = updates ?? new UnconfiguredUpdateService();
        _dispatcher = dispatcher ?? new ContextUiDispatcher();
        _router.StatusChanged += OnStatusChanged;
        _router.LevelChanged += OnLevelChanged;
        RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync, () => CanEditRoute);
        StartCommand = new AsyncRelayCommand(StartAsync, () => IsStartEnabled);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsStopEnabled);
        ResetSettingsCommand = new AsyncRelayCommand(ResetSettingsAsync, () => CanResetSettings);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync, () => !_shuttingDown && !IsCheckingUpdates && !IsDownloadingUpdate);
        StartCommand.PropertyChanged += (_, _) => RefreshComputedState();
        StopCommand.PropertyChanged += (_, _) => RefreshComputedState();
        CheckUpdatesCommand.PropertyChanged += (_, _) => OnPropertyChanged(nameof(UpdateButtonText));
    }

    public ObservableCollection<AudioDeviceDescriptor> Sources { get; } = new();
    public ObservableCollection<AudioDeviceDescriptor> Targets { get; } = new();
    public ObservableCollection<AudioDeviceDescriptor> Microphones { get; } = new();
    public ObservableCollection<AudioDeviceDescriptor> CableCandidates { get; } = new();

    public IAsyncRelayCommand RefreshDevicesCommand { get; }
    public IAsyncRelayCommand StartCommand { get; }
    public IAsyncRelayCommand StopCommand { get; }
    public IAsyncRelayCommand ResetSettingsCommand { get; }
    public IAsyncRelayCommand CheckUpdatesCommand { get; }
    public string AppVersion => $"v{typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";
    public string UpdateButtonText => Text(IsCheckingUpdates ? MessageKey.Checking : MessageKey.CheckUpdates);
    public event EventHandler? UpdateReady;
    public DownloadedUpdate? DownloadedUpdate { get; private set; }
    public bool IsDownloadingUpdate
    {
        get => _isDownloadingUpdate;
        private set { if (SetProperty(ref _isDownloadingUpdate, value)) CheckUpdatesCommand.NotifyCanExecuteChanged(); }
    }
    public UpdateDownloadProgress? DownloadProgress { get => _downloadProgress; private set => SetProperty(ref _downloadProgress, value); }
    public string DownloadMessage => _localizer.Render(_downloadMessage);
    public bool IsCheckingUpdates
    {
        get => _isCheckingUpdates;
        private set
        {
            if (!SetProperty(ref _isCheckingUpdates, value)) return;
            OnPropertyChanged(nameof(UpdateButtonText));
            CheckUpdatesCommand.NotifyCanExecuteChanged();
        }
    }
    public UpdateCheckResult? UpdateResult { get => _updateResult; private set => SetProperty(ref _updateResult, value); }
    public bool IsUpdateDialogOpen { get => _isUpdateDialogOpen; private set => SetProperty(ref _isUpdateDialogOpen, value); }
    public void DismissUpdate() { if (!IsDownloadingUpdate) IsUpdateDialogOpen = false; }
    public string UpdateMessage => _localizer.Render(_updateMessage);
    private LocalizedMessage UpdateNotice
    {
        get => _updateMessage;
        set => SetProperty(ref _updateMessage, value, nameof(UpdateMessage));
    }
    public string SettingsMessage => _localizer.Render(_settingsMessage);
    private LocalizedMessage SettingsNotice
    {
        get => _settingsMessage;
        set { if (SetProperty(ref _settingsMessage, value, nameof(SettingsMessage))) { OnPropertyChanged(nameof(HasSettingsMessage)); } }
    }
    public bool HasSettingsMessage => !string.IsNullOrEmpty(SettingsMessage);
    public bool CanResetSettings => _initialized && !_shuttingDown && !_resetting && !StartCommand.IsRunning && !StopCommand.IsRunning;
    public bool CanEditSettings => !_shuttingDown && !_resetting && !StartCommand.IsRunning && !StopCommand.IsRunning;

    public AudioDeviceDescriptor? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
            {
                UpdateTargetMessage();
                RefreshComputedState();
                ScheduleSave();
            }
        }
    }

    public AudioDeviceDescriptor? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (SetProperty(ref _selectedTarget, value))
            {
                UpdateTargetMessage();
                RefreshComputedState();
                ScheduleSave();
            }
        }
    }

    public AudioDeviceDescriptor? SelectedMicrophone
    {
        get => _selectedMicrophone;
        set
        {
            if (SetProperty(ref _selectedMicrophone, value))
            {
                UpdateTargetMessage();
                RefreshComputedState();
                ScheduleSave();
            }
        }
    }

    public string StatusText => _localizer.Render(_statusText);
    private LocalizedMessage Status
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value, nameof(StatusText));
    }

    public string TargetMessage => _localizer.Render(_targetMessage);
    private LocalizedMessage TargetNotice
    {
        get => _targetMessage;
        set => SetProperty(ref _targetMessage, value, nameof(TargetMessage));
    }

    public string MonitorCaption => _localizer.Render(_monitorCaption);
    private LocalizedMessage Monitor
    {
        get => _monitorCaption;
        set => SetProperty(ref _monitorCaption, value, nameof(MonitorCaption));
    }

    public string SignalState => _localizer.Render(_signalState);
    private LocalizedMessage Signal
    {
        get => _signalState;
        set => SetProperty(ref _signalState, value, nameof(SignalState));
    }

    public IReadOnlyList<float> Waveform
    {
        get => _waveform;
        private set => SetProperty(ref _waveform, value);
    }

    public double LevelPercent
    {
        get => _levelPercent;
        private set
        {
            if (SetProperty(ref _levelPercent, value))
            {
                OnPropertyChanged(nameof(LevelLabel));
            }
        }
    }

    public string LevelLabel => $"{LevelPercent:0}%";

    public double GainDb
    {
        get => _gainDb;
        set
        {
            if (SetProperty(ref _gainDb, value))
            {
                OnPropertyChanged(nameof(GainLabel));
                PushProcessing();
            }
        }
    }

    public string GainLabel => $"{GainDb:+0.0;-0.0;0.0} dB";

    public bool CompressorEnabled
    {
        get => _compressorEnabled;
        set
        {
            if (SetProperty(ref _compressorEnabled, value))
            {
                PushProcessing();
            }
        }
    }

    public double CompressorThresholdDb
    {
        get => _compressorThresholdDb;
        set
        {
            if (SetProperty(ref _compressorThresholdDb, value))
            {
                OnPropertyChanged(nameof(ThresholdLabel));
                PushProcessing();
            }
        }
    }

    public string ThresholdLabel => Text(MessageKey.ThresholdValue, CompressorThresholdDb.ToString("0.0"));

    public double CompressorRatio
    {
        get => _compressorRatio;
        set
        {
            if (SetProperty(ref _compressorRatio, value))
            {
                OnPropertyChanged(nameof(RatioLabel));
                PushProcessing();
            }
        }
    }

    public string RatioLabel => Text(MessageKey.RatioValue, CompressorRatio.ToString("0.0"));

    public bool IsMicMixEnabled
    {
        get => _isMicMixEnabled;
        set
        {
            if (SetProperty(ref _isMicMixEnabled, value))
            {
                PushMicrophone();
                UpdateTargetMessage();
                RefreshComputedState();
            }
        }
    }

    public double MicGainDb
    {
        get => _micGainDb;
        set
        {
            if (SetProperty(ref _micGainDb, value))
            {
                OnPropertyChanged(nameof(MicGainLabel));
                PushMicrophone();
            }
        }
    }

    public string MicGainLabel => $"{Text(MessageKey.MicrophoneGain)} {MicGainDb:+0.0;-0.0;0.0} dB";

    public decimal SampleRate
    {
        get => _sampleRate;
        set
        {
            if (SetProperty(ref _sampleRate, value))
            {
                OnPropertyChanged(nameof(EngineLine));
                ScheduleSave();
            }
        }
    }

    public decimal Channels
    {
        get => _channels;
        set
        {
            if (SetProperty(ref _channels, value))
            {
                OnPropertyChanged(nameof(EngineLine));
                ScheduleSave();
            }
        }
    }

    public decimal LatencyMilliseconds
    {
        get => _latencyMilliseconds;
        set { if (SetProperty(ref _latencyMilliseconds, value)) ScheduleSave(); }
    }

    public decimal BufferMilliseconds
    {
        get => _bufferMilliseconds;
        set { if (SetProperty(ref _bufferMilliseconds, value)) ScheduleSave(); }
    }

    public bool IsRouting
    {
        get => _isRouting;
        private set
        {
            if (SetProperty(ref _isRouting, value))
            {
                RefreshComputedState();
            }
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set { if (SetProperty(ref _minimizeToTray, value)) { OnPropertyChanged(nameof(TrayHint)); ScheduleSave(); } }
    }
    public bool CloseToTray
    {
        get => _closeToTray;
        set { if (SetProperty(ref _closeToTray, value)) { OnPropertyChanged(nameof(TrayHint)); ScheduleSave(); } }
    }
    public string TrayHint => Text((CloseToTray, MinimizeToTray) switch
    {
        (true, true) => MessageKey.TrayContinues,
        (true, false) => MessageKey.TrayCloseOnly,
        (false, true) => MessageKey.TrayMinimizeOnly,
        _ => MessageKey.CloseExits
    });
    public string NoticeText => _localizer.Render(_noticeText);
    private LocalizedMessage Notice
    {
        get => _noticeText;
        set { if (SetProperty(ref _noticeText, value, nameof(NoticeText))) { OnPropertyChanged(nameof(HasNotice)); } }
    }
    public bool HasNotice => !string.IsNullOrEmpty(NoticeText);
    public bool ShouldShowUsageOnStartup => _hideUsageOnStartup != true;
    public bool DontShowUsageAgain => _hideUsageOnStartup ?? true;

    public void SetUsageGuidePreference(bool dontShowAgain)
    {
        if (_hideUsageOnStartup == dontShowAgain) return;
        _hideUsageOnStartup = dontShowAgain;
        OnPropertyChanged(nameof(ShouldShowUsageOnStartup));
        OnPropertyChanged(nameof(DontShowUsageAgain));
        ScheduleSave();
    }
    public bool CanEditRoute => !IsRouting && CanEditSettings;
    public bool IsStartEnabled => CanEditRoute && (_router is IAudioGraphController
        ? GraphValidator.GetError(Graph, true, Targets.ToArray(), Microphones.ToArray()) is null
        : RouteValidator.GetError(SelectedSource, SelectedTarget, SelectedMicrophone, IsMicMixEnabled) is null);
    public bool IsStopEnabled => IsRouting && CanEditSettings;
    public string RouteLine => SelectedSource is null || SelectedTarget is null ? Text(MessageKey.NoRoute) : $"{SelectedSource.Name} -> {SelectedTarget.Name}";
    public string EngineLine => Text(MessageKey.EngineSummary, SampleRate / 1000, Text(Channels == 1 ? MessageKey.Mono : MessageKey.Stereo));

    public async Task InitializeAsync()
    {
        try { _startWithWindows = _startup.IsEnabled; }
        catch (Exception ex) { Notice = new(MessageKey.Detail, ex); }
        OnPropertyChanged(nameof(StartWithWindows));
        try
        {
            _loadedSettings = await _settings.LoadAsync();
            Notice = _settings.LoadWarning ?? LocalizedMessage.Empty;
        }
        catch (Exception ex)
        {
            _loadedSettings = new AppSettings();
            Notice = new(MessageKey.SettingsLoadFailed, ex);
        }
        ApplySettings(_loadedSettings);
        await RefreshDevicesAsync();
        _graph = _loadedSettings.Graph ?? GraphMigration.FromSettings(CaptureSettings());
        _advancedMode = _loadedSettings.AdvancedMode;
        _initialized = true;
        RefreshComputedState();
    }

    public Task CheckUpdatesOnStartupAsync()
    {
        if (_startupUpdatesChecked || _shuttingDown) return Task.CompletedTask;
        _startupUpdatesChecked = true;
        return CheckUpdatesAsync();
    }

    private Task CheckUpdatesAsync()
    {
        if (_shuttingDown) return Task.CompletedTask;
        if (!_updateTask.IsCompleted) return _updateTask;
        _updateTask = PerformUpdateCheckAsync();
        return _updateTask;
    }

    private async Task PerformUpdateCheckAsync()
    {
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        IsCheckingUpdates = true;
        UpdateNotice = new(MessageKey.UpdateChecking);
        try
        {
            var result = await _updates.CheckAsync(cancellation.Token);
            if (_shuttingDown) return;
            if (result.InstallerUrl != UpdateResult?.InstallerUrl) DownloadedUpdate = null;
            UpdateResult = result;
            UpdateNotice = result.Message;
            IsUpdateDialogOpen = result.State == UpdateCheckState.Available;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!_shuttingDown)
            {
                UpdateResult = null;
                IsUpdateDialogOpen = false;
                UpdateNotice = new(MessageKey.UpdateCheckFailed, ex);
            }
        }
        finally { _updateCancellation = null; IsCheckingUpdates = false; }
    }

    public void CancelUpdateDownload() => _downloadCancellation?.Cancel();
    public void DownloadUpdate()
    {
        if (_shuttingDown || IsDownloadingUpdate) return;
        if (DownloadedUpdate is not null) { UpdateReady?.Invoke(this, EventArgs.Empty); return; }
        if (_downloads is null || UpdateResult?.State != UpdateCheckState.Available) throw new InvalidOperationException().WithMessage(MessageKey.ActionUnavailable);
        _downloadTask = DownloadUpdateAsync(UpdateResult);
    }
    private async Task DownloadUpdateAsync(UpdateCheckResult result)
    {
        using var cancellation = new CancellationTokenSource();
        _downloadCancellation = cancellation;
        IsDownloadingUpdate = true;
        DownloadProgress = null;
        _downloadMessage = new(MessageKey.UpdateDownloading);
        OnPropertyChanged(nameof(DownloadMessage));
        try
        {
            DownloadedUpdate = await _downloads!.DownloadAsync(result, new Progress<UpdateDownloadProgress>(p =>
            {
                if (!IsDownloadingUpdate) return;
                DownloadProgress = p;
                _downloadMessage = new(p.Verifying ? MessageKey.UpdateVerifying : MessageKey.UpdateDownloading);
                OnPropertyChanged(nameof(DownloadMessage));
            }), cancellation.Token);
            _downloadMessage = new(MessageKey.UpdateReadyToInstall);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { _downloadMessage = new(MessageKey.UpdateDownloadCancelled); }
        catch (Exception ex) { _downloadMessage = new(MessageKey.UpdateDownloadError, ex); }
        finally
        {
            _downloadCancellation = null;
            IsDownloadingUpdate = false;
            OnPropertyChanged(nameof(DownloadMessage));
            OnPropertyChanged(nameof(DownloadedUpdate));
        }
        if (DownloadedUpdate is not null && !_shuttingDown) UpdateReady?.Invoke(this, EventArgs.Empty);
    }

    private async Task ResetSettingsAsync()
    {
        _resetting = true;
        _initialized = false;
        RefreshComputedState();
        try
        {
            _saveCancellation?.Cancel();
            await _pendingSave;
            await _router.StopAsync();
            IsRouting = false;
            _loadedSettings = new AppSettings();
            ApplySettings(_loadedSettings);
            StartWithWindows = false;
            Notice = LocalizedMessage.Empty;
            await RefreshDevicesAsync();
            _graph = GraphMigration.FromSettings(CaptureSettings());
            _advancedMode = false;
            GraphRevision++;
            OnPropertyChanged(nameof(Graph));
            OnPropertyChanged(nameof(AdvancedMode));
            await SaveCurrentSettingsAsync();
            LevelPercent = 0;
            Waveform = Array.Empty<float>();
            Signal = new(MessageKey.NoSignal);
            if (!HasNotice) Status = new(MessageKey.Ready);
            SettingsNotice = new(MessageKey.SettingsResetDone);
        }
        catch (Exception ex) { SettingsNotice = new(MessageKey.SettingsResetFailed, ex); }
        finally
        {
            _resetting = false;
            _initialized = true;
            RefreshComputedState();
        }
    }

    private async Task RefreshDevicesAsync()
    {
        try
        {
            var saved = _initialized ? CaptureSettings() : _loadedSettings;
            Refill(Sources, _devices.GetLoopbackSources());
            Refill(Targets, _devices.GetRenderTargets());
            Refill(Microphones, _devices.GetMicrophones());
            Refill(CableCandidates, Targets.Where(device => device.IsVirtualCableCandidate));

            SelectedSource = SelectDevice(Sources, saved.SourceDeviceId) ?? Sources.FirstOrDefault(device => device.IsDefault) ?? Sources.FirstOrDefault();
            SelectedTarget = SelectDevice(Targets, saved.TargetDeviceId)
                ?? Targets.FirstOrDefault(device => device.VirtualCableKind == VirtualCableKind.VirtualAudioCable)
                ?? Targets.FirstOrDefault(device => device.IsVirtualCableCandidate)
                ?? Targets.FirstOrDefault();
            SelectedMicrophone = SelectDevice(Microphones, saved.MicrophoneDeviceId) ?? Microphones.FirstOrDefault(device => device.IsDefault) ?? Microphones.FirstOrDefault();
            UpdateTargetMessage();
        }
        catch (Exception ex)
        {
            Refill(Sources, Array.Empty<AudioDeviceDescriptor>());
            Refill(Targets, Array.Empty<AudioDeviceDescriptor>());
            Refill(Microphones, Array.Empty<AudioDeviceDescriptor>());
            Refill(CableCandidates, Array.Empty<AudioDeviceDescriptor>());
            Status = new(MessageKey.DeviceDiscoveryFailed);
            Notice = new(MessageKey.Detail, ex);
        }
        finally
        {
            RefreshComputedState();
        }
    }

    private async Task StartAsync()
    {
        if (_router is not IAudioGraphController && (SelectedSource is null || SelectedTarget is null))
        {
            return;
        }

        try
        {
            await SaveCurrentSettingsAsync();
            if (_router is IAudioGraphController graphRouter)
                await graphRouter.StartGraphAsync(Graph, CurrentEngine());
            else await _router.StartAsync(CurrentRouteConfig());
        }
        catch (Exception ex)
        {
            Status = new(MessageKey.StartFailed);
            Notice = new(MessageKey.Detail, ex);
            IsRouting = false;
        }
    }

    private async Task StopAsync()
    {
        try
        {
            await _router.StopAsync();
            await SaveCurrentSettingsAsync();
        }
        catch (Exception ex)
        {
            Notice = new(MessageKey.StopOrSaveFailed, ex);
            IsRouting = false;
        }
    }

    private RouteConfig CurrentRouteConfig() => new(
        SelectedSource!.Id,
        SelectedTarget!.Id,
        SelectedMicrophone?.Id,
        new AudioEngineSettings((int)SampleRate, (int)Channels, (int)LatencyMilliseconds, (int)BufferMilliseconds),
        CurrentProcessing(),
        CurrentMicrophone());

    private ProcessingSettings CurrentProcessing() => new(
        (float)GainDb,
        CompressorEnabled,
        (float)CompressorThresholdDb,
        (float)CompressorRatio,
        LimiterEnabled: _limiterEnabled);

    private MicMixSettings CurrentMicrophone() => new(IsMicMixEnabled, (float)MicGainDb);

    private AppSettings CaptureSettings() => new()
        {
            Graph = _graph,
            AdvancedMode = _advancedMode,
            SimpleBusId = _simpleBusId,
            SimpleWindow = _simpleWindow,
            AdvancedWindow = _advancedWindow,
            Language = Language,
            SourceDeviceId = SelectedSource?.Id,
            TargetDeviceId = SelectedTarget?.Id,
            MicrophoneDeviceId = SelectedMicrophone?.Id,
            MinimizeToTray = MinimizeToTray,
            CloseToTray = CloseToTray,
            HideUsageOnStartup = _hideUsageOnStartup,
            Processing = CurrentProcessing(),
            Microphone = CurrentMicrophone(),
            Engine = new AudioEngineSettings((int)SampleRate, (int)Channels, (int)LatencyMilliseconds, (int)BufferMilliseconds)
        };

    private async Task SaveCurrentSettingsAsync()
    {
        _saveCancellation?.Cancel();
        await _pendingSave;
        await _settings.SaveAsync(CaptureSettings());
    }

    private void ScheduleSave()
    {
        if (!_initialized || _shuttingDown) return;
        _saveCancellation?.Cancel();
        _saveCancellation?.Dispose();
        _saveCancellation = new CancellationTokenSource();
        _pendingSave = SaveLaterAsync(CaptureSettings(), _saveCancellation.Token);
    }

    private async Task SaveLaterAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(350, cancellationToken);
            await _settings.SaveAsync(settings, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { Notice = new(MessageKey.SettingsSaveFailed, ex); }
    }

    public async Task<bool> ShutdownAsync()
    {
        _shuttingDown = true;
        _updateCancellation?.Cancel();
        _downloadCancellation?.Cancel();
        RefreshComputedState();
        try
        {
            await _updateTask;
            await _downloadTask;
            // A pending Start can still be waiting for settings I/O. Finish it
            // before Stop so it cannot open an audio session after shutdown.
            if (StartCommand.ExecutionTask is { IsCompleted: false } starting) await starting;
            if (StopCommand.ExecutionTask is { IsCompleted: false } stopping) await stopping;
            if (ResetSettingsCommand.ExecutionTask is { IsCompleted: false } resetting) await resetting;
            await _router.StopAsync();
            await SaveCurrentSettingsAsync();
            return true;
        }
        catch (Exception ex)
        {
            Notice = new(MessageKey.ShutdownFailed, ex);
            CancelShutdown();
            return false;
        }
    }

    public void CancelShutdown()
    {
        _shuttingDown = false;
        RefreshComputedState();
    }

    private void ApplySettings(AppSettings settings)
    {
        _simpleWindow = settings.SimpleWindow;
        _advancedWindow = settings.AdvancedWindow;
        _limiterEnabled = settings.Processing.LimiterEnabled;
        _simpleBusId = settings.SimpleBusId;
        Language = LanguageCatalog.Resolve(settings.Language, _initialLanguage);
        MinimizeToTray = settings.MinimizeToTray;
        CloseToTray = settings.CloseToTray ?? settings.MinimizeToTray;
        _hideUsageOnStartup = settings.HideUsageOnStartup;
        OnPropertyChanged(nameof(ShouldShowUsageOnStartup));
        OnPropertyChanged(nameof(DontShowUsageAgain));
        GainDb = settings.Processing.GainDb;
        CompressorEnabled = settings.Processing.CompressorEnabled;
        CompressorThresholdDb = settings.Processing.CompressorThresholdDb;
        CompressorRatio = settings.Processing.CompressorRatio;
        IsMicMixEnabled = settings.Microphone.Enabled;
        MicGainDb = settings.Microphone.GainDb;
        SampleRate = settings.Engine.SampleRate;
        Channels = settings.Engine.Channels;
        LatencyMilliseconds = settings.Engine.LatencyMilliseconds;
        BufferMilliseconds = settings.Engine.BufferMilliseconds;
    }

    private void PushProcessing() { _router.UpdateProcessing(CurrentProcessing()); SyncSimpleGraph(); ScheduleSave(); }

    private void PushMicrophone() { _router.UpdateMicrophoneMix(CurrentMicrophone()); SyncSimpleGraph(); ScheduleSave(); }

    private void OnStatusChanged(object? sender, RouteStatusSnapshot snapshot)
    {
        _dispatcher.Post(() =>
        {
            Status = snapshot.State == RouteState.Faulted ? new(MessageKey.RouteError) : snapshot.Message;
            if (snapshot.State == RouteState.Faulted) Notice = snapshot.Message;
            IsRouting = snapshot.State is RouteState.Starting or RouteState.Running or RouteState.Stopping;
            Monitor = snapshot.State == RouteState.Running ? new(MessageKey.MonitorRouting) : new(MessageKey.WaitingPlayback);
        });
    }

    private void OnLevelChanged(object? sender, LevelMeterSnapshot snapshot)
    {
        _dispatcher.Post(() =>
        {
            LevelPercent = Math.Clamp(snapshot.Rms * 100, 0, 100);
            Signal = snapshot.Peak > 0.01f ? new(MessageKey.SignalDetected) : new(MessageKey.NoSignal);
            Waveform = snapshot.Waveform;
        });
    }

    private void UpdateTargetMessage()
    {
        var error = RouteValidator.GetError(SelectedSource, SelectedTarget, SelectedMicrophone, IsMicMixEnabled);
        TargetNotice = error is { } key ? new(key) : (SelectedTarget switch
        {
            null => new(MessageKey.SelectCable),
            { IsVirtualCableCandidate: true } => new(MessageKey.CableInputHint),
            _ => new(MessageKey.NotVirtualCable)
        });
    }

    private void RefreshComputedState()
    {
        OnPropertyChanged(nameof(CanEditRoute));
        OnPropertyChanged(nameof(CanEditSettings));
        OnPropertyChanged(nameof(CanResetSettings));
        OnPropertyChanged(nameof(IsStartEnabled));
        OnPropertyChanged(nameof(IsStopEnabled));
        OnPropertyChanged(nameof(RouteLine));
        OnPropertyChanged(nameof(EngineLine));
        RefreshDevicesCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        ResetSettingsCommand.NotifyCanExecuteChanged();
    }

    private static AudioDeviceDescriptor? SelectDevice(IEnumerable<AudioDeviceDescriptor> devices, string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : devices.FirstOrDefault(device => string.Equals(device.Id, id, StringComparison.OrdinalIgnoreCase));

    private static void Refill<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}

