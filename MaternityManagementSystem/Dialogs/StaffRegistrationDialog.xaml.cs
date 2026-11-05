
using System.Windows;
using System.Windows.Controls;

namespace MaternityManagementSystem.Dialogs;

public partial class StaffRegistrationDialog : Window
{
    public string Username =>
        UsernameTextBox.Text.Trim();

    public string Password =>
        PasswordBox.Password;

    public string SelectedRole =>
        (RoleComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
        ?? string.Empty;

    public StaffRegistrationDialog()
    {
        InitializeComponent();

        CreateButton.Click += CreateButton_Click;

        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
        };

        UsernameTextBox.TextChanged += (_, _) =>
        {
            ClearValidation();
        };

        PasswordBox.PasswordChanged += (_, _) =>
        {
            ClearValidation();
        };

        ConfirmPasswordBox.PasswordChanged += (_, _) =>
        {
            ClearValidation();
        };

        RoleComboBox.SelectionChanged += (_, _) =>
        {
            ClearValidation();
        };

        Loaded += (_, _) =>
        {
            UsernameTextBox.Focus();
        };
    }

    private void CreateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClearValidation();

        string username = UsernameTextBox.Text.Trim();
        string password = PasswordBox.Password;
        string confirmPassword = ConfirmPasswordBox.Password;
        string role = SelectedRole;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowValidation("Please enter a username.");
            UsernameTextBox.Focus();
            return;
        }

        if (username.Length > 100)
        {
            ShowValidation("Username cannot exceed 100 characters.");
            UsernameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowValidation("Please enter a password.");
            PasswordBox.Focus();
            return;
        }

        if (password.Length < 8)
        {
            ShowValidation("Password must contain at least 8 characters.");
            PasswordBox.Focus();
            return;
        }

        if (password.Length > 1024)
        {
            ShowValidation("Password is too long.");
            PasswordBox.Focus();
            return;
        }

        if (!string.Equals(
                password,
                confirmPassword,
                StringComparison.Ordinal))
        {
            ShowValidation("Passwords do not match.");
            ConfirmPasswordBox.Focus();
            return;
        }

        if (role != "Admin" && role != "Staff")
        {
            ShowValidation("Please select a valid account role.");
            RoleComboBox.Focus();
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
