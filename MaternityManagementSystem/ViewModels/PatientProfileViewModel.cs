
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MaternityManagementSystem.ViewModels;

public partial class PatientProfileViewModel : ObservableObject
{
    private readonly PatientRepository _patientRepository;
    private readonly PregnancyRepository _pregnancyRepository;
    private readonly int _patientId;

    public PatientProfileViewModel(
        PatientRepository patientRepository,
        PregnancyRepository pregnancyRepository,
        int patientId)
    {
        ArgumentNullException.ThrowIfNull(patientRepository);
        ArgumentNullException.ThrowIfNull(pregnancyRepository);

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        _patientRepository = patientRepository;
        _pregnancyRepository = pregnancyRepository;
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
            Patient = await _patientRepository.GetByIdAsync(_patientId);

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
        var records = await _pregnancyRepository.GetByPatientIdAsync(_patientId);

        Pregnancies.Clear();

        foreach (var pregnancy in records)
        {
            Pregnancies.Add(pregnancy);
        }
    }
}
