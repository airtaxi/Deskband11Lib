using Deskband11Lib.Wpf;
using System.Windows;

namespace Deskband11Lib.Wpf.Sample;

public partial class App : Application
{
    private static MainWindow? s_mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await InitializeMainWindowAsync();
    }

    private static async Task InitializeMainWindowAsync()
    {
        var mainWindow = new MainWindow();
        s_mainWindow = mainWindow;
        mainWindow.TaskbarContentHost.TaskbarWindowRecreationRequired += OnTaskbarContentHostTaskbarWindowRecreationRequired;

        await mainWindow.PrepareTaskbarContentAsync();
        mainWindow.Show();
    }

    // The hosted window is recreated when Explorer restarts, the monitor is disconnected, or the system DPI changes,
    // so the main window must be recreated to attach to the new taskbar.
    private static async void OnTaskbarContentHostTaskbarWindowRecreationRequired(object? sender, EventArgs e)
    {
        var mainWindow = s_mainWindow;
        if (mainWindow is not null) ReleaseMainWindow(mainWindow);

        // Wait for the taskbar to be ready before recreating the main window.
        await Task.Delay(1000);
        await InitializeMainWindowAsync();

        if (mainWindow?.IsWindowAlive() == true) mainWindow.Close();
    }

    private static void ReleaseMainWindow(MainWindow mainWindow)
    {
        mainWindow.TaskbarContentHost.Dispose();
        if (ReferenceEquals(s_mainWindow, mainWindow)) s_mainWindow = null;
    }
}
