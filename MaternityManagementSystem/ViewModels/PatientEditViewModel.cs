
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;
using System;
using System.Threading.Tasks;

namespace MaternityManagementSystem.ViewModels;

public partial class PatientEditViewModel : ObservableObject
{
    private readonly PatientService _patientService;
    private readonly Patient originalPatient;

    public PatientEditViewModel(
        PatientService patientService,
        Patient patient)
    {
        ArgumentNullException.ThrowIfNull(patientService);
        ArgumentNullException.ThrowIfNull(patient);

        _patientService = patientService;
        originalPatient = patient;

        FirstName = patient.FirstName;
        MiddleName = patient.MiddleName ?? string.Empty;
        LastName = patient.LastName;
        DateOfBirth = patient.DateOfBirth;
        Sex = patient.Sex ?? string.Empty;
        CivilStatus = patient.CivilStatus ?? string.Empty;
        ContactNumber = patient.ContactNumber ?? string.Empty;
        Email = patient.Email ?? string.Empty;
        Address = patient.Address ?? string.Empty;
        EmergencyContactName = patient.EmergencyContactName ?? string.Empty;
        EmergencyContactNumber = patient.EmergencyContactNumber ?? string.Empty;
    }

    [ObservableProperty] private string firstName = string.Empty;
    [ObservableProperty] private string middleName = string.Empty;
    [ObservableProperty] private string lastName = string.Empty;
    [ObservableProperty] private DateOnly? dateOfBirth;

    public DateTime? DateOfBirthForPicker
    {
        get => DateOfBirth?.ToDateTime(TimeOnly.MinValue);

        set
        {
            DateOfBirth = value.HasValue
                ? DateOnly.FromDateTime(value.Value)
                : null;

            OnPropertyChanged(nameof(DateOfBirthForPicker));
        }
    }

    [ObservableProperty] private string sex = string.Empty;
    [ObservableProperty] private string civilStatus = string.Empty;
    [ObservableProperty] private string contactNumber = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string address = string.Empty;
    [ObservableProperty] private string emergencyContactName = string.Empty;
    [ObservableProperty] private string emergencyContactNumber = string.Empty;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool saveSucceeded;

    public event Action? PatientUpdated;

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        SaveSucceeded = false;

        try
        {
            var updatedPatient = new Patient
            {
                Id = originalPatient.Id,
                PatientNumber = originalPatient.PatientNumber,
                FirstName = FirstName,
                MiddleName = MiddleName,
                LastName = LastName,
                DateOfBirth = DateOfBirth,
                Sex = Sex,
                CivilStatus = CivilStatus,
                ContactNumber = ContactNumber,
                Email = Email,
                Address = Address,
                EmergencyContactName = EmergencyContactName,
                EmergencyContactNumber = EmergencyContactNumber,
                CreatedAtUtc = originalPatient.CreatedAtUtc
            };

            await _patientService.UpdatePatientAsync(updatedPatient);

            SaveSucceeded = true;
            StatusMessage = "Patient information updated successfully.";

            PatientUpdated?.Invoke();
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception)
        {
            StatusMessage = "Unable to save changes. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
