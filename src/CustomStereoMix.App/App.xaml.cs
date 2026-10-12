using System.Windows;
using System.Net.Http;
using CustomStereoMix.Audio;
using CustomStereoMix.State;

namespace CustomStereoMix.App;

public partial class App : Application
{
    private NaudioDeviceService? _devices;
    private Mutex? _instance;
    private HttpClient? _updatesClient;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instance = new Mutex(true, "Local\\CustomStereoMix.WebView2", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("Custom Stereo Mix가 이미 실행 중입니다. 트레이 아이콘에서 창을 열어 주세요.", "Custom Stereo Mix", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(); return;
        }
        _devices = new NaudioDeviceService();
        _updatesClient = new HttpClient();
        var updates = new GitHubReleaseUpdateService(_updatesClient, typeof(MainWindowViewModel).Assembly.GetName().Version!);
        var model = new MainWindowViewModel(_devices, new NaudioRouteController(_devices), new JsonSettingsStore(), updates, new WpfUiDispatcher(Dispatcher));
        MainWindow = new MainWindow(model);
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
