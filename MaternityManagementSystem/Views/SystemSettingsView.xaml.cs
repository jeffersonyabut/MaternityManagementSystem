
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
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

    private async void CreateDatabaseBackupButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not SystemSettingsViewModel viewModel)
        {
            MessageBox.Show(
                "Database backup is not available in this view.",
                "Golddie",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var folderDialog = new OpenFolderDialog
        {
            Title = "Select a secure folder for the database backup"
        };

        if (folderDialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        await viewModel.CreateDatabaseBackupAsync(
            folderDialog.FolderName);
    }
}
