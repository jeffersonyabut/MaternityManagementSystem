
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

    private async void SaveUniSmsApiKeyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not SystemSettingsViewModel viewModel)
        {
            return;
        }

        string apiKey = UniSmsApiKeyPasswordBox.Password;

        try
        {
            await viewModel.SaveUniSmsApiKeyAsync(apiKey);
        }
        finally
        {
            UniSmsApiKeyPasswordBox.Clear();
        }
    }

    private async void RemoveUniSmsApiKeyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not SystemSettingsViewModel viewModel)
        {
            return;
        }

        await viewModel.RemoveUniSmsApiKeyAsync();
    }

    private async void SendUniSmsTestButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not SystemSettingsViewModel viewModel ||
            viewModel.IsUniSmsBusy)
        {
            return;
        }

        await viewModel.SendUniSmsTestMessageAsync();
    }
}
