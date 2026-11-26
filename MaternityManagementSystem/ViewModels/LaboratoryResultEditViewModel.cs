
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
    private LaboratoryResultStatus? status;

    [ObservableProperty]
    private IReadOnlyList<LaboratoryResultStatusOption> statusOptions =
        LaboratoryResultStatusOption.All;

    [ObservableProperty]
    private DateTime? requestedDate;

    [ObservableProperty]
    private DateTime? testDate;

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

    public bool IsTestDateEnabled =>
        Status is LaboratoryResultStatus.AwaitingResult or
            LaboratoryResultStatus.Resulted;

    public bool IsResultEnabled =>
        Status == LaboratoryResultStatus.Resulted;

    public bool IsRequestedDateRequired =>
        Status is LaboratoryResultStatus.Requested or
            LaboratoryResultStatus.AwaitingResult;

    public string RequestedDateLabel =>
        IsRequestedDateRequired
            ? "Requested Date (Required)"
            : "Requested Date (Optional)";

    public string RequestedDateHelpText
    {
        get
        {
            if (Status is null && RequestedDate is null)
            {
                return "This legacy record has no requested date. Enter one before selecting Requested or Awaiting Result.";
            }

            if (IsRequestedDateRequired && !RequestedDate.HasValue)
            {
                return "Enter a requested date before saving this status.";
            }

            return IsRequestedDateRequired
                ? "Required for Requested and Awaiting Result."
                : "Optional for Resulted.";
        }
    }

    public string StatusHelpText =>
        Status is null
            ? "This legacy record has no status. Select a status explicitly; none is inferred."
            : string.Empty;

    partial void OnStatusChanged(LaboratoryResultStatus? value)
    {
        OnPropertyChanged(nameof(IsTestDateEnabled));
        OnPropertyChanged(nameof(IsResultEnabled));
        OnPropertyChanged(nameof(IsRequestedDateRequired));
        OnPropertyChanged(nameof(RequestedDateLabel));
        OnPropertyChanged(nameof(RequestedDateHelpText));
        OnPropertyChanged(nameof(StatusHelpText));
    }

    partial void OnRequestedDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(RequestedDateHelpText));
    }

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

            Status = laboratoryResult.Status;
            StatusOptions = LaboratoryResultStatusOption
                .ForCurrentStatus(laboratoryResult.Status);
            RequestedDate = laboratoryResult.RequestedDate?.ToDateTime(
                TimeOnly.MinValue);

            PatientName =
                $"{laboratoryResult.Patient.FirstName} {laboratoryResult.Patient.LastName}";

            PrenatalVisitDisplay =
                laboratoryResult.PrenatalVisit is null
                    ? "Not associated with a prenatal visit"
                    : $"Visit dated {laboratoryResult.PrenatalVisit.VisitDate:MMM dd, yyyy}";

            TestName = laboratoryResult.TestName;
            TestDate = laboratoryResult.TestDate?.ToDateTime(TimeOnly.MinValue);
            Result = laboratoryResult.Result ?? string.Empty;
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
            if (Status is null)
            {
                throw new ArgumentException(
                    "Select a laboratory status before saving this record.");
            }

            var updatedLaboratoryResult = new LaboratoryResult
            {
                Id = _existingLaboratoryResult.Id,
                PatientId = _existingLaboratoryResult.PatientId,
                PrenatalVisitId = _existingLaboratoryResult.PrenatalVisitId,
                Status = Status,
                RequestedDate = RequestedDate.HasValue
                    ? DateOnly.FromDateTime(RequestedDate.Value)
                    : null,
                TestName = TestName,
                TestDate = TestDate.HasValue
                    ? DateOnly.FromDateTime(TestDate.Value)
                    : null,
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
