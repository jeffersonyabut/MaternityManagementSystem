
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Services.Reports;
using MaternityManagementSystem.ViewModels.Reports;
using MaternityManagementSystem.Views.Reports;

namespace MaternityManagementSystem.Views
{
    public partial class ReportsView : UserControl
    {
        private readonly PatientMasterListViewModel _viewModel;
        private readonly PrenatalCareReportViewModel _prenatalCareViewModel;
        private readonly LaboratoryResultsReportViewModel _laboratoryResultsViewModel;
        private readonly AppointmentReportViewModel _appointmentReportViewModel;

        public ReportsView()
        {
            InitializeComponent();

            var app = (App)Application.Current;

            var authorizationService = new AuthorizationService(
                app.SessionService);

            var patientService = new PatientService(
                new PatientRepository(),
                authorizationService,
                app.SessionService);

            // Patient Master List
            var patientReportService =
                new PatientReportService(patientService);

            _viewModel = new PatientMasterListViewModel(
                patientReportService);

            // Prenatal Care Report
            var prenatalCareReportService =
                new PrenatalCareReportService(patientService);

            _prenatalCareViewModel =
                new PrenatalCareReportViewModel(
                    prenatalCareReportService);

            // Laboratory Results Report
            var laboratoryResultService =
                new LaboratoryResultService(
                    new LaboratoryResultRepository(),
                    new PatientRepository(),
                    new PrenatalVisitRepository(),
                    new PregnancyRepository(),
                    authorizationService,
                    app.SessionService);

            var laboratoryResultsReportService =
                new LaboratoryResultsReportService(
                    laboratoryResultService);

            _laboratoryResultsViewModel =
                new LaboratoryResultsReportViewModel(
                    laboratoryResultsReportService);

            // Appointment Report
            var appointmentService =
                new AppointmentService(
                    new AppointmentRepository(),
                    new PatientRepository(),
                    new PregnancyRepository(),
                    authorizationService,
                    app.SessionService);

            var appointmentReportService =
                new AppointmentReportService(
                    appointmentService);

            _appointmentReportViewModel =
                new AppointmentReportViewModel(
                    appointmentReportService);

            DataContext = _viewModel;
        }

        private void PatientMasterListButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is DashboardWindow dashboard)
            {
                var reportView =
                    new PatientMasterListReportView(_viewModel);

                dashboard.NavigateToReportView(reportView);
            }
        }

        private void IndividualPatientRecordButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is DashboardWindow dashboard)
            {
                dashboard.ShowIndividualPatientRecordSelection();
            }
        }

        private void PrenatalCareReportButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is DashboardWindow dashboard)
            {
                var reportView =
                    new PrenatalCareReportView(_prenatalCareViewModel);

                reportView.BackRequested += dashboard.ShowReports;

                dashboard.NavigateToReportView(reportView);
            }
        }

        private void LaboratoryResultsReportButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is DashboardWindow dashboard)
            {
                var reportView =
                    new LaboratoryResultsReportView(
                        _laboratoryResultsViewModel);

                reportView.BackRequested += dashboard.ShowReports;

                dashboard.NavigateToReportView(reportView);
            }
        }

        private void AppointmentReportButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is DashboardWindow dashboard)
            {
                var reportView =
                    new AppointmentReportView(
                        _appointmentReportViewModel);

                reportView.BackRequested += dashboard.ShowReports;

                dashboard.NavigateToReportView(reportView);
            }
        }
    }
}
