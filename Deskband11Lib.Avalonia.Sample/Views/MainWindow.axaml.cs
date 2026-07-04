using Avalonia.Controls;
using Deskband11Lib.Avalonia;
using Deskband11Lib.Avalonia.Sample.Controls;

namespace Deskband11Lib.Avalonia.Sample.Views;

public partial class MainWindow : Window
{
    public TaskbarContentHost TaskbarContentHost { get; }

    public MainWindow()
    {
        InitializeComponent();

        var content = new SampleTaskbarContent();
        Content = content;
        TaskbarContentHost = new TaskbarContentHost(this, content, new() { PreferredWidth = double.MaxValue });
    }

    public async Task PrepareTaskbarContentAsync() => await TaskbarContentHost.AttachWhenLayoutReadyAsync();

    protected override void OnClosed(EventArgs e)
    {
        TaskbarContentHost.Dispose();
        base.OnClosed(e);
    }
}