using Deskband11Lib.WinUI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Deskband11Lib.WinUI.Sample;

public sealed partial class MainWindow : Window
{
    public TaskbarContentHost TaskbarContentHost { get; }

    public MainWindow()
    {
        InitializeComponent();

        TaskbarContentHost ??= new TaskbarContentHost(this, (FrameworkElement)Content, new() { PreferredWidth = double.MaxValue });
    }

    public async Task PrepareTaskbarContentAsync() => await TaskbarContentHost.AttachWhenLayoutReadyAsync();

    private void OnWindowClosed(object sender, WindowEventArgs e) => TaskbarContentHost?.Dispose();
}
