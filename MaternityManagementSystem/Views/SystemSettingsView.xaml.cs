
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class SystemSettingsView : UserControl
{
    public SystemSettingsView()
    {
        InitializeComponent();
    }

    private async void SaveSettingsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not SystemSettingsViewModel viewModel)
        {
            MessageBox.Show(
                "System settings are not properly initialized.",
                "Golddie",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        await viewModel.SaveSettingsAsync();
    }
}
