
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Dialogs;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class PatientProfileView : UserControl
{
    private readonly PatientProfileViewModel _viewModel;
    private readonly RecordVersionService _recordVersionService;

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
        System.Windows.RoutedEventArgs e)
    {
        await _viewModel.LoadPatientAsync();
    }

    private void BackToPatientsButton_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatients();
        }
    }

    private void EditPatientButton_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
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
        System.Windows.RoutedEventArgs e)
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
            dashboardWindow.ShowPregnancyEdit(pregnancyCard.Pregnancy);
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
            dashboardWindow.ShowPrenatalVisits(pregnancyCard.Pregnancy);
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

        var pregnancyId = pregnancyCard.Pregnancy.Id;

        var historyViewModel = new PregnancyHistoryViewModel(
            _recordVersionService,
            pregnancyId);

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
}
