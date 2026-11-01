
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class LaboratoryResultRegistrationView : UserControl
{
    private readonly int _patientId;
    private readonly LaboratoryResultRegistrationViewModel _viewModel;

    public LaboratoryResultRegistrationView(Patient patient)
    {
        ArgumentNullException.ThrowIfNull(patient);

        InitializeComponent();

        _patientId = patient.Id;

        var app = (App)Application.Current;

        var authorizationService = new AuthorizationService(
            app.SessionService);

        var patientRepository = new PatientRepository();
        var pregnancyRepository = new PregnancyRepository();
        var prenatalVisitRepository = new PrenatalVisitRepository();

        var laboratoryResultService = new LaboratoryResultService(
            new LaboratoryResultRepository(),
            patientRepository,
            prenatalVisitRepository,
            pregnancyRepository,
            authorizationService,
            app.SessionService);

        _viewModel = new LaboratoryResultRegistrationViewModel(
            laboratoryResultService,
            pregnancyRepository,
            prenatalVisitRepository,
            patient);

        DataContext = _viewModel;

        _viewModel.LaboratoryResultSaved += OnLaboratoryResultSaved;

        Loaded += LaboratoryResultRegistrationView_Loaded;
    }

    private async void LaboratoryResultRegistrationView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.LoadVisitOptionsAsync();
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatientProfile(_patientId);
        }
    }

    private void OnLaboratoryResultSaved()
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatientProfile(_patientId);
        }
    }
}
