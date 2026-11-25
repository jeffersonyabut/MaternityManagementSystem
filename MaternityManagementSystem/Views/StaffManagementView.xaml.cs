
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Dialogs;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class StaffManagementView : UserControl
{
    public StaffManagementView()
    {
        InitializeComponent();
    }

    private void ClearFilters_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is StaffManagementViewModel viewModel)
        {
            viewModel.ClearFilters();
        }
    }

    private async void EditStaffButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not StaffManagementViewModel viewModel)
        {
            return;
        }

        var selectedStaff = viewModel.SelectedStaff;
        Window? owner = Window.GetWindow(this);

        if (selectedStaff is null)
        {
            CustomDialog warningDialog = new();

            warningDialog.Configure(
                "No Account Selected",
                "Please select a staff account from the table before editing.",
                confirmText: "OK",
                showCancel: false);

            if (owner is not null)
            {
                warningDialog.Owner = owner;
            }

            warningDialog.ShowDialog();
            return;
        }

        StaffEditDialog editDialog = new(selectedStaff);

        if (owner is not null)
        {
            editDialog.Owner = owner;
        }

        bool? result = editDialog.ShowDialog();

        if (result != true)
        {
            return;
        }

        try
        {
            await viewModel.UpdateStaffAsync(
                selectedStaff.Id,
                editDialog.Username,
                editDialog.SelectedRole);
        }
        catch (UnauthorizedAccessException)
        {
            // Session expiration is handled by DashboardWindow.
        }
        catch (Exception ex)
        {
            CustomDialog errorDialog = new();

            errorDialog.Configure(
                "Unable to Update Account",
                ex.Message,
                confirmText: "OK",
                showCancel: false);

            if (owner is not null)
            {
                errorDialog.Owner = owner;
            }

            errorDialog.ShowDialog();
        }
    }

    private async void AddStaffButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not StaffManagementViewModel viewModel)
        {
            return;
        }

        StaffRegistrationDialog registrationDialog = new();

        Window? owner = Window.GetWindow(this);

        if (owner is not null)
        {
            registrationDialog.Owner = owner;
        }

        bool? result = registrationDialog.ShowDialog();

        if (result != true)
        {
            return;
        }

        try
        {
            await viewModel.CreateStaffAsync(
                registrationDialog.Username,
                registrationDialog.Password,
                registrationDialog.SelectedRole);
        }
        catch (UnauthorizedAccessException)
        {
            // Session expiration is handled by DashboardWindow.
        }
        catch (Exception ex)
        {
            CustomDialog errorDialog = new();

            errorDialog.Configure(
                "Unable to Create Account",
                ex.Message,
                confirmText: "OK",
                showCancel: false);

            if (owner is not null)
            {
                errorDialog.Owner = owner;
            }

            errorDialog.ShowDialog();
        }
    }

    private async void ChangeStaffStatusButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not StaffManagementViewModel viewModel)
        {
            return;
        }

        var selectedStaff = viewModel.SelectedStaff;
        Window? owner = Window.GetWindow(this);

        if (selectedStaff is null)
        {
            CustomDialog warningDialog = new();

            warningDialog.Configure(
                "No Account Selected",
                "Please select a staff account before changing its status.",
                confirmText: "OK",
                showCancel: false);

            if (owner is not null)
            {
                warningDialog.Owner = owner;
            }

            warningDialog.ShowDialog();
            return;
        }

        string action = selectedStaff.IsActive
            ? "deactivate"
            : "activate";

        CustomDialog confirmationDialog = new();

        confirmationDialog.Configure(
            $"Confirm Account {char.ToUpper(action[0]) + action[1..]}",
            $"Are you sure you want to {action} the account '{selectedStaff.Username}'?",
            confirmText: "Continue",
            cancelText: "Cancel");

        if (owner is not null)
        {
            confirmationDialog.Owner = owner;
        }

        if (confirmationDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await viewModel.SetStaffStatusAsync(
                selectedStaff.Id,
                !selectedStaff.IsActive);
        }
        catch (UnauthorizedAccessException)
        {
            // Session expiration is handled by DashboardWindow.
        }
        catch (Exception ex)
        {
            CustomDialog errorDialog = new();

            errorDialog.Configure(
                "Unable to Change Account Status",
                ex.Message,
                confirmText: "OK",
                showCancel: false);

            if (owner is not null)
            {
                errorDialog.Owner = owner;
            }

            errorDialog.ShowDialog();
        }
    }

    private async void ResetStaffPasswordButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not StaffManagementViewModel viewModel)
        {
            return;
        }

        var selectedStaff = viewModel.SelectedStaff;
        Window? owner = Window.GetWindow(this);

        if (selectedStaff is null)
        {
            CustomDialog warningDialog = new();

            warningDialog.Configure(
                "No Account Selected",
                "Please select a staff account before resetting its password.",
                confirmText: "OK",
                showCancel: false);

            if (owner is not null)
            {
                warningDialog.Owner = owner;
            }

            warningDialog.ShowDialog();
            return;
        }

        StaffPasswordResetDialog resetDialog = new();

        if (owner is not null)
        {
            resetDialog.Owner = owner;
        }

        bool? result = resetDialog.ShowDialog();

        if (result != true)
        {
            return;
        }

        try
        {
            await viewModel.ResetStaffPasswordAsync(
                selectedStaff.Id,
                resetDialog.NewPassword);

            CustomDialog successDialog = new();

            successDialog.Configure(
                "Password Reset Successful",
                $"The password for '{selectedStaff.Username}' has been updated successfully.",
                confirmText: "OK",
                showCancel: false);

            if (owner is not null)
            {
                successDialog.Owner = owner;
            }

            successDialog.ShowDialog();
        }
        catch (UnauthorizedAccessException)
        {
            // Session expiration is handled by DashboardWindow.
        }
        catch (Exception ex)
        {
            CustomDialog errorDialog = new();

            errorDialog.Configure(
                "Unable to Reset Password",
                ex.Message,
                confirmText: "OK",
                showCancel: false);

            if (owner is not null)
            {
                errorDialog.Owner = owner;
            }

            errorDialog.ShowDialog();
        }
    }
}
