
using System.Windows;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.Views;

public partial class AdminSetupWindow : Window
{
    public AdminSetupWindow()
    {
        InitializeComponent();
    }

    private async void CreateAdminButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string username = UsernameInput.Text.Trim();
        string password = PasswordInput.Password;
        string confirmation = ConfirmPasswordInput.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            MessageText.Text = "Please enter a username.";
            return;
        }

        if (username.Length > 100)
        {
            MessageText.Text = "Username cannot exceed 100 characters.";
            return;
        }

        if (password.Length < 12)
        {
            MessageText.Text = "Use a password with at least 12 characters.";
            return;
        }

        if (password.Length > 1024)
        {
            MessageText.Text = "Password cannot exceed 1,024 characters.";
            return;
        }

        if (password != confirmation)
        {
            MessageText.Text = "Passwords do not match.";
            return;
        }

        CreateAdminButton.IsEnabled = false;

        try
        {
            var repository = new StaffRepository();

            if (await repository.HasAnyStaffAsync())
            {
                MessageText.Text =
                    "A staff account already exists. Setup is unavailable.";

                return;
            }

            string normalizedUsername =
                UsernameNormalizer.Normalize(username);

            var admin = new Staff
            {
                Username = username,
                NormalizedUsername = normalizedUsername,
                PasswordHash = PasswordHasher.HashPassword(password),
                Role = StaffRoles.Admin,
                IsActive = true
            };

            await repository.AddAsync(admin);

            MessageBox.Show(
                "Administrator account created successfully.",
                "Golddie",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
        }
        catch (Exception)
        {
            MessageText.Text =
                "Could not create the account. Please check the database and try again.";
        }
        finally
        {
            CreateAdminButton.IsEnabled = true;
        }
    }
}
