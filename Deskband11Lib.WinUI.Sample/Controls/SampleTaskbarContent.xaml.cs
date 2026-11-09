using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Deskband11Lib.WinUI.Sample.Controls;

public sealed partial class SampleTaskbarContent : UserControl
{
    public SampleTaskbarContent() => InitializeComponent();

    public void ApplyTaskbarOrientation(bool isVertical)
    {
        HorizontalLayoutBorder.Visibility = isVertical ? Visibility.Collapsed : Visibility.Visible;
        VerticalLayoutBorder.Visibility = isVertical ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCloseButtonClicked(object sender, RoutedEventArgs e) => Environment.Exit(0);
}
