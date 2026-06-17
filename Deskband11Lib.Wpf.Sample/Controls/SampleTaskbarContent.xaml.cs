using System.Windows;
using System.Windows.Controls;

namespace Deskband11Lib.Wpf.Sample.Controls;

public partial class SampleTaskbarContent : UserControl
{
    public SampleTaskbarContent() => InitializeComponent();

    private void OnCloseButtonClicked(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
