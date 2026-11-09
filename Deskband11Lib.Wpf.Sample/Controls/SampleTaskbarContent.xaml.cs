using System.Windows;
using System.Windows.Controls;

namespace Deskband11Lib.Wpf.Sample.Controls;

public partial class SampleTaskbarContent : UserControl
{
    public SampleTaskbarContent() => InitializeComponent();

    public void ApplyTaskbarOrientation(bool isVertical)
    {
        HorizontalLayoutBorder.Visibility = isVertical ? Visibility.Collapsed : Visibility.Visible;
        VerticalLayoutBorder.Visibility = isVertical ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCloseButtonClicked(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
