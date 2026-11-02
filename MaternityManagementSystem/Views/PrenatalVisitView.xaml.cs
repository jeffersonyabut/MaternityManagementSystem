
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

    private void GenerateVisitReportButton_Click(
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
            dashboardWindow.ShowPrenatalVisitReport(
                visit.Id,
                _pregnancy);
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

    private async void LaboratoryResultsButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not PrenatalVisit visit)
        {
            return;
        }

        try
        {
            var app = (App)Application.Current;

            var authorizationService = new AuthorizationService(
                app.SessionService);

            var patientRepository = new PatientRepository();

            var patient = await patientRepository.GetByIdAsync(
                _pregnancy.PatientId);

            if (patient is null)
            {
                MessageBox.Show(
                    "The patient associated with this visit could not be found.",
                    "Laboratory Results",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var laboratoryResultService = new LaboratoryResultService(
                new LaboratoryResultRepository(),
                patientRepository,
                new PrenatalVisitRepository(),
                new PregnancyRepository(),
                authorizationService,
                app.SessionService);

            var viewModel = new LaboratoryResultsViewModel(
                laboratoryResultService,
                patient,
                visit);

            await viewModel.LoadLaboratoryResultsAsync();

            var dashboardWindow = Window.GetWindow(this) as DashboardWindow;

            if (dashboardWindow is null)
            {
                return;
            }

            var dialog = new LaboratoryResultsDialog
            {
                Owner = dashboardWindow,
                DataContext = viewModel,
                CancelEditAction = () =>
                {
                    dashboardWindow.ShowPrenatalVisits(_pregnancy);
                }
            };

            dialog.ShowDialog();
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Unable to load laboratory results for this prenatal visit.",
                "Laboratory Results",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }




}
