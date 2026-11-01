
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class LaboratoryResultEditView : UserControl
{
    private readonly int _laboratoryResultId;
    private readonly int _patientId;

    private readonly Action? _cancelAction;
    private readonly Action? _saveAction;

    private readonly LaboratoryResultEditViewModel _viewModel;

    public LaboratoryResultEditView(
        int laboratoryResultId,
        int patientId,
        Action? cancelAction = null,
        Action? saveAction = null)
    {
        if (laboratoryResultId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(laboratoryResultId));
        }

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        InitializeComponent();

        _laboratoryResultId = laboratoryResultId;
        _patientId = patientId;
        _cancelAction = cancelAction;
        _saveAction = saveAction;

        var app = (App)Application.Current;

        var authorizationService = new AuthorizationService(
            app.SessionService);

        var laboratoryResultService = new LaboratoryResultService(
            new LaboratoryResultRepository(),
            new PatientRepository(),
            new PrenatalVisitRepository(),
            new PregnancyRepository(),
            authorizationService,
            app.SessionService);

        _viewModel = new LaboratoryResultEditViewModel(
            laboratoryResultService);

        DataContext = _viewModel;

        _viewModel.LaboratoryResultUpdated += OnLaboratoryResultUpdated;

        Loaded += LaboratoryResultEditView_Loaded;
    }

    private async void LaboratoryResultEditView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.LoadLaboratoryResultAsync(
            _laboratoryResultId);
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_cancelAction is not null)
        {
            _cancelAction.Invoke();
            return;
        }

        ReturnToPatientProfile();
    }

    private void OnLaboratoryResultUpdated()
    {
        if (_saveAction is not null)
        {
            _saveAction.Invoke();
            return;
        }

        ReturnToPatientProfile();
    }

    private void ReturnToPatientProfile()
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatientProfile(_patientId);
        }
    }
}
