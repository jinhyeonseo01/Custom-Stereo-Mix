using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using CustomStereoMix.State;
using CustomStereoMix.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Forms = System.Windows.Forms;

namespace CustomStereoMix.App;

public partial class MainWindow : Window
{
    private const string AppHost = "stereomix.example";
    private readonly MainWindowViewModel _model;
    private readonly UiBridge _bridge;
    private readonly string _browserData;
    private readonly Forms.NotifyIcon _tray;
    private readonly Forms.ToolStripMenuItem _startItem;
    private readonly Forms.ToolStripMenuItem _stopItem;
    private readonly DispatcherTimer _meterTimer;
    private bool _modelInitialized, _pageReady, _stateScheduled, _meterDirty, _shutdownComplete, _exiting, _starting;

    public Task Initialization { get; private set; } = Task.CompletedTask;

    public MainWindow(MainWindowViewModel model, string? browserData = null)
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            FitInitialWindow();
            System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle)?.AddHook(SessionMessage);
        };
        _model = model;
        SizeChanged += (_, _) =>
        {
            if (_modelInitialized && WindowState == WindowState.Normal && !_changingModeSize)
                _model.RememberWindowSize(ActualWidth, ActualHeight);
        };
        StartupMessage.Text = model.Text(MessageKey.PreparingInterface);
        RetryButton.Content = model.Text(MessageKey.Retry);
        RuntimeButton.Content = model.Text(MessageKey.RuntimeGuide);
        _browserData = browserData ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CustomStereoMix", "WebView2");
        _bridge = new UiBridge(model, () => OpenWebsiteAsync("https://vb-audio.com/Cable/"), uri => OpenWebsiteAsync(uri.AbsoluteUri));
        var menu = new Forms.ContextMenuStrip { Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Regular) };
        Forms.ToolStripMenuItem MenuItem(MessageKey key, EventHandler? click = null)
        {
            var item = new Forms.ToolStripMenuItem(model.Text(key)) { Tag = key };
            if (click is not null) item.Click += click;
            return item;
        }
        menu.Items.Add(MenuItem(MessageKey.OpenWindow, (_, _) => RestoreWindow()));
        menu.Items.Add(MenuItem(MessageKey.Guide, (_, _) => OpenView("help")));
        menu.Items.Add(MenuItem(MessageKey.Settings, (_, _) => OpenView("settings")));
        menu.Items.Add(new Forms.ToolStripSeparator());
        _startItem = MenuItem(MessageKey.StartRouting);
        _stopItem = MenuItem(MessageKey.TrayStopRouting);
        _startItem.Click += async (_, _) => await RunTrayCommand("start");
        _stopItem.Click += async (_, _) => await RunTrayCommand("stop");
        menu.Items.Add(_startItem); menu.Items.Add(_stopItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(MenuItem(MessageKey.Exit, async (_, _) => await ExitAsync()));
        using var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/CustomStereoMix.App;component/Assets/AppIcon.ico"))!.Stream;
        _tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(stream), ContextMenuStrip = menu, Text = "Custom Stereo Mix", Visible = model.MinimizeToTray };
        _tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) RestoreWindow(); };
        _model.PropertyChanged += ModelChanged;
        _model.UpdateReady += OnUpdateReady;
        _bridge.Changed += (_, _) => ScheduleState();
        _meterTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background, (_, _) =>
        {
            if ((_meterDirty || _model.IsRouting) && _pageReady && IsVisible)
            {
                _meterDirty = false;
                Post(new { type = "meter", meter = _bridge.Meter() });
            }
        }, Dispatcher);
        Loaded += async (_, _) => { Initialization = InitializeBrowserAsync(); await Initialization; };
        Closing += OnClosing;
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && _model.MinimizeToTray && !_exiting) HideToTray(); };
        Closed += (_, _) =>
        {
            _meterTimer.Stop(); _model.PropertyChanged -= ModelChanged;
            _model.UpdateReady -= OnUpdateReady;
            _tray.Visible = false; _tray.Dispose(); menu.Dispose(); Browser.Dispose();
        };
    }

    private IntPtr SessionMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Restart Manager requests an actual exit even when close-to-tray is enabled.
        if ((lParam.ToInt64() & 1) == 0) return IntPtr.Zero;
        if (message == 0x11) { handled = true; return new IntPtr(1); } // WM_QUERYENDSESSION
        if (message == 0x16 && wParam != IntPtr.Zero)
        { handled = true; Dispatcher.BeginInvoke(new Action(async () => await ExitAsync())); }
        return IntPtr.Zero;
    }

    private void OnUpdateReady(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(async () =>
    {
        if (_exiting || _shutdownComplete || _model.DownloadedUpdate is not { } update) return;
        RestoreWindow();
        if (System.Windows.MessageBox.Show(this, _model.Text(MessageKey.UpdateExitConfirmation), "Custom Stereo Mix", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
        try { await ExitAsync(update); }
        catch (Exception ex) { System.Windows.MessageBox.Show(this, _model.Text(MessageKey.UpdateLaunchFailed, ex), "Custom Stereo Mix", MessageBoxButton.OK, MessageBoxImage.Error); }
    }));

    private void FitInitialWindow()
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var screen = Forms.Screen.FromHandle(handle).WorkingArea;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var availableWidth = Math.Max(320, screen.Width / dpi.DpiScaleX - 24);
        var availableHeight = Math.Max(320, screen.Height / dpi.DpiScaleY - 24);
        MinWidth = Math.Min(MinWidth, availableWidth);
        MinHeight = Math.Min(MinHeight, availableHeight);
        Width = Math.Min(Width, availableWidth);
        Height = Math.Min(Height, availableHeight);
    }
    private bool _changingModeSize;
    private void ApplyModeWindowSize()
    {
        if (WindowState != WindowState.Normal) return;
        _changingModeSize = true;
        try { Width = _model.WindowSize.Width; Height = _model.WindowSize.Height; FitInitialWindow(); }
        finally { _changingModeSize = false; }
    }

    private async Task InitializeBrowserAsync()
    {
        if (_starting || _exiting) return;
        _starting = true; _pageReady = false;
        Browser.Visibility = Visibility.Hidden;
        StartupPanel.Visibility = Visibility.Visible;
        RetryButton.Visibility = RuntimeButton.Visibility = Visibility.Collapsed;
        StartupMessage.Text = _model.Text(MessageKey.PreparingInterface);
        try
        {
            if (!_modelInitialized)
            {
                await _model.InitializeAsync();
                _modelInitialized = true;
                ApplyModeWindowSize();
                UpdateTray();
                StartupMessage.Text = _model.Text(MessageKey.PreparingInterface);
            }
            var content = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            if (!File.Exists(Path.Combine(content, "index.html"))) throw new FileNotFoundException(_model.Text(MessageKey.InterfaceFilesMissing));
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _browserData);
            await Browser.EnsureCoreWebView2Async(environment);
            var core = Browser.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = Debugger.IsAttached;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.SetVirtualHostNameToFolderMapping(AppHost, content, CoreWebView2HostResourceAccessKind.DenyCors);
            core.WebMessageReceived -= OnWebMessage;
            core.WebMessageReceived += OnWebMessage;
            core.NavigationStarting -= OnNavigationStarting;
            core.NavigationStarting += OnNavigationStarting;
            core.ProcessFailed -= OnProcessFailed;
            core.ProcessFailed += OnProcessFailed;
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            await core.AddScriptToExecuteOnDocumentCreatedAsync($"window.__initialLanguage = {JsonSerializer.Serialize(_model.Language)};");
            core.Navigate($"https://{AppHost}/index.html");
        }
        catch (Exception ex)
        {
            StartupMessage.Text = ex is WebView2RuntimeNotFoundException
                ? _model.Text(MessageKey.RuntimeRequired)
                : _model.Text(MessageKey.InterfaceOpenFailed, ex);
            RetryButton.Visibility = Visibility.Visible;
            RuntimeButton.Visibility = ex is WebView2RuntimeNotFoundException ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _starting = false; }
    }

    private static bool IsAppUri(string source) => Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && uri.Host == AppHost && uri.IsDefaultPort;

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!IsAppUri(e.Uri)) e.Cancel = true;
        else _pageReady = false;
    }

    private async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!IsAppUri(e.Source)) return;
        try
        {
            var json = e.WebMessageAsJson;
            if (json.Length > 262144) return;
            using var message = JsonDocument.Parse(json);
            var response = await _bridge.HandleAsync(message.RootElement);
            if (_exiting) return;
            if (message.RootElement.TryGetProperty("command", out var command) && command.GetString() == "ready")
            {
                _pageReady = true;
                Browser.Visibility = Visibility.Visible;
                StartupPanel.Visibility = Visibility.Collapsed;
                _meterDirty = true;
            }
            Post(response);
            if (command.ValueKind == JsonValueKind.String && command.GetString() == "ready")
                _ = _model.CheckUpdatesOnStartupAsync();
        }
        catch (Exception ex) { Debug.WriteLine($"Web message rejected: {ex.Message}"); }
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _pageReady = false;
        Browser.Visibility = Visibility.Hidden;
        StartupMessage.Text = _model.Text(MessageKey.InterfaceStopped);
        StartupPanel.Visibility = RetryButton.Visibility = Visibility.Visible;
    }

    private async void RetryStartup(object sender, RoutedEventArgs e)
    {
        Browser.Dispose();
        var root = (System.Windows.Controls.Grid)Content;
        root.Children.Remove(Browser);
        UnregisterName("Browser");
        Browser = new WebView2 { Name = "Browser", DefaultBackgroundColor = System.Drawing.Color.FromArgb(245, 247, 251) };
        RegisterName("Browser", Browser);
        root.Children.Insert(0, Browser);
        Initialization = InitializeBrowserAsync(); await Initialization;
    }

    private async void OpenRuntimeWebsite(object sender, RoutedEventArgs e)
    {
        try { await OpenWebsiteAsync("https://developer.microsoft.com/microsoft-edge/webview2/"); }
        catch (Exception ex) { StartupMessage.Text = ex.Message; }
    }

    private static Task OpenWebsiteAsync(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private void Post(object message)
    {
        if (!_pageReady || Browser.CoreWebView2 is null || _shutdownComplete) return;
        try { Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, UiBridge.JsonOptions)); }
        catch (Exception ex) { Debug.WriteLine($"UI delivery paused: {ex.Message}"); }
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => ModelChanged(sender, e)); return; }
        if (e.PropertyName == nameof(MainWindowViewModel.AdvancedMode)) ApplyModeWindowSize();
        if (e.PropertyName is "Waveform" or "SignalState" or "LevelPercent" or "LevelLabel") _meterDirty = true;
        else { UpdateTray(); ScheduleState(); }
    }

    private void ScheduleState()
    {
        if (_stateScheduled || _shutdownComplete) return;
        _stateScheduled = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _stateScheduled = false;
            Post(new { type = "state", state = _bridge.Snapshot() });
        }));
    }

    private void UpdateTray()
    {
        foreach (var item in _tray.ContextMenuStrip!.Items.OfType<Forms.ToolStripMenuItem>())
            item.Text = _model.Text((MessageKey)item.Tag!);
        RetryButton.Content = _model.Text(MessageKey.Retry);
        RuntimeButton.Content = _model.Text(MessageKey.RuntimeGuide);
        _meterDirty = true;
        _tray.Visible = _model.MinimizeToTray && !_shutdownComplete;
        _tray.Text = $"Custom Stereo Mix · {_model.StatusText}";
        _startItem.Enabled = _model.IsStartEnabled;
        _stopItem.Enabled = _model.IsStopEnabled;
        if (!_model.MinimizeToTray && !IsVisible && _modelInitialized && !_exiting) RestoreWindow();
    }

    private async Task RunTrayCommand(string command)
    {
        using var message = JsonDocument.Parse(JsonSerializer.Serialize(new { id = Guid.NewGuid().ToString("N"), command }));
        await _bridge.HandleAsync(message.RootElement);
    }

    public void OpenView(string view) { RestoreWindow(); _bridge.OpenView(view); }
    public void RestoreWindow()
    {
        ShowInTaskbar = true; Show(); WindowState = WindowState.Normal; Activate(); _meterDirty = true; ScheduleState();
    }

    public void HideToTray()
    {
        _bridge.CommitGuideOnClose();
        _bridge.OpenView("main");
        ShowInTaskbar = false; Hide();
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_shutdownComplete) return;
        e.Cancel = true;
        if (_exiting) return;
        if (_model.MinimizeToTray && _tray.Visible) HideToTray();
        else await ExitAsync();
    }

    public async Task ExitAsync(DownloadedUpdate? update = null)
    {
        if (_exiting || _shutdownComplete) return;
        _exiting = true;
        try
        {
            await Initialization;
            _bridge.CommitGuideOnClose();
            if (!await _model.ShutdownAsync()) { RestoreWindow(); return; }
            // Spawn only after settings/audio shutdown succeeded. The helper then
            // waits for this process to exit before starting the installer.
            if (update is not null)
            {
                try { UpdateInstallerLauncher.StartHelper(update, _model.Language); }
                catch { _model.CancelShutdown(); RestoreWindow(); throw; }
            }
            _shutdownComplete = true;
            Close();
            System.Windows.Application.Current.Shutdown();
        }
        finally { _exiting = false; }
    }
}
