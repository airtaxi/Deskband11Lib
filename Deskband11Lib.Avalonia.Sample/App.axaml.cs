using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Deskband11Lib.Avalonia.Sample.Views;

namespace Deskband11Lib.Avalonia.Sample;

public class App : Application
{
    private MainWindow? _window;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        await InitializeMainWindowAsync();
        base.OnFrameworkInitializationCompleted();
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