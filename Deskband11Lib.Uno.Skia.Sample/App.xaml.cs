using Microsoft.UI.Xaml;
using System.Runtime.Versioning;

namespace Deskband11Lib.Uno.Skia.Sample;

[SupportedOSPlatform("windows10.0.22000.0")]
public partial class App : Application
{
    private MainWindow? _window;

    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args) => await InitializeMainWindowAsync();

    private async Task InitializeMainWindowAsync()
    {
        var window = new MainWindow();
        _window = window;
#if DEBUG
        window.UseStudio();
#endif
        window.TaskbarContentHost.TaskbarWindowRecreated += OnTaskbarContentHostTaskbarWindowRecreated;

        await window.PrepareTaskbarContentAsync();
        window.Activate();
    }

    private async void OnTaskbarContentHostTaskbarWindowRecreated(object? sender, EventArgs e)
    {
        _window?.TaskbarContentHost.TaskbarWindowRecreated -= OnTaskbarContentHostTaskbarWindowRecreated;

        await Task.Delay(1000);
        await InitializeMainWindowAsync();
    }
}
