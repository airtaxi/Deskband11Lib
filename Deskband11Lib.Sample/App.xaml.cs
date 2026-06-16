using Microsoft.UI.Xaml;

namespace Deskband11Lib.Sample;

public partial class App : Application
{
    private MainWindow? _window;

    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args) => await InitializeMainWindow();

    private async Task InitializeMainWindow()
    {
        var window = new MainWindow();
        _window = window;
        window.TaskbarContentHost.TaskbarWindowRecreated += OnTaskbarContentHostTaskbarWindowRecreated;

        await window.PrepareTaskbarContentAsync();
        window.Activate();
    }

    private async void OnTaskbarContentHostTaskbarWindowRecreated(object? sender, EventArgs e)
    {
        // TaskbarContentHost clears its TaskbarWindowRecreated handlers during disposal, but unsubscribe explicitly to prevent leaks.
        _window?.TaskbarContentHost.TaskbarWindowRecreated -= OnTaskbarContentHostTaskbarWindowRecreated;
        // The taskbar has already disposed the hosted window. Do not call _window.Close(); it can throw an unrecoverable ExecutionEngineException.

        await Task.Delay(1000); // Wait for the taskbar icon animation to settle.
        await InitializeMainWindow();
    }
}
