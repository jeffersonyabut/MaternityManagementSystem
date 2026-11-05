
using System;
using System.Windows;

namespace MaternityManagementSystem.Dialogs;

public partial class StaffPasswordResetDialog : Window
{
    public string NewPassword => NewPasswordBox.Password;

    public StaffPasswordResetDialog()
    {
        InitializeComponent();

        ResetButton.Click += ResetButton_Click;

        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
        };

        NewPasswordBox.PasswordChanged += (_, _) =>
            ClearValidation();

        ConfirmPasswordBox.PasswordChanged += (_, _) =>
            ClearValidation();

        Loaded += (_, _) => NewPasswordBox.Focus();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ClearValidation();

        string newPassword = NewPasswordBox.Password;
        string confirmPassword = ConfirmPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ShowValidation("Please enter a new password.");
            NewPasswordBox.Focus();
            return;
        }

        if (newPassword.Length < 8)
        {
            ShowValidation(
                "Password must contain at least 8 characters.");

            NewPasswordBox.Focus();
            return;
        }

        if (newPassword.Length > 1024)
        {
            ShowValidation("Password exceeds the maximum allowed length.");
            NewPasswordBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(confirmPassword))
        {
            ShowValidation("Please confirm the new password.");
            ConfirmPasswordBox.Focus();
            return;
        }

        if (!string.Equals(
                newPassword,
                confirmPassword,
                StringComparison.Ordinal))
        {
            ShowValidation("The passwords do not match.");
            ConfirmPasswordBox.Clear();
            ConfirmPasswordBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationText.Visibility = Visibility.Visible;
    }

    private void ClearValidation()
    {
        ValidationText.Text = string.Empty;
        ValidationText.Visibility = Visibility.Collapsed;
    }
}
