
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class AppointmentOverviewViewModel : ObservableObject
{
    private readonly AppointmentService _appointmentService;
    private readonly RegionalSettingsService _regionalSettingsService;

    private readonly ObservableCollection<Appointment> _allAppointments = new();


    public AppointmentOverviewViewModel(
        AppointmentService appointmentService,
        RegionalSettingsService regionalSettingsService)
    {
        ArgumentNullException.ThrowIfNull(appointmentService);
        ArgumentNullException.ThrowIfNull(regionalSettingsService);

        _appointmentService = appointmentService;
        _regionalSettingsService = regionalSettingsService;
    }

    // APPOINTMENT COLLECTIONS

    [ObservableProperty]
    private ObservableCollection<Appointment> appointments = new();

    [ObservableProperty]
    private Appointment? selectedAppointment;



    // SEARCH AND FILTERS

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedStatus = "All";

    [ObservableProperty]
    private DateOnly? selectedDate;

    public ObservableCollection<string> StatusOptions { get; } =
    [
        "All",
        "Scheduled",
        "Confirmed",
        "Completed",
        "Cancelled",
        "No-show"
    ];

    // SUMMARY COUNTS

    [ObservableProperty]
    private int todayCount;

    [ObservableProperty]
    private int upcomingCount;

    [ObservableProperty]
    private int completedCount;

    [ObservableProperty]
    private int cancelledCount;

    // GENERAL STATE

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    // PROPERTY CHANGE HANDLERS

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedStatusChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedDateChanged(DateOnly? value)
    {
        ApplyFilters();
    }

    // LOAD APPOINTMENTS

    public async Task LoadAppointmentsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var records =
                await _appointmentService.GetAllAsync();

            _allAppointments.Clear();

            foreach (var appointment in records)
            {
                _allAppointments.Add(appointment);
            }

            UpdateSummaryCounts();
            ApplyFilters();

            StatusMessage = _allAppointments.Count == 0
                ? "No appointments have been scheduled yet."
                : $"{_allAppointments.Count} appointment(s) loaded.";
        }
        catch (Exception)
        {
            StatusMessage = "Unable to load appointments.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // SUMMARY CALCULATIONS

    private void UpdateSummaryCounts()
    {
        DateOnly today = DateOnly.FromDateTime(
            _regionalSettingsService.ConvertUtcToLocal(
                DateTime.UtcNow));

        TodayCount = _allAppointments.Count(appointment =>
            appointment.AppointmentDate == today);

        UpcomingCount = _allAppointments.Count(appointment =>
            appointment.AppointmentDate > today &&
            appointment.Status is "Scheduled" or "Confirmed");

        CompletedCount = _allAppointments.Count(appointment =>
            appointment.Status == "Completed");

        CancelledCount = _allAppointments.Count(appointment =>
            appointment.Status == "Cancelled");
    }

    // SEARCH AND FILTERING

    private void ApplyFilters()
    {
        string search = SearchText.Trim();

        var filtered = _allAppointments
            .Where(appointment =>
            {
                bool matchesStatus =
                    SelectedStatus == "All" ||
                    string.Equals(
                        appointment.Status,
                        SelectedStatus,
                        StringComparison.OrdinalIgnoreCase);

                bool matchesDate =
                    !SelectedDate.HasValue ||
                    appointment.AppointmentDate == SelectedDate.Value;

                string patientName = GetPatientName(appointment);

                bool matchesSearch =
                    string.IsNullOrWhiteSpace(search) ||
                    patientName.Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase) ||
                    appointment.Purpose.Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase);

                return matchesStatus &&
                       matchesDate &&
                       matchesSearch;
            })
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.StartTime)
            .ToList();

        Appointments.Clear();

        foreach (var appointment in filtered)
        {
            Appointments.Add(appointment);
        }

        SelectedAppointment = null;
    }

    // HELPERS

    private static string GetPatientName(Appointment appointment)
    {
        var patient = appointment.Patient;

        if (patient is null)
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            new[]
            {
                patient.FirstName,
                patient.MiddleName,
                patient.LastName
            }.Where(name => !string.IsNullOrWhiteSpace(name)));
    }

    // CLEAR FILTERS

    public void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedStatus = "All";
        SelectedDate = null;
    }
}
