
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;
using MaternityManagementSystem.Views;

namespace MaternityManagementSystem.Dialogs;

public partial class LaboratoryResultsDialog : Window
{
    public Action? CancelEditAction { get; set; }

    private readonly RecordVersionService _recordVersionService;

    public LaboratoryResultsDialog()
    {
        InitializeComponent();

        var app = (App)Application.Current;

        var authorizationService =
            new AuthorizationService(app.SessionService);

        _recordVersionService = new RecordVersionService(
            new RecordVersionRepository(),
            authorizationService);
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    private async void LaboratoryResultHistoryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not LaboratoryResult laboratoryResult)
        {
            return;
        }

        var app = (App)Application.Current;

        var historyViewModel = new LaboratoryResultHistoryViewModel(
            _recordVersionService,
            app.RegionalSettings,
            laboratoryResult.Id);

        await historyViewModel.LoadHistoryAsync();

        var dialog = new RecordHistoryDialog
        {
            Owner = this
        };

        dialog.Configure(
            title: "Laboratory Result History",
            subtitle: $"Review previous changes made to {laboratoryResult.TestName}.",
            history: historyViewModel.History);

        dialog.ShowDialog();
    }

    private void EditLaboratoryResultButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not LaboratoryResult laboratoryResult)
        {
            return;
        }

        if (DataContext is not LaboratoryResultsViewModel viewModel)
        {
            return;
        }

        var dashboardWindow = Owner as DashboardWindow;

        if (dashboardWindow is null)
        {
            return;
        }

        int patientId = viewModel.Patient.Id;

        Close();

        dashboardWindow.ShowLaboratoryResultEdit(
            laboratoryResult.Id,
            patientId,
            cancelAction: CancelEditAction,
            saveAction: CancelEditAction);
    }

    private async void LaboratoryResultArchiveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not LaboratoryResult laboratoryResult)
        {
            return;
        }

        if (DataContext is not LaboratoryResultsViewModel viewModel)
        {
            return;
        }

        var dialog = new CustomDialog
        {
            Owner = this
        };

        dialog.Configure(
            title: "Archive Laboratory Result",
            message: "Please provide a reason for archiving this laboratory result. The record will be retained and removed from the active results list.",
            confirmText: "Archive",
            cancelText: "Cancel",
            showCancel: true,
            showInput: true,
            inputPlaceholder: "Enter archive reason...",
            requireInput: true
        );

        dialog.SetAppearance(
            icon: "!",
            iconColor: "#FFDC2626",
            iconBackground: "#FFFEE2E2",
            confirmBackground: new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(
                    "#FFDC2626")!)
        );

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string reason = dialog.InputValue.Trim();

        await viewModel.ArchiveLaboratoryResultAsync(
            laboratoryResult,
            reason);
    }
}
