
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaternityManagementSystem.Dialogs;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly DashboardViewModel _viewModel;
        private readonly AppointmentService _appointmentService;

        public DashboardView()
        {
            InitializeComponent();

            _viewModel = new DashboardViewModel();
            DataContext = _viewModel;

            var app = (App)Application.Current;

            _appointmentService = new AppointmentService(
                new AppointmentRepository(),
                new PatientRepository(),
                new PregnancyRepository(),
                new AuthorizationService(app.SessionService),
                app.SessionService);

            Loaded += DashboardView_Loaded;
        }

        private async void DashboardView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await _viewModel.LoadDashboardAsync();
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
