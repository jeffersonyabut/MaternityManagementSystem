
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Dialogs;

public partial class StaffEditDialog : Window
{
    private readonly StaffDisplayModel _originalStaff;

    public string Username =>
        UsernameTextBox.Text.Trim();

    public string SelectedRole =>
        (RoleComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
        ?? string.Empty;

    public StaffEditDialog(StaffDisplayModel staff)
    {
        ArgumentNullException.ThrowIfNull(staff);

        InitializeComponent();

        _originalStaff = staff;

        LoadStaffDetails();

        SaveButton.Click += SaveButton_Click;

        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
        };

        UsernameTextBox.TextChanged += (_, _) =>
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
            UsernameTextBox.CaretIndex =
                UsernameTextBox.Text.Length;
        };
    }

    private void LoadStaffDetails()
    {
        AccountIdText.Text = _originalStaff.Id.ToString();

        UsernameTextBox.Text = _originalStaff.Username;

        foreach (ComboBoxItem item in RoleComboBox.Items)
        {
            if (string.Equals(
                    item.Tag?.ToString(),
                    _originalStaff.Role,
                    StringComparison.OrdinalIgnoreCase))
            {
                RoleComboBox.SelectedItem = item;
                break;
            }
        }

        AccountStatusText.Text = _originalStaff.IsActive
            ? "Active"
            : "Inactive";

        AccountStatusText.Foreground = _originalStaff.IsActive
            ? new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(36, 150, 107))
            : new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(214, 90, 104));
    }

    private void SaveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClearValidation();

        string username = Username;
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

        if (!StaffRoles.IsValid(role))
        {
            ShowValidation("Please select a valid account role.");
            RoleComboBox.Focus();
            return;
        }

        bool usernameChanged =
            !string.Equals(
                username,
                _originalStaff.Username,
                StringComparison.Ordinal);

        bool roleChanged =
            !string.Equals(
                role,
                _originalStaff.Role,
                StringComparison.OrdinalIgnoreCase);

        if (!usernameChanged && !roleChanged)
        {
            ShowValidation("No changes were made.");
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
