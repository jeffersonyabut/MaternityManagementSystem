using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MaternityManagementSystem.ViewModels;

public partial class PatientRegistrationViewModel : ObservableObject
{
    private readonly PatientService _patientService;

    public PatientRegistrationViewModel(PatientService patientService)
    {
        ArgumentNullException.ThrowIfNull(patientService);
        _patientService = patientService;
    }

    [ObservableProperty]
    private string firstName = string.Empty;

    [ObservableProperty]
    private string middleName = string.Empty;

    [ObservableProperty]
    private string lastName = string.Empty;

    [ObservableProperty]
    private DateTime? dateOfBirth;

    [ObservableProperty]
    private string sex = string.Empty;

    [ObservableProperty]
    private string civilStatus = string.Empty;

    [ObservableProperty]
    private string contactNumber = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string address = string.Empty;

    [ObservableProperty]
    private string emergencyContactName = string.Empty;

    [ObservableProperty]
    private string emergencyContactNumber = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private async Task RegisterPatientAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            Patient patient = new()
            {
                FirstName = FirstName,
                MiddleName = MiddleName,
                LastName = LastName,
                DateOfBirth = DateOfBirth.HasValue
                    ? DateOnly.FromDateTime(DateOfBirth.Value)
                    : null,
                Sex = NormalizeOptionalText(Sex),
                CivilStatus = NormalizeOptionalText(CivilStatus),
                ContactNumber = ContactNumber,
                Email = Email,
                Address = Address,
                EmergencyContactName = EmergencyContactName,
                EmergencyContactNumber = EmergencyContactNumber
            };

            Patient registeredPatient =
                await _patientService.RegisterPatientAsync(patient);

            ClearForm();

            StatusMessage =
                $"Patient registered successfully. Patient number: {registeredPatient.PatientNumber}";
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception)
        {
            StatusMessage =
                "Unable to register the patient. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        FirstName = string.Empty;
        MiddleName = string.Empty;
        LastName = string.Empty;
        DateOfBirth = null;
        Sex = string.Empty;
        CivilStatus = string.Empty;
        ContactNumber = string.Empty;
        Email = string.Empty;
        Address = string.Empty;
        EmergencyContactName = string.Empty;
        EmergencyContactNumber = string.Empty;
        StatusMessage = string.Empty;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
