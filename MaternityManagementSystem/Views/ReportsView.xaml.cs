
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Services.Reports;
using MaternityManagementSystem.ViewModels.Reports;

namespace MaternityManagementSystem.Views
{
    public partial class ReportsView : UserControl
    {
        private readonly PatientMasterListViewModel _viewModel;

        public ReportsView()
        {
            InitializeComponent();

            var app = (App)Application.Current;

            var patientService = new PatientService(
                new PatientRepository(),
                new AuthorizationService(app.SessionService),
                app.SessionService);

            var patientReportService =
                new PatientReportService(patientService);

            _viewModel = new PatientMasterListViewModel(
                patientReportService);

            DataContext = _viewModel;
        }

        private void PatientMasterListButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is DashboardWindow dashboard)
            {
                var reportView = new PatientMasterListReportView(_viewModel);

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





    }
}
