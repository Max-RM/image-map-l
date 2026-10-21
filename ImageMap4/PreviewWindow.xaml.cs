using System.Windows;

namespace ImageMap4;
/// <summary>
/// Interaction logic for PreviewWindow.xaml
/// </summary>
public partial class PreviewWindow : Window
{
    public PreviewWindow(Map map)
    {
        DataContext = map;
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
