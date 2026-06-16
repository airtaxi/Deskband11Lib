using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Deskband11Lib.Sample;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private MainWindow? _window;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
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
