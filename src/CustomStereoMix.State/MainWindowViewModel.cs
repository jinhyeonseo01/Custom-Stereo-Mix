using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CustomStereoMix.Core;

namespace CustomStereoMix.State;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IAudioDeviceService _devices;
    private readonly IAudioRouteController _router;
    private readonly ISettingsStore _settings;
    private readonly IUpdateService _updates;
    private readonly IUiDispatcher _dispatcher;
    private bool _resetting;
    private string _settingsMessage = string.Empty;
    private string _updateMessage = "GitHub Releases에서 새 버전을 확인할 수 있습니다.";
    private Task _updateTask = Task.CompletedTask;
    private CancellationTokenSource? _updateCancellation;
    private bool _startupUpdatesChecked;
    private bool _isCheckingUpdates;
    private bool _isUpdateDialogOpen;
    private UpdateCheckResult? _updateResult;
    private bool _initialized;
    private bool _shuttingDown;
    private bool _minimizeToTray = true;
    private bool? _hideUsageOnStartup;
    private string _noticeText = string.Empty;
    private AppSettings _loadedSettings = new();
    private CancellationTokenSource? _saveCancellation;
    private Task _pendingSave = Task.CompletedTask;
    private AudioDeviceDescriptor? _selectedSource;
    private AudioDeviceDescriptor? _selectedTarget;
    private AudioDeviceDescriptor? _selectedMicrophone;
    private string _statusText = "대기 중";
    private string _targetMessage = "No virtual cable target selected.";
    private string _monitorCaption = "재생음을 기다리는 중";
    private string _signalState = "신호 없음";
    private IReadOnlyList<float> _waveform = Array.Empty<float>();
    private double _levelPercent;
    private double _gainDb;
    private bool _compressorEnabled;
    private double _compressorThresholdDb = -12;
    private double _compressorRatio = 4;
    private bool _isMicMixEnabled;
    private double _micGainDb;
    private decimal _sampleRate = 48000;
    private decimal _channels = 2;
    private decimal _latencyMilliseconds = 50;
    private decimal _bufferMilliseconds = 250;
    private bool _isRouting;

    public MainWindowViewModel(IAudioDeviceService devices, IAudioRouteController router, ISettingsStore settings, IUpdateService? updates = null, IUiDispatcher? dispatcher = null)
    {
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
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync, () => !_shuttingDown && !IsCheckingUpdates);
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
    public string UpdateButtonText => IsCheckingUpdates ? "확인 중…" : "업데이트 확인";
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
    public void DismissUpdate() => IsUpdateDialogOpen = false;
    public string UpdateMessage { get => _updateMessage; private set => SetProperty(ref _updateMessage, value); }
    public string SettingsMessage
    {
        get => _settingsMessage;
        private set { if (SetProperty(ref _settingsMessage, value)) OnPropertyChanged(nameof(HasSettingsMessage)); }
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

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string TargetMessage
    {
        get => _targetMessage;
        private set => SetProperty(ref _targetMessage, value);
    }

    public string MonitorCaption
    {
        get => _monitorCaption;
        private set => SetProperty(ref _monitorCaption, value);
    }

    public string SignalState
    {
        get => _signalState;
        private set => SetProperty(ref _signalState, value);
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

    public string ThresholdLabel => $"기준 {CompressorThresholdDb:0.0} dB";

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

    public string RatioLabel => $"압축 비율 {CompressorRatio:0.0}:1";

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

    public string MicGainLabel => $"마이크 볼륨 {MicGainDb:+0.0;-0.0;0.0} dB";

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
    public string TrayHint => MinimizeToTray ? "닫기 · 최소화하면 트레이에서 계속 실행됩니다" : "닫으면 프로그램이 종료됩니다";
    public string NoticeText
    {
        get => _noticeText;
        private set { if (SetProperty(ref _noticeText, value)) OnPropertyChanged(nameof(HasNotice)); }
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
    public bool IsStartEnabled => CanEditRoute && RouteValidator.GetError(SelectedSource, SelectedTarget, SelectedMicrophone, IsMicMixEnabled) is null;
    public bool IsStopEnabled => IsRouting && CanEditSettings;
    public string RouteLine => SelectedSource is null || SelectedTarget is null ? "No route selected" : $"{SelectedSource.Name} -> {SelectedTarget.Name}";
    public string EngineLine => $"{SampleRate / 1000:0.##} kHz · {(Channels == 1 ? "모노" : "스테레오")}";

    public async Task InitializeAsync()
    {
        try
        {
            _loadedSettings = await _settings.LoadAsync();
            NoticeText = _settings.LoadWarning ?? string.Empty;
        }
        catch (Exception ex)
        {
            _loadedSettings = new AppSettings();
            NoticeText = $"설정 로드 실패: {ex.Message}";
        }
        ApplySettings(_loadedSettings);
        await RefreshDevicesAsync();
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
        UpdateMessage = "새 버전을 확인하고 있습니다…";
        try
        {
            var result = await _updates.CheckAsync(cancellation.Token);
            if (_shuttingDown) return;
            UpdateResult = result;
            UpdateMessage = result.Message;
            IsUpdateDialogOpen = result.State == UpdateCheckState.Available;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!_shuttingDown)
            {
                UpdateResult = null;
                IsUpdateDialogOpen = false;
                UpdateMessage = $"업데이트 확인 실패: {ex.Message}";
            }
        }
        finally { _updateCancellation = null; IsCheckingUpdates = false; }
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
            NoticeText = string.Empty;
            await RefreshDevicesAsync();
            await SaveCurrentSettingsAsync();
            LevelPercent = 0;
            Waveform = Array.Empty<float>();
            SignalState = "신호 없음";
            if (!HasNotice) StatusText = "대기 중";
            SettingsMessage = "설정을 기본값으로 초기화했습니다. 소리 연결은 중지되었습니다.";
        }
        catch (Exception ex) { SettingsMessage = $"설정 초기화 실패: {ex.Message}"; }
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
            Refill(Microphones, _devices.GetMicrophones().Where(device => !device.IsVirtualCableCandidate));
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
            StatusText = "장치 검색 실패";
            NoticeText = ex.Message;
        }
        finally
        {
            RefreshComputedState();
        }
    }

    private async Task StartAsync()
    {
        if (SelectedSource is null || SelectedTarget is null)
        {
            return;
        }

        var config = CurrentRouteConfig();
        try
        {
            await SaveCurrentSettingsAsync();
            await _router.StartAsync(config);
        }
        catch (Exception ex)
        {
            StatusText = "시작 실패";
            NoticeText = ex.Message;
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
            NoticeText = $"중지 또는 설정 저장 실패: {ex.Message}";
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
        LimiterEnabled: true);

    private MicMixSettings CurrentMicrophone() => new(IsMicMixEnabled, (float)MicGainDb);

    private AppSettings CaptureSettings() => new()
        {
            SourceDeviceId = SelectedSource?.Id,
            TargetDeviceId = SelectedTarget?.Id,
            MicrophoneDeviceId = SelectedMicrophone?.Id,
            MinimizeToTray = MinimizeToTray,
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
        catch (Exception ex) { NoticeText = $"설정 저장 실패: {ex.Message}"; }
    }

    public async Task<bool> ShutdownAsync()
    {
        _shuttingDown = true;
        _updateCancellation?.Cancel();
        RefreshComputedState();
        try
        {
            await _updateTask;
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
            NoticeText = $"종료 준비 실패: {ex.Message}";
            _shuttingDown = false;
            RefreshComputedState();
            return false;
        }
    }

    private void ApplySettings(AppSettings settings)
    {
        MinimizeToTray = settings.MinimizeToTray;
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

    private void PushProcessing() { _router.UpdateProcessing(CurrentProcessing()); ScheduleSave(); }

    private void PushMicrophone() { _router.UpdateMicrophoneMix(CurrentMicrophone()); ScheduleSave(); }

    private void OnStatusChanged(object? sender, RouteStatusSnapshot snapshot)
    {
        _dispatcher.Post(() =>
        {
            StatusText = snapshot.State == RouteState.Faulted ? "연결 오류" : snapshot.Message;
            if (snapshot.State == RouteState.Faulted) NoticeText = snapshot.Message;
            IsRouting = snapshot.State is RouteState.Starting or RouteState.Running or RouteState.Stopping;
            MonitorCaption = snapshot.State == RouteState.Running ? "가상 케이블로 보내는 신호" : "재생음을 기다리는 중";
        });
    }

    private void OnLevelChanged(object? sender, LevelMeterSnapshot snapshot)
    {
        _dispatcher.Post(() =>
        {
            LevelPercent = Math.Clamp(snapshot.Rms * 100, 0, 100);
            SignalState = snapshot.Peak > 0.01f ? "신호 감지됨" : "신호 없음";
            Waveform = snapshot.Waveform;
        });
    }

    private void UpdateTargetMessage()
    {
        var error = RouteValidator.GetError(SelectedSource, SelectedTarget, SelectedMicrophone, IsMicMixEnabled);
        TargetMessage = error ?? (SelectedTarget switch
        {
            null => "가상 케이블을 선택하세요.",
            { IsVirtualCableCandidate: true } => "녹음 앱에서는 이 케이블의 녹음 장치를 선택하세요.",
            _ => "가상 케이블이 아닌 출력 장치입니다."
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

