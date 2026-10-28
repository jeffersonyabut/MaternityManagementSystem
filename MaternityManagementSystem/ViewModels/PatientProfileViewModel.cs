
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using System;
using System.Threading.Tasks;

namespace MaternityManagementSystem.ViewModels;

public partial class PatientProfileViewModel : ObservableObject
{
    private readonly PatientRepository _patientRepository;
    private readonly int _patientId;

    public PatientProfileViewModel(
        PatientRepository patientRepository,
        int patientId)
    {
        ArgumentNullException.ThrowIfNull(patientRepository);

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        _patientRepository = patientRepository;
        _patientId = patientId;
    }

    [ObservableProperty]
    private Patient? patient;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public async Task LoadPatientAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            Patient = await _patientRepository.GetByIdAsync(_patientId);

            if (Patient is null)
            {
                StatusMessage = "This patient record could not be found.";
            }
        }
        catch (Exception)
        {
            StatusMessage = "Unable to load the patient profile.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
