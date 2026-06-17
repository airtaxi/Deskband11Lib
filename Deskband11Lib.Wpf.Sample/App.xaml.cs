using System.Windows;

namespace Deskband11Lib.Wpf.Sample;

public partial class App : Application
{
    private MainWindow? _window;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await InitializeMainWindowAsync();
    }

    private async Task InitializeMainWindowAsync()
    {
        var window = new MainWindow();
        _window = window;
        window.TaskbarContentHost.TaskbarWindowRecreated += OnTaskbarContentHostTaskbarWindowRecreated;

        await window.PrepareTaskbarContentAsync();
        window.Show();
    }

    private async void OnTaskbarContentHostTaskbarWindowRecreated(object? sender, EventArgs e)
    {
        _window?.TaskbarContentHost.TaskbarWindowRecreated -= OnTaskbarContentHostTaskbarWindowRecreated;
        _window = null;

        await Task.Delay(1000);
        await InitializeMainWindowAsync();
    }
}
