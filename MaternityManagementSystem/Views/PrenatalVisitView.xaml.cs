
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Dialogs;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class PrenatalVisitView : UserControl
{
    private readonly Pregnancy _pregnancy;
    private readonly PrenatalVisitViewModel _viewModel;
    private readonly RecordVersionService _recordVersionService;

    public PrenatalVisitView(Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(pregnancy);

        InitializeComponent();

        _pregnancy = pregnancy;

        var app = (App)Application.Current;

        var authorizationService = new AuthorizationService(
            app.SessionService);

        var prenatalVisitService = new PrenatalVisitService(
            new PrenatalVisitRepository(),
            new PregnancyRepository(),
            authorizationService,
            app.SessionService);

        _recordVersionService = new RecordVersionService(
            new RecordVersionRepository(),
            authorizationService);

        _viewModel = new PrenatalVisitViewModel(
            prenatalVisitService,
            pregnancy);

        DataContext = _viewModel;

        Loaded += PrenatalVisitView_Loaded;
    }

    private async void PrenatalVisitView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.LoadVisitsAsync();
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatientProfile(_pregnancy.PatientId);
        }
    }

    private void AddVisitButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPrenatalVisitRegistration(_pregnancy);
        }
    }

    private void EditVisitButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not PrenatalVisit visit)
        {
            return;
        }

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPrenatalVisitEdit(
                visit.Id,
                _pregnancy);
        }
    }

    private void VitalSignsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not PrenatalVisit visit)
        {
            return;
        }

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowVitalSigns(visit, _pregnancy);
        }
    }

    private async void VisitHistoryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not PrenatalVisit visit)
        {
            return;
        }

        var historyViewModel = new PrenatalVisitHistoryViewModel(
            _recordVersionService,
            visit.Id);

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
            title: "Prenatal Visit History",
            subtitle: "Review previous changes made to this prenatal visit.",
            history: historyViewModel.History);

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is not null)
        {
            dialog.Owner = parentWindow;
        }

        dialog.ShowDialog();
    }
}
