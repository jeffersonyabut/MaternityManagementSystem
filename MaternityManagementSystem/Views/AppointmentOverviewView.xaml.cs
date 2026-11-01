
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaternityManagementSystem.Dialogs;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class AppointmentOverviewView : UserControl
{
    private readonly AppointmentOverviewViewModel _viewModel = null!;
    private readonly AppointmentService _appointmentService = null!;

    public AppointmentOverviewView()
    {
        InitializeComponent();

        // Prevent application services from initializing
        // inside the Visual Studio Designer.
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            return;
        }

        var app = (App)Application.Current;

        var appointmentRepository = new AppointmentRepository();
        var patientRepository = new PatientRepository();
        var pregnancyRepository = new PregnancyRepository();

        var authorizationService = new AuthorizationService(
            app.SessionService);

        _appointmentService = new AppointmentService(
            appointmentRepository,
            patientRepository,
            pregnancyRepository,
            authorizationService,
            app.SessionService);

        _viewModel = new AppointmentOverviewViewModel(
            _appointmentService);

        DataContext = _viewModel;

        Loaded += AppointmentOverviewView_Loaded;
    }

    // LOAD APPOINTMENTS

    private async void AppointmentOverviewView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.LoadAppointmentsAsync();
    }

    // DATE FILTER

    private void AppointmentDateFilter_SelectedDateChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (DataContext is not AppointmentOverviewViewModel viewModel)
        {
            return;
        }

        DateTime? selectedDate = AppointmentDateFilter.SelectedDate;

        viewModel.SelectedDate = selectedDate.HasValue
            ? DateOnly.FromDateTime(selectedDate.Value)
            : null;
    }

    // SCHEDULE APPOINTMENT

    private void ScheduleAppointment_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is DashboardWindow dashboard)
        {
            dashboard.ShowAppointmentRegistration();
        }
    }

    // VIEW APPOINTMENT DETAILS

    private void ViewAppointment_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_viewModel.SelectedAppointment is null)
        {
            ShowSelectionWarning();
            return;
        }

        var detailsDialog = new AppointmentDetailsDialog
        {
            Owner = Window.GetWindow(this),
            DataContext = _viewModel.SelectedAppointment
        };

        detailsDialog.ShowDialog();
    }

    // MANAGE APPOINTMENT

    private async void ManageAppointment_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_viewModel.SelectedAppointment is null)
        {
            ShowSelectionWarning();
            return;
        }

        var manageDialog = new AppointmentManageDialog
        {
            Owner = Window.GetWindow(this),
            DataContext = _viewModel.SelectedAppointment,
            AppointmentService = _appointmentService
        };

        bool? result = manageDialog.ShowDialog();

        if (result == true)
        {
            await _viewModel.LoadAppointmentsAsync();
        }
    }

    // SELECTION WARNING

    private void ShowSelectionWarning()
    {
        var warningDialog = new CustomDialog
        {
            Owner = Window.GetWindow(this)
        };

        warningDialog.Configure(
            title: "No Appointment Selected",
            message: "Please select an appointment record from the list before proceeding.",
            confirmText: "Got It",
            showCancel: false,
            showInput: false);

        warningDialog.SetAppearance(
            icon: "i",
            iconColor: "#2563EB",
            iconBackground: "#DBEAFE",
            confirmBackground: new SolidColorBrush(
                Color.FromRgb(37, 99, 235)));

        warningDialog.ShowDialog();
    }
}
