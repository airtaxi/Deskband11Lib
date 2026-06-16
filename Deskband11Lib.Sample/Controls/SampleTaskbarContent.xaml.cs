using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Deskband11Lib.Sample.Controls;

public sealed partial class SampleTaskbarContent : UserControl
{
    public SampleTaskbarContent() => InitializeComponent();

    private void OnCloseButtonClicked(object sender, RoutedEventArgs e) => Application.Current.Exit();
}
