using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Deskband11Lib.Avalonia.Sample.Controls;

public partial class SampleTaskbarContent : UserControl
{
    public SampleTaskbarContent() => InitializeComponent();

    private void OnCloseButtonClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime) lifetime.Shutdown();
    }
}