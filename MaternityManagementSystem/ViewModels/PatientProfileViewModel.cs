
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MaternityManagementSystem.ViewModels;

public partial class PatientProfileViewModel : ObservableObject
{
    private readonly PatientService _patientService;
    private readonly PregnancyService _pregnancyService;
    private readonly int _patientId;

    public PatientProfileViewModel(
        PatientService patientService,
        PregnancyService pregnancyService,
        int patientId)
    {
        ArgumentNullException.ThrowIfNull(patientService);
        ArgumentNullException.ThrowIfNull(pregnancyService);

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        _patientService = patientService;
        _pregnancyService = pregnancyService;
        _patientId = patientId;
    }

    [ObservableProperty]
    private Patient? patient;

    [ObservableProperty]
    private ObservableCollection<Pregnancy> pregnancies = new();

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
            Patient = await _patientService.GetPatientByIdAsync(_patientId);

            if (Patient is null)
            {
                StatusMessage = "This patient record could not be found.";
                Pregnancies.Clear();
                return;
            }

            await LoadPregnanciesAsync();
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

    private async Task LoadPregnanciesAsync()
    {
        var records = await _pregnancyService.GetByPatientIdAsync(_patientId);

        Pregnancies.Clear();

        foreach (var pregnancy in records)
        {
            Pregnancies.Add(pregnancy);
        }
    }
}
