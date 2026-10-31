
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;
using MaternityManagementSystem.Dialogs;

namespace MaternityManagementSystem.Views;

public partial class VitalSignsView : UserControl
{
    private readonly PrenatalVisit _prenatalVisit;
    private readonly VitalSignsViewModel _viewModel;
    private readonly Pregnancy _pregnancy;
    private readonly RecordVersionService _recordVersionService;

    public VitalSignsView(
        PrenatalVisit prenatalVisit,
        Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(prenatalVisit);
        ArgumentNullException.ThrowIfNull(pregnancy);

        InitializeComponent();

        _prenatalVisit = prenatalVisit;
        _pregnancy = pregnancy;

        var app = (App)Application.Current;
        var authorizationService = new AuthorizationService(
            app.SessionService);

        var vitalSignService = new VitalSignService(
            new VitalSignRepository(),
            new PrenatalVisitRepository(),
            authorizationService,
            app.SessionService);

        _recordVersionService = new RecordVersionService(
            new RecordVersionRepository(),
            authorizationService);

        _viewModel = new VitalSignsViewModel(
            vitalSignService,
            prenatalVisit);

        DataContext = _viewModel;

        Loaded += VitalSignsView_Loaded;
    }

    private async void VitalSignsView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.LoadVitalSignsAsync();
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPrenatalVisits(_pregnancy);
        }
    }

    private void RecordVitalSignsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowVitalSignRegistration(
                _prenatalVisit,
                _pregnancy);
        }
    }

    private void EditVitalSignButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (button.DataContext is not VitalSign vitalSign)
        {
            return;
        }

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowVitalSignEdit(
                vitalSign,
                _prenatalVisit,
                _pregnancy);
        }
    }

    private async void VitalSignHistoryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not VitalSign vitalSign)
        {
            return;
        }

        try
        {
            var historyViewModel = new VitalSignHistoryViewModel(
                _recordVersionService,
                vitalSign.Id);

            await historyViewModel.LoadHistoryAsync();

            if (!string.IsNullOrEmpty(historyViewModel.StatusMessage))
            {
                MessageBox.Show(
                    historyViewModel.StatusMessage,
                    "History",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var dialog = new RecordHistoryDialog();

            dialog.Configure(
                title: "Vital Signs History",
                subtitle: "Review previous changes made to this vital signs record.",
                history: historyViewModel.History);

            var parentWindow = Window.GetWindow(this);

            if (parentWindow is not null)
            {
                dialog.Owner = parentWindow;
            }

            dialog.ShowDialog();
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Unable to load vital signs history. Please try again.",
                "History",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void ArchiveVitalSignButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (button.DataContext is not VitalSign vitalSign)
        {
            return;
        }

        DialogService dialogService = new();

        string? archiveReason = dialogService.ShowInput(
            "Archive Vital Signs",
            "Please provide a reason for archiving this vital signs record.",
            "Enter archive reason...",
            confirmText: "Continue");

        if (archiveReason == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(archiveReason))
        {
            dialogService.ShowWarning(
                "Archive Cancelled",
                "An archive reason is required. No changes were made.");

            return;
        }

        bool confirmed = dialogService.ShowConfirmation(
            "Confirm Archive",
            "Are you sure you want to archive this vital signs record?\n\n" +
            "It will no longer appear in the normal history, but it will remain in the database.",
            "Archive");

        if (!confirmed)
        {
            return;
        }

        try
        {
            var app = (App)Application.Current;
            var authorizationService = new AuthorizationService(
                app.SessionService);

            var vitalSignService = new VitalSignService(
                new VitalSignRepository(),
                new PrenatalVisitRepository(),
                authorizationService,
                app.SessionService);

            await vitalSignService.ArchiveVitalSignAsync(
                vitalSign.Id,
                archiveReason);

            await _viewModel.LoadVitalSignsAsync();

            dialogService.ShowSuccess(
                "Archive Successful",
                "The vital signs record has been archived successfully.");
        }
        catch (Exception)
        {
            dialogService.ShowError(
                "Archive Failed",
                "Unable to archive the vital signs record. Please try again.");
        }
    }
}
