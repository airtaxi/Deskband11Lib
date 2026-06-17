using Deskband11Lib.Uno.Skia;
using Microsoft.UI.Xaml;
using System.Runtime.Versioning;

namespace Deskband11Lib.Uno.Skia.Sample;

[SupportedOSPlatform("windows10.0.22000.0")]
public sealed partial class MainWindow : Window
{
    public TaskbarContentHost TaskbarContentHost { get; }

    public MainWindow()
    {
        InitializeComponent();

        TaskbarContentHost ??= new TaskbarContentHost(this, (FrameworkElement)Content!, new() { PreferredWidth = double.MaxValue });
    }

    public async Task PrepareTaskbarContentAsync() => await TaskbarContentHost.AttachWhenLayoutReadyAsync();

    private void OnWindowClosed(object sender, WindowEventArgs e) => TaskbarContentHost?.Dispose();
}
