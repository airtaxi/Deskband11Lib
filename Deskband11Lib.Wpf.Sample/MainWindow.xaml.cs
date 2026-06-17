using Deskband11Lib.Wpf;
using System.Windows;

namespace Deskband11Lib.Wpf.Sample;

public partial class MainWindow : Window
{
    public TaskbarContentHost TaskbarContentHost { get; }

    public MainWindow()
    {
        InitializeComponent();
        TaskbarContentHost = new TaskbarContentHost(this, (FrameworkElement)Content, new() { PreferredWidth = double.MaxValue });
    }

    public async Task PrepareTaskbarContentAsync() => await TaskbarContentHost.AttachWhenLayoutReadyAsync();

    protected override void OnClosed(EventArgs e)
    {
        TaskbarContentHost.Dispose();
        base.OnClosed(e);
    }
}
