
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class AppointmentRegistrationView : UserControl
{
    private readonly AppointmentViewModel _viewModel = null!;

    private bool _isSelectingPatient;
    public event Action? AppointmentSaved;
    public event Action? CancelRequested;

    public AppointmentRegistrationView()
    {
        InitializeComponent();

        // Prevent the Visual Studio Designer from trying to
        // cast its design-time Application to our App class.
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            return;
        }

        AppointmentDatePicker.SelectedDate = DateTime.Today;

        var app = (App)Application.Current;

        var appointmentRepository = new AppointmentRepository();
        var patientRepository = new PatientRepository();
        var pregnancyRepository = new PregnancyRepository();

        var authorizationService = new AuthorizationService(
            app.SessionService);

        var appointmentService = new AppointmentService(
            appointmentRepository,
            patientRepository,
            pregnancyRepository,
            authorizationService,
            app.SessionService,
            app.RegionalSettings);

        _viewModel = new AppointmentViewModel(
            appointmentService,
            patientRepository,
            pregnancyRepository);

        DataContext = _viewModel;

        Loaded += AppointmentRegistrationView_Loaded;
    }

    private async void AppointmentRegistrationView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.LoadDefaultAppointmentDurationAsync();
        await _viewModel.LoadPatientsAsync();
    }

    // PATIENT DATA GRID SELECTION

    private async void PatientDataGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isSelectingPatient)
        {
            return;
        }

        if (PatientDataGrid.SelectedItem is Patient patient)
        {
            _isSelectingPatient = true;

            try
            {
                await _viewModel.SelectPatientAsync(patient);
            }
            finally
            {
                _isSelectingPatient = false;
            }
        }
        else
        {
            await _viewModel.SelectPatientAsync(null);
        }
    }

    // SCHEDULE APPOINTMENT

    private async void ScheduleAppointment_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_viewModel.IsBusy)
        {
            return;
        }

        if (_viewModel.SelectedPatient is null)
        {
            _viewModel.StatusMessage =
                "Please select a patient.";

            return;
        }

        if (AppointmentDatePicker.SelectedDate is null)
        {
            _viewModel.StatusMessage =
                "Please select an appointment date.";

            return;
        }

        if (!TimeOnly.TryParse(
                StartTimeBox.Text,
                CultureInfo.CurrentCulture,
                DateTimeStyles.None,
                out TimeOnly startTime))
        {
            _viewModel.StatusMessage =
                "Please enter a valid start time, such as 08:30 AM.";

            return;
        }

        TimeOnly? endTime = null;

        if (!string.IsNullOrWhiteSpace(EndTimeBox.Text))
        {
            if (!TimeOnly.TryParse(
                    EndTimeBox.Text,
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.None,
                    out TimeOnly parsedEndTime))
            {
                _viewModel.StatusMessage =
                    "Please enter a valid end time, such as 09:00 AM.";

                return;
            }

            endTime = parsedEndTime;
        }

        if (string.IsNullOrWhiteSpace(PurposeBox.Text))
        {
            _viewModel.StatusMessage =
                "Please enter the appointment purpose.";

            return;
        }

        var appointment = new Appointment
        {
            PatientId = _viewModel.SelectedPatient.Id,

            PregnancyId = _viewModel.SelectedPregnancy?.Id,

            AppointmentDate = DateOnly.FromDateTime(
                AppointmentDatePicker.SelectedDate.Value),

            StartTime = startTime,

            EndTime = endTime,

            Purpose = PurposeBox.Text.Trim(),

            Notes = string.IsNullOrWhiteSpace(NotesBox.Text)
                ? null
                : NotesBox.Text.Trim(),

            Status = "Scheduled"
        };

        bool success =
            await _viewModel.CreateAppointmentAsync(appointment);

        if (success)
        {
            AppointmentSaved?.Invoke();
        }
    }

    // CLEAR FORM

    private void ClearForm_Click(
        object sender,
        RoutedEventArgs e)
    {
        ClearForm();
    }

    private void ClearForm()
    {
        PatientDataGrid.SelectedItem = null;

        _viewModel.SelectedPatient = null;
        _viewModel.SelectedPregnancy = null;

        _viewModel.Pregnancies.Clear();

        PatientSearchBox.Clear();

        AppointmentDatePicker.SelectedDate = DateTime.Today;

        StartTimeBox.Clear();
        EndTimeBox.Clear();
        PurposeBox.Clear();
        NotesBox.Clear();

        _viewModel.StatusMessage =
            "Appointment form cleared.";
    }

    // CANCEL REGISTRATION

    private void CancelRegistration_Click(
        object sender,
        RoutedEventArgs e)
    {
        CancelRequested?.Invoke();
    }


}
