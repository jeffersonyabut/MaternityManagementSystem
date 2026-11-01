
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class AppointmentViewModel : ObservableObject
{
    private readonly AppointmentService _appointmentService;
    private readonly PatientRepository _patientRepository;
    private readonly PregnancyRepository _pregnancyRepository;

    private CancellationTokenSource? _patientSearchCancellationTokenSource;
    private int _patientSearchRequestId;

    private const int PatientPageSize = 25;

    public AppointmentViewModel(
        AppointmentService appointmentService,
        PatientRepository patientRepository,
        PregnancyRepository pregnancyRepository)
    {
        ArgumentNullException.ThrowIfNull(appointmentService);
        ArgumentNullException.ThrowIfNull(patientRepository);
        ArgumentNullException.ThrowIfNull(pregnancyRepository);

        _appointmentService = appointmentService;
        _patientRepository = patientRepository;
        _pregnancyRepository = pregnancyRepository;
    }

    // APPOINTMENT COLLECTIONS

    [ObservableProperty]
    private ObservableCollection<Appointment> appointments = new();

    [ObservableProperty]
    private Appointment? selectedAppointment;

    // PATIENT SELECTION

    [ObservableProperty]
    private ObservableCollection<Patient> patients = new();

    [ObservableProperty]
    private Patient? selectedPatient;

    [ObservableProperty]
    private string patientSearchText = string.Empty;

    [ObservableProperty]
    private int patientCurrentPage = 1;

    [ObservableProperty]
    private int patientTotalPages = 1;

    [ObservableProperty]
    private int patientTotalResults;

    public bool CanGoToPreviousPatientPage => PatientCurrentPage > 1;

    public bool CanGoToNextPatientPage =>
        PatientCurrentPage < PatientTotalPages;

    public bool HasSelectedPatient => SelectedPatient is not null;

    partial void OnSelectedPatientChanged(Patient? value)
    {
        OnPropertyChanged(nameof(HasSelectedPatient));
    }

    partial void OnPatientCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoToPreviousPatientPage));
        OnPropertyChanged(nameof(CanGoToNextPatientPage));
    }

    partial void OnPatientTotalPagesChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoToNextPatientPage));
    }

    partial void OnPatientSearchTextChanged(string value)
    {
        PatientCurrentPage = 1;
        SelectedPatient = null;
        SelectedPregnancy = null;
        Pregnancies.Clear();

        _ = SearchPatientsAsync();
    }

    // PREGNANCY SELECTION

    [ObservableProperty]
    private ObservableCollection<Pregnancy> pregnancies = new();

    [ObservableProperty]
    private Pregnancy? selectedPregnancy;

    [ObservableProperty]
    private bool isLoadingPatientOptions;

    // GENERAL STATE

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private DateOnly? selectedDate;

    [ObservableProperty]
    private string selectedStatus = "All";

    [ObservableProperty]
    private string searchText = string.Empty;

    // PATIENT LOADING AND SEARCH

    public async Task LoadPatientsAsync()
    {
        await SearchPatientsAsync();
    }

    public async Task SearchPatientsAsync(string searchTerm)
    {
        PatientSearchText = searchTerm?.Trim() ?? string.Empty;
        await SearchPatientsAsync();
    }

    public async Task SearchPatientsAsync()
    {
        var cancellationSource = new CancellationTokenSource();

        var previousSource = _patientSearchCancellationTokenSource;
        _patientSearchCancellationTokenSource = cancellationSource;

        // Safely cancel the previous search.
        // Its CancellationTokenSource may already have been disposed.
        try
        {
            previousSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The previous search has already completed.
        }

        int requestId = Interlocked.Increment(
            ref _patientSearchRequestId);

        IsLoadingPatientOptions = true;

        try
        {
            string searchTerm = PatientSearchText.Trim();

            int totalResults =
                await _patientRepository.CountSearchResultsAsync(
                    searchTerm,
                    cancellationSource.Token);

            if (requestId != _patientSearchRequestId)
            {
                return;
            }

            PatientTotalResults = totalResults;

            PatientTotalPages = Math.Max(
                1,
                (int)Math.Ceiling(
                    (double)totalResults / PatientPageSize));

            if (PatientCurrentPage > PatientTotalPages)
            {
                PatientCurrentPage = PatientTotalPages;
            }

            var records =
                await _patientRepository.SearchPageAsync(
                    searchTerm,
                    PatientCurrentPage,
                    PatientPageSize,
                    cancellationSource.Token);

            if (requestId != _patientSearchRequestId)
            {
                return;
            }

            Patients.Clear();

            foreach (var patient in records)
            {
                Patients.Add(patient);
            }

            StatusMessage = totalResults == 0
                ? "No matching patients found."
                : $"Showing {Patients.Count} of {totalResults} patient(s).";
        }
        catch (OperationCanceledException)
        {
            // A newer search has replaced this request.
        }
        catch (Exception)
        {
            if (requestId == _patientSearchRequestId)
            {
                StatusMessage = "Unable to search registered patients.";
            }
        }
        finally
        {
            if (requestId == _patientSearchRequestId)
            {
                IsLoadingPatientOptions = false;
            }

            cancellationSource.Dispose();

            if (ReferenceEquals(
                _patientSearchCancellationTokenSource,
                cancellationSource))
            {
                _patientSearchCancellationTokenSource = null;
            }
        }
    }

    // PATIENT PAGINATION

    [RelayCommand]
    private async Task PreviousPatientPageAsync()
    {
        if (PatientCurrentPage <= 1 || IsLoadingPatientOptions)
        {
            return;
        }

        PatientCurrentPage--;
        await SearchPatientsAsync();
    }

    [RelayCommand]
    private async Task NextPatientPageAsync()
    {
        if (PatientCurrentPage >= PatientTotalPages ||
            IsLoadingPatientOptions)
        {
            return;
        }

        PatientCurrentPage++;
        await SearchPatientsAsync();
    }

    // PATIENT SELECTION AND PREGNANCY LOADING

    public async Task SelectPatientAsync(Patient? patient)
    {
        SelectedPatient = patient;

        SelectedPregnancy = null;
        Pregnancies.Clear();

        if (patient is null)
        {
            return;
        }

        IsLoadingPatientOptions = true;

        try
        {
            var records =
                await _pregnancyRepository.GetByPatientIdAsync(
                    patient.Id);

            if (SelectedPatient?.Id != patient.Id)
            {
                return;
            }

            foreach (var pregnancy in records)
            {
                Pregnancies.Add(pregnancy);
            }
        }
        catch (Exception)
        {
            StatusMessage =
                "Unable to load the patient's pregnancy records.";
        }
        finally
        {
            IsLoadingPatientOptions = false;
        }
    }

    // APPOINTMENT LOADING

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

            Appointments.Clear();

            foreach (var appointment in records)
            {
                Appointments.Add(appointment);
            }

            StatusMessage = Appointments.Count == 0
                ? "No appointments have been scheduled yet."
                : $"{Appointments.Count} appointment(s) loaded.";
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

    // CREATE APPOINTMENT

    public async Task<bool> CreateAppointmentAsync(
        Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var createdAppointment =
                await _appointmentService.CreateAppointmentAsync(
                    appointment);

            Appointments.Add(createdAppointment);
            SortAppointments();

            StatusMessage = "Appointment scheduled successfully.";
            return true;
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        catch (Exception)
        {
            StatusMessage = "Unable to schedule the appointment.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // UPDATE APPOINTMENT

    public async Task<bool> UpdateAppointmentAsync(
        Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var updatedAppointment =
                await _appointmentService.UpdateAppointmentAsync(
                    appointment);

            var existing = FindAppointment(updatedAppointment.Id);

            if (existing is not null)
            {
                int index = Appointments.IndexOf(existing);
                Appointments[index] = updatedAppointment;
            }

            SortAppointments();

            StatusMessage = "Appointment updated successfully.";
            return true;
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        catch (Exception)
        {
            StatusMessage = "Unable to update the appointment.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // CANCEL APPOINTMENT

    public async Task<bool> CancelAppointmentAsync(
        Appointment appointment,
        string cancellationReason)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        if (IsBusy)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(cancellationReason))
        {
            StatusMessage = "A cancellation reason is required.";
            return false;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            await _appointmentService.CancelAppointmentAsync(
                appointment.Id,
                cancellationReason);

            var existing = FindAppointment(appointment.Id);

            if (existing is not null)
            {
                existing.Status = "Cancelled";
                existing.CancellationReason = cancellationReason.Trim();
            }

            StatusMessage = "Appointment cancelled successfully.";
            return true;
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        catch (Exception)
        {
            StatusMessage = "Unable to cancel the appointment.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // UPDATE APPOINTMENT STATUS

    public async Task<bool> UpdateAppointmentStatusAsync(
        Appointment appointment,
        string newStatus)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            await _appointmentService.UpdateAppointmentStatusAsync(
                appointment.Id,
                newStatus);

            var existing = FindAppointment(appointment.Id);

            if (existing is not null)
            {
                existing.Status = newStatus.Trim();
            }

            StatusMessage = "Appointment status updated successfully.";
            return true;
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        catch (Exception)
        {
            StatusMessage = "Unable to update appointment status.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ARCHIVE APPOINTMENT

    public async Task<bool> ArchiveAppointmentAsync(
        Appointment appointment,
        string archiveReason)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        if (IsBusy)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(archiveReason))
        {
            StatusMessage = "An archive reason is required.";
            return false;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            await _appointmentService.ArchiveAppointmentAsync(
                appointment.Id,
                archiveReason);

            var existing = FindAppointment(appointment.Id);

            if (existing is not null)
            {
                Appointments.Remove(existing);
            }

            StatusMessage = "Appointment archived successfully.";
            return true;
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
        catch (Exception)
        {
            StatusMessage = "Unable to archive the appointment.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // FILTERING

    public ObservableCollection<Appointment> GetFilteredAppointments()
    {
        var filtered = new ObservableCollection<Appointment>();

        foreach (var appointment in Appointments)
        {
            bool matchesDate =
                !SelectedDate.HasValue ||
                appointment.AppointmentDate == SelectedDate.Value;

            bool matchesStatus =
                SelectedStatus == "All" ||
                string.Equals(
                    appointment.Status,
                    SelectedStatus,
                    StringComparison.OrdinalIgnoreCase);

            string patientName =
                $"{appointment.Patient?.FirstName} " +
                $"{appointment.Patient?.LastName}";

            bool matchesSearch =
                string.IsNullOrWhiteSpace(SearchText) ||
                patientName.Contains(
                    SearchText.Trim(),
                    StringComparison.OrdinalIgnoreCase) ||
                appointment.Purpose.Contains(
                    SearchText.Trim(),
                    StringComparison.OrdinalIgnoreCase);

            if (matchesDate && matchesStatus && matchesSearch)
            {
                filtered.Add(appointment);
            }
        }

        return filtered;
    }

    // HELPERS

    private Appointment? FindAppointment(int appointmentId)
    {
        foreach (var appointment in Appointments)
        {
            if (appointment.Id == appointmentId)
            {
                return appointment;
            }
        }

        return null;
    }

    private void SortAppointments()
    {
        var sorted = new List<Appointment>(Appointments);

        sorted.Sort((a, b) =>
        {
            int dateComparison =
                a.AppointmentDate.CompareTo(b.AppointmentDate);

            return dateComparison != 0
                ? dateComparison
                : a.StartTime.CompareTo(b.StartTime);
        });

        Appointments.Clear();

        foreach (var appointment in sorted)
        {
            Appointments.Add(appointment);
        }
    }
}
