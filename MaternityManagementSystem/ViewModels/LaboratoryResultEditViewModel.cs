
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class LaboratoryResultEditViewModel : ObservableObject
{
    private readonly LaboratoryResultService _laboratoryResultService;

    private LaboratoryResult? _existingLaboratoryResult;

    public LaboratoryResultEditViewModel(
        LaboratoryResultService laboratoryResultService)
    {
        ArgumentNullException.ThrowIfNull(laboratoryResultService);

        _laboratoryResultService = laboratoryResultService;
    }

    [ObservableProperty]
    private string patientName = string.Empty;

    [ObservableProperty]
    private string prenatalVisitDisplay = "Not associated with a prenatal visit";

    [ObservableProperty]
    private string testName = string.Empty;

    [ObservableProperty]
    private DateTime? testDate = DateTime.Today;

    [ObservableProperty]
    private string result = string.Empty;

    [ObservableProperty]
    private string? unit;

    [ObservableProperty]
    private string? referenceRange;

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public event Action? LaboratoryResultUpdated;

    public async Task LoadLaboratoryResultAsync(int laboratoryResultId)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var laboratoryResult =
                await _laboratoryResultService.GetByIdAsync(
                    laboratoryResultId);

            if (laboratoryResult is null)
            {
                StatusMessage = "Laboratory result could not be found.";
                return;
            }

            if (laboratoryResult.IsArchived)
            {
                StatusMessage = "Archived laboratory results cannot be edited.";
                return;
            }

            _existingLaboratoryResult = laboratoryResult;

            PatientName =
                $"{laboratoryResult.Patient.FirstName} {laboratoryResult.Patient.LastName}";

            PrenatalVisitDisplay =
                laboratoryResult.PrenatalVisit is null
                    ? "Not associated with a prenatal visit"
                    : $"Visit dated {laboratoryResult.PrenatalVisit.VisitDate:MMM dd, yyyy}";

            TestName = laboratoryResult.TestName;
            TestDate = laboratoryResult.TestDate.ToDateTime(TimeOnly.MinValue);
            Result = laboratoryResult.Result;
            Unit = laboratoryResult.Unit;
            ReferenceRange = laboratoryResult.ReferenceRange;
            Notes = laboratoryResult.Notes;
        }
        catch (Exception)
        {
            StatusMessage = "Unable to load the laboratory result.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }


        if (_existingLaboratoryResult is null)
        {
            StatusMessage = "No laboratory result is loaded.";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            if (!TestDate.HasValue)
            {
                throw new ArgumentException("Laboratory test date is required.");
            }

            var updatedLaboratoryResult = new LaboratoryResult
            {
                Id = _existingLaboratoryResult.Id,
                PatientId = _existingLaboratoryResult.PatientId,
                PrenatalVisitId = _existingLaboratoryResult.PrenatalVisitId,
                TestName = TestName,
                TestDate = DateOnly.FromDateTime(TestDate.Value),
                Result = Result,
                Unit = Unit,
                ReferenceRange = ReferenceRange,
                Notes = Notes
            };

            await _laboratoryResultService.UpdateLaboratoryResultAsync(
                updatedLaboratoryResult);

            StatusMessage = "Laboratory result updated successfully.";

            LaboratoryResultUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
