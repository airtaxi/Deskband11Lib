using Deskband11Lib;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Deskband11Lib.Sample;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    public TaskbarContentHost TaskbarContentHost { get; }

    public MainWindow()
    {
        InitializeComponent();

        TaskbarContentHost ??= new TaskbarContentHost(this, (FrameworkElement)Content, new() { PreferredWidth = 2800 });
    }

    public async Task PrepareTaskbarContentAsync() => await TaskbarContentHost.AttachWhenLayoutReadyAsync();

    private void OnWindowClosed(object sender, WindowEventArgs e) => TaskbarContentHost?.Dispose();
}
