
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Dialogs;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class PatientProfileView : UserControl
{
    private readonly PatientProfileViewModel _viewModel;
    private readonly RecordVersionService _recordVersionService;
    private LaboratoryResultsViewModel? _laboratoryResultsViewModel;

    public PatientProfileView(int patientId)
    {
        InitializeComponent();

        var app = (App)Application.Current;

        var authorizationService =
            new AuthorizationService(app.SessionService);

        var patientRepository = new PatientRepository();
        var pregnancyRepository = new PregnancyRepository();

        var patientService = new PatientService(
            patientRepository,
            authorizationService,
            app.SessionService);

        var pregnancyService = new PregnancyService(
            pregnancyRepository,
            patientRepository,
            authorizationService,
            app.SessionService);

        var recordVersionRepository = new RecordVersionRepository();

        _recordVersionService = new RecordVersionService(
            recordVersionRepository,
            authorizationService);

        _viewModel = new PatientProfileViewModel(
            patientService,
            pregnancyService,
            _recordVersionService,
            patientId);

        DataContext = _viewModel;

        Loaded += PatientProfileView_Loaded;
    }

    private async void PatientProfileView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.LoadPatientAsync();

        if (_viewModel.Patient is null)
        {
            return;
        }

        var app = (App)Application.Current;

        var authorizationService =
            new AuthorizationService(app.SessionService);

        var laboratoryResultService = new LaboratoryResultService(
            new LaboratoryResultRepository(),
            new PatientRepository(),
            new PrenatalVisitRepository(),
            new PregnancyRepository(),
            authorizationService,
            app.SessionService);

        _laboratoryResultsViewModel = new LaboratoryResultsViewModel(
            laboratoryResultService,
            _viewModel.Patient);

        LaboratoryResultsSection.DataContext =
            _laboratoryResultsViewModel;

        await _laboratoryResultsViewModel.LoadLaboratoryResultsAsync();
    }

    private void BackToPatientsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatients();
        }
    }

    private void EditPatientButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_viewModel.Patient is null)
        {
            return;
        }

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatientEdit(_viewModel.Patient);
        }
    }

    private void AddPregnancyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow &&
            _viewModel.Patient is not null)
        {
            dashboardWindow.ShowPregnancyRegistration(
                _viewModel.Patient.Id);
        }
    }

    private void EditPregnancyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not PregnancyCardDisplayViewModel pregnancyCard)
        {
            return;
        }

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPregnancyEdit(
                pregnancyCard.Pregnancy);
        }
    }

    private void ViewPrenatalVisitsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not PregnancyCardDisplayViewModel pregnancyCard)
        {
            return;
        }

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPrenatalVisits(
                pregnancyCard.Pregnancy);
        }
    }

    private async void PregnancyHistoryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not PregnancyCardDisplayViewModel pregnancyCard)
        {
            return;
        }

        var historyViewModel = new PregnancyHistoryViewModel(
            _recordVersionService,
            pregnancyCard.Pregnancy.Id);

        await historyViewModel.LoadHistoryAsync();

        var dialog = new RecordHistoryDialog();

        dialog.Configure(
            title: "Pregnancy Record History",
            subtitle: "Review previous changes made to this pregnancy record.",
            history: historyViewModel.History);

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is not null)
        {
            dialog.Owner = parentWindow;
        }

        dialog.ShowDialog();
    }

    private void AddLaboratoryResultButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_viewModel.Patient is null)
        {
            return;
        }

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowLaboratoryResultRegistration(
                _viewModel.Patient);
        }
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

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowLaboratoryResultEdit(
                laboratoryResult.Id,
                laboratoryResult.PatientId);
        }
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

        var historyViewModel = new LaboratoryResultHistoryViewModel(
            _recordVersionService,
            laboratoryResult.Id);

        await historyViewModel.LoadHistoryAsync();

        var dialog = new RecordHistoryDialog();

        dialog.Configure(
            title: "Laboratory Result History",
            subtitle: $"Review previous changes made to {laboratoryResult.TestName}.",
            history: historyViewModel.History);

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is not null)
        {
            dialog.Owner = parentWindow;
        }

        dialog.ShowDialog();
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

        if (_laboratoryResultsViewModel is null)
        {
            return;
        }

        var dialog = new CustomDialog
        {
            Owner = Window.GetWindow(this)
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
            confirmBackground: new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString(
                    "#FFDC2626")!)
        );

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string reason = dialog.InputValue.Trim();

        await _laboratoryResultsViewModel.ArchiveLaboratoryResultAsync(
            laboratoryResult,
            reason);
    }
}
