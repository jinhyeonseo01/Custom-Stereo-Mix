using System.Windows;
using CustomStereoMix.Audio;
using CustomStereoMix.State;

namespace CustomStereoMix.App;

public partial class App : Application
{
    private NaudioDeviceService? _devices;
    private Mutex? _instance;
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
        var model = new MainWindowViewModel(_devices, new NaudioRouteController(_devices), new JsonSettingsStore(), dispatcher: new WpfUiDispatcher(Dispatcher));
        MainWindow = new MainWindow(model);
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _devices?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
