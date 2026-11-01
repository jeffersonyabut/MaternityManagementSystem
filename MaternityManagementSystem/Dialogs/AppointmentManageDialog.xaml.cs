
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.Dialogs;

public partial class AppointmentManageDialog : Window
{
    private Appointment? _appointment;

    public AppointmentService? AppointmentService { get; set; }

    private static readonly string[] EditableStatuses =
    [
        "Scheduled",
        "Confirmed",
        "Completed",
        "No-show"
    ];

    public AppointmentManageDialog()
    {
        InitializeComponent();

        StatusComboBox.ItemsSource = EditableStatuses;

        Loaded += AppointmentManageDialog_Loaded;
    }

    private void AppointmentManageDialog_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not Appointment appointment)
        {
            MessageBox.Show(
                "The appointment record could not be loaded.",
                "Manage Appointment",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Close();
            return;
        }

        _appointment = appointment;

        PopulateFields(appointment);
    }

    // POPULATE FIELDS

    private void PopulateFields(Appointment appointment)
    {
        var patient = appointment.Patient;

        PatientNameText.Text = patient is null
            ? "Patient information unavailable"
            : string.Join(
                " ",
                new[]
                {
                    patient.FirstName,
                    patient.MiddleName,
                    patient.LastName
                }.Where(name => !string.IsNullOrWhiteSpace(name)));

        PatientNumberText.Text = patient is null
            ? "Patient number unavailable"
            : $"Patient No. {patient.PatientNumber}";

        AppointmentDatePicker.SelectedDate =
            appointment.AppointmentDate.ToDateTime(
                TimeOnly.MinValue);

        StartTimeTextBox.Text =
            appointment.StartTime.ToString(
                "hh:mm tt",
                CultureInfo.InvariantCulture);

        EndTimeTextBox.Text =
            appointment.EndTime?.ToString(
                "hh:mm tt",
                CultureInfo.InvariantCulture) ?? string.Empty;

        PurposeTextBox.Text = appointment.Purpose;
        NotesTextBox.Text = appointment.Notes ?? string.Empty;

        StatusComboBox.SelectedItem = appointment.Status;
    }

    // SAVE CHANGES

    private async void SaveChanges_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_appointment is null)
        {
            ShowError("No appointment is currently loaded.");
            return;
        }

        if (AppointmentService is null)
        {
            ShowError("Appointment service is unavailable.");
            return;
        }

        if (AppointmentDatePicker.SelectedDate is not DateTime selectedDate)
        {
            ShowError("Please select an appointment date.");
            return;
        }

        if (!TimeOnly.TryParse(
                StartTimeTextBox.Text.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out TimeOnly startTime))
        {
            ShowError("Please enter a valid start time, such as 09:00 AM.");
            return;
        }

        TimeOnly? endTime = null;

        if (!string.IsNullOrWhiteSpace(EndTimeTextBox.Text))
        {
            if (!TimeOnly.TryParse(
                    EndTimeTextBox.Text.Trim(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out TimeOnly parsedEndTime))
            {
                ShowError("Please enter a valid end time, such as 09:30 AM.");
                return;
            }

            endTime = parsedEndTime;
        }

        if (string.IsNullOrWhiteSpace(PurposeTextBox.Text))
        {
            ShowError("Please enter the appointment purpose.");
            return;
        }

        if (StatusComboBox.SelectedItem is not string selectedStatus)
        {
            ShowError("Please select an appointment status.");
            return;
        }

        string updatedPurpose = PurposeTextBox.Text.Trim();

        string? updatedNotes = string.IsNullOrWhiteSpace(NotesTextBox.Text)
            ? null
            : NotesTextBox.Text.Trim();

        DateOnly updatedDate = DateOnly.FromDateTime(selectedDate);

        bool hasChanges =
            _appointment.AppointmentDate != updatedDate ||
            _appointment.StartTime != startTime ||
            _appointment.EndTime != endTime ||
            _appointment.Purpose != updatedPurpose ||
            (_appointment.Notes ?? string.Empty) !=
                (updatedNotes ?? string.Empty) ||
            _appointment.Status != selectedStatus;

        if (!hasChanges)
        {
            MessageBox.Show(
                "No changes were detected. The appointment remains unchanged.",
                "No Changes to Save",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var updatedAppointment = new Appointment
        {
            Id = _appointment.Id,

            PatientId = _appointment.PatientId,
            PregnancyId = _appointment.PregnancyId,

            AppointmentDate = updatedDate,
            StartTime = startTime,
            EndTime = endTime,

            Purpose = updatedPurpose,
            Notes = updatedNotes,

            Status = selectedStatus,

            CancellationReason = _appointment.CancellationReason
        };

        try
        {
            await AppointmentService.UpdateAppointmentAsync(
                updatedAppointment);

            ShowSuccess(
                "Appointment Updated",
                "The appointment details have been updated successfully.");

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    // CANCEL APPOINTMENT

    private async void CancelAppointment_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_appointment is null)
        {
            ShowError("No appointment is currently loaded.");
            return;
        }

        if (AppointmentService is null)
        {
            ShowError("Appointment service is unavailable.");
            return;
        }

        var reasonDialog = new CustomDialog
        {
            Owner = this
        };

        reasonDialog.Configure(
            title: "Cancel Appointment",
            message: "Please provide a reason for cancelling this appointment. This information will be saved in the appointment record.",
            confirmText: "Confirm Cancellation",
            cancelText: "Keep Appointment",
            showCancel: true,
            showInput: true,
            inputPlaceholder: "Enter cancellation reason...",
            requireInput: true);

        reasonDialog.SetAppearance(
            icon: "!",
            iconColor: "#DC2626",
            iconBackground: "#FEE2E2",
            confirmBackground: new SolidColorBrush(
                Color.FromRgb(220, 38, 38)));

        if (reasonDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await AppointmentService.CancelAppointmentAsync(
                _appointment.Id,
                reasonDialog.InputValue.Trim());

            ShowSuccess(
                "Appointment Cancelled",
                "The appointment has been cancelled successfully.");

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    // ARCHIVE APPOINTMENT

    private async void ArchiveAppointment_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_appointment is null)
        {
            ShowError("No appointment is currently loaded.");
            return;
        }

        if (AppointmentService is null)
        {
            ShowError("Appointment service is unavailable.");
            return;
        }

        var reasonDialog = new CustomDialog
        {
            Owner = this
        };

        reasonDialog.Configure(
            title: "Archive Appointment",
            message: "Please provide a reason for archiving this appointment. The record will be removed from the active appointment overview but retained in the system history.",
            confirmText: "Archive Appointment",
            cancelText: "Keep Appointment",
            showCancel: true,
            showInput: true,
            inputPlaceholder: "Enter archive reason...",
            requireInput: true);

        reasonDialog.SetAppearance(
            icon: "!",
            iconColor: "#D97706",
            iconBackground: "#FEF3C7",
            confirmBackground: new SolidColorBrush(
                Color.FromRgb(217, 119, 6)));

        if (reasonDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await AppointmentService.ArchiveAppointmentAsync(
                _appointment.Id,
                reasonDialog.InputValue.Trim());

            ShowSuccess(
                "Appointment Archived",
                "The appointment has been archived successfully.");

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    // SUCCESS DIALOG

    private void ShowSuccess(
        string title,
        string message)
    {
        var successDialog = new CustomDialog
        {
            Owner = this
        };

        successDialog.Configure(
            title: title,
            message: message,
            confirmText: "Done",
            showCancel: false,
            showInput: false);

        successDialog.SetAppearance(
            icon: "✓",
            iconColor: "#16A34A",
            iconBackground: "#DCFCE7",
            confirmBackground: new SolidColorBrush(
                Color.FromRgb(22, 163, 74)));

        successDialog.ShowDialog();
    }

    // CLOSE

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    // ERROR MESSAGE

    private void ShowError(string message)
    {
        MessageBox.Show(
            message,
            "Appointment Management",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
