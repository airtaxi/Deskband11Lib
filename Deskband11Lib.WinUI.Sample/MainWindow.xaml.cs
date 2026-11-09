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
        TaskbarContentHost.TaskbarEdgeChanged += OnTaskbarContentHostTaskbarEdgeChanged;
    }

    public async Task PrepareTaskbarContentAsync() => await TaskbarContentHost.AttachWhenLayoutReadyAsync();

    private void OnTaskbarContentHostTaskbarEdgeChanged(object? _, EventArgs __) => TaskbarContent.ApplyTaskbarOrientation(TaskbarContentHost.IsTaskbarVertical);

    private void OnWindowClosed(object sender, WindowEventArgs e) => TaskbarContentHost?.Dispose();
}
