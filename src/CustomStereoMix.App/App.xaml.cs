using System.Windows;
using System.Net.Http;
using CustomStereoMix.Audio;
using CustomStereoMix.State;
using CustomStereoMix.Core;

namespace CustomStereoMix.App;

public partial class App : Application
{
    private NaudioDeviceService? _devices;
    private Mutex? _instance;
    private HttpClient? _updatesClient;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.FirstOrDefault() == "--launch-installer")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            await UpdateInstallerLauncher.RunHelperAsync(e.Args);
            Shutdown(); return;
        }
        var initialLanguage = LanguageCatalog.InitialLanguage(AppContext.BaseDirectory);
        _instance = new Mutex(true, "Local\\CustomStereoMix.WebView2", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show(new Localizer(initialLanguage).Text(MessageKey.AlreadyRunning), "Custom Stereo Mix", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(); return;
        }
        _devices = new NaudioDeviceService();
        _updatesClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var updates = new GitHubReleaseUpdateService(_updatesClient, typeof(MainWindowViewModel).Assembly.GetName().Version!);
        var model = new MainWindowViewModel(_devices, new NaudioRouteController(_devices), new JsonSettingsStore(), updates, new WpfUiDispatcher(Dispatcher), initialLanguage,
            new WindowsStartupService(Path.Combine(AppContext.BaseDirectory, "CustomStereoMix.App.exe")),
            new UpdateDownloadService(_updatesClient, UpdateInstallerLauncher.CacheDirectory));
        MainWindow = new MainWindow(model);
        if (e.Args.Contains("--autostart"))
            MainWindow.Loaded += async (_, _) =>
            {
                await ((MainWindow)MainWindow).Initialization;
                if (model.MinimizeToTray && !model.ShouldShowUsageOnStartup) ((MainWindow)MainWindow).HideToTray();
            };
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _devices?.Dispose();
        _updatesClient?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
