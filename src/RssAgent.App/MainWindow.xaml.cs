using System.Windows;
using System.Windows.Controls;
using RssAgent.App.ViewModels;

namespace RssAgent.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void RssBoxPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is PasswordBox box)
        {
            viewModel.Settings.RssBoxPassword = box.Password;
        }
    }

    private void GeminiApiKeyChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is PasswordBox box)
        {
            viewModel.Settings.GeminiApiKey = box.Password;
        }
    }
}
