
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Services.Reports;
using MaternityManagementSystem.ViewModels.Reports;

namespace MaternityManagementSystem.Views.Reports;

public partial class IndividualPatientRecordSelectionView : UserControl
{
    private readonly IndividualPatientRecordSelectionViewModel _viewModel;

    public event Action<IndividualPatientRecordReport>? ReportGenerated;

    public IndividualPatientRecordSelectionView()
    {
        InitializeComponent();

        var app = (App)Application.Current;

        var patientService = new PatientService(
            new PatientRepository(),
            new AuthorizationService(app.SessionService),
            app.SessionService);

        var reportService =
            new IndividualPatientRecordReportService(patientService);

        _viewModel = new IndividualPatientRecordSelectionViewModel(
            patientService,
            reportService);

        _viewModel.ReportGenerated += report =>
        {
            ReportGenerated?.Invoke(report);
        };

        DataContext = _viewModel;

        Loaded += View_Loaded;
    }

    private async void View_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.LoadPatientsCommand.ExecuteAsync(null);
    }
}
