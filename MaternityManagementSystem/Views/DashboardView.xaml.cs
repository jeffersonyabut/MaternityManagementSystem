
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaternityManagementSystem.Dialogs;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Views.Reports;

namespace MaternityManagementSystem.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly DashboardViewModel _viewModel;
        private readonly AppointmentService _appointmentService;

        public DashboardView()
        {
            InitializeComponent();

            var app = (App)Application.Current;

            var authorizationService = new AuthorizationService(
                app.SessionService);

            var contextFactory = new ClinicDbContextFactory();
            var clock = new SystemClock();

            var analyticsService = new DashboardAnalyticsService(
                authorizationService,
                app.RegionalSettings,
                contextFactory,
                clock);

            _viewModel = new DashboardViewModel(analyticsService);
            DataContext = _viewModel;

            var systemSettingRepository = new SystemSettingRepository();

            var systemSettingService = new SystemSettingService(
                systemSettingRepository,
                authorizationService,
                app.SessionService);

            _appointmentService = new AppointmentService(
                new AppointmentRepository(),
                new PatientRepository(),
                new PregnancyRepository(),
                authorizationService,
                app.SessionService,
                app.RegionalSettings,
                systemSettingService);

            Loaded += DashboardView_Loaded;
        }

        private async void DashboardView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                await _viewModel.LoadDashboardAsync();
            }
            catch (UnauthorizedAccessException)
            {
                // Session expiration is handled by DashboardWindow.
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Unable to load dashboard information. Please try again.",
                    "Dashboard Unavailable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void NavigateToPatients(
            object sender,
            MouseButtonEventArgs e)
        {
            if (Window.GetWindow(this) is DashboardWindow dashboard)
            {
                dashboard.ShowPatients();
            }
        }

        private void NavigateToAppointments(
            object sender,
            MouseButtonEventArgs e)
        {
            if (Window.GetWindow(this) is DashboardWindow dashboard)
            {
                dashboard.ShowAppointments();
            }
        }

        private void NavigateToPrenatalCare(
            object sender,
            MouseButtonEventArgs e)
        {
            if (Window.GetWindow(this) is DashboardWindow dashboard)
            {
                dashboard.ShowPatients();
            }
        }

        private async void UpcomingAppointments_MouseDoubleClick(
            object sender,
            MouseButtonEventArgs e)
        {
            if (sender is not DataGrid dataGrid ||
                dataGrid.SelectedItem is not UpcomingAppointment selectedAppointment)
            {
                return;
            }

            try
            {
                var appointment = await _appointmentService.GetByIdAsync(
                    selectedAppointment.AppointmentId);

                if (appointment is null || appointment.IsArchived)
                {
                    MessageBox.Show(
                        "This appointment could not be found or is no longer available.",
                        "Appointment Unavailable",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    await _viewModel.LoadDashboardAsync();
                    return;
                }

                var detailsDialog = new AppointmentDetailsDialog
                {
                    Owner = Window.GetWindow(this),
                    DataContext = appointment
                };

                detailsDialog.ShowDialog();
            }
            catch (UnauthorizedAccessException)
            {
                // Session expiration is handled by DashboardWindow.
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Unable to load the appointment details. Please try again.",
                    "Unable to Open Appointment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
