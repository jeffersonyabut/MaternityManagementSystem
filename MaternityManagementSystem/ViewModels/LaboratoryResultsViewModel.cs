
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class LaboratoryResultsViewModel : ObservableObject
{
    private readonly LaboratoryResultService _laboratoryResultService;
    private readonly int _patientId;
    private readonly int? _prenatalVisitId;

    public LaboratoryResultsViewModel(
        LaboratoryResultService laboratoryResultService,
        Patient patient,
        PrenatalVisit? prenatalVisit = null)
    {
        ArgumentNullException.ThrowIfNull(laboratoryResultService);
        ArgumentNullException.ThrowIfNull(patient);

        if (patient.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patient));
        }

        if (prenatalVisit is not null &&
            prenatalVisit.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(prenatalVisit));
        }

        _laboratoryResultService = laboratoryResultService;
        _patientId = patient.Id;
        _prenatalVisitId = prenatalVisit?.Id;

        Patient = patient;
        PrenatalVisit = prenatalVisit;
    }

    [ObservableProperty]
    private Patient patient = null!;

    [ObservableProperty]
    private PrenatalVisit? prenatalVisit;

    [ObservableProperty]
    private ObservableCollection<LaboratoryResult> laboratoryResults = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public async Task LoadLaboratoryResultsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var records = _prenatalVisitId.HasValue
                ? await _laboratoryResultService
                    .GetByPrenatalVisitIdAsync(_prenatalVisitId.Value)
                : await _laboratoryResultService
                    .GetByPatientIdAsync(_patientId);

            LaboratoryResults.Clear();

            foreach (var record in records)
            {
                LaboratoryResults.Add(record);
            }

            if (LaboratoryResults.Count == 0)
            {
                StatusMessage = _prenatalVisitId.HasValue
                    ? "No laboratory results recorded for this prenatal visit yet."
                    : "No laboratory results recorded for this patient yet.";
            }
        }
        catch (Exception)
        {
            StatusMessage = "Unable to load laboratory results.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> ArchiveLaboratoryResultAsync(
        LaboratoryResult laboratoryResult,
        string archiveReason)
    {
        ArgumentNullException.ThrowIfNull(laboratoryResult);

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
            await _laboratoryResultService.ArchiveLaboratoryResultAsync(
                laboratoryResult.Id,
                archiveReason);

            LaboratoryResults.Remove(laboratoryResult);

            StatusMessage = LaboratoryResults.Count == 0
                ? "No active laboratory results remain."
                : "Laboratory result archived successfully.";

            return true;
        }
        catch (Exception)
        {
            StatusMessage = "Unable to archive the laboratory result.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
