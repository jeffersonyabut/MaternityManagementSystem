
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class LaboratoryResultRegistrationViewModel : ObservableObject
{
    private readonly LaboratoryResultService _laboratoryResultService;
    private readonly int _patientId;

    public LaboratoryResultRegistrationViewModel(
        LaboratoryResultService laboratoryResultService,
        PregnancyRepository pregnancyRepository,
        PrenatalVisitRepository prenatalVisitRepository,
        Patient patient)
    {
        ArgumentNullException.ThrowIfNull(laboratoryResultService);
        ArgumentNullException.ThrowIfNull(pregnancyRepository);
        ArgumentNullException.ThrowIfNull(prenatalVisitRepository);
        ArgumentNullException.ThrowIfNull(patient);

        if (patient.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patient));
        }

        _laboratoryResultService = laboratoryResultService;
        _patientId = patient.Id;

        Patient = patient;

        _pregnancyRepository = pregnancyRepository;
        _prenatalVisitRepository = prenatalVisitRepository;
    }

    private readonly PregnancyRepository _pregnancyRepository;
    private readonly PrenatalVisitRepository _prenatalVisitRepository;

    [ObservableProperty]
    private Patient patient = null!;

    [ObservableProperty]
    private string testName = string.Empty;

    [ObservableProperty]
    private LaboratoryResultStatus? status =
        LaboratoryResultStatus.Requested;

    [ObservableProperty]
    private IReadOnlyList<LaboratoryResultStatusOption> statusOptions =
        LaboratoryResultStatusOption.All;

    [ObservableProperty]
    private DateTime? requestedDate = DateTime.Today;

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
    private VisitOption? selectedVisit;

    [ObservableProperty]
    private ObservableCollection<VisitOption> visitOptions = new();

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

    public string RequestedDateHelpText =>
        IsRequestedDateRequired && !RequestedDate.HasValue
            ? "Enter a requested date before saving this status."
            : IsRequestedDateRequired
                ? "Required for Requested and Awaiting Result."
                : "Optional for Resulted.";

    partial void OnStatusChanged(LaboratoryResultStatus? value)
    {
        OnPropertyChanged(nameof(IsTestDateEnabled));
        OnPropertyChanged(nameof(IsResultEnabled));
        OnPropertyChanged(nameof(IsRequestedDateRequired));
        OnPropertyChanged(nameof(RequestedDateLabel));
        OnPropertyChanged(nameof(RequestedDateHelpText));
    }

    partial void OnRequestedDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(RequestedDateHelpText));
    }

    public event Action? LaboratoryResultSaved;

    public async Task LoadVisitOptionsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            VisitOptions.Clear();

            var pregnancies = await _pregnancyRepository
                .GetByPatientIdAsync(_patientId);

            foreach (var pregnancy in pregnancies)
            {
                var visits = await _prenatalVisitRepository
                    .GetByPregnancyIdAsync(pregnancy.Id);

                foreach (var visit in visits)
                {
                    VisitOptions.Add(new VisitOption
                    {
                        Visit = visit,
                        DisplayText =
                            $"{visit.VisitDate:MMM dd, yyyy} · Pregnancy #{pregnancy.Id}"
                    });
                }
            }

            VisitOptions.Insert(0, new VisitOption
            {
                Visit = null,
                DisplayText = "Not associated with a prenatal visit"
            });

            SelectedVisit = VisitOptions.FirstOrDefault();
        }
        catch (Exception)
        {
            StatusMessage = "Unable to load the patient's prenatal visits.";
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

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var laboratoryResult = new LaboratoryResult
            {
                PatientId = _patientId,
                PrenatalVisitId = SelectedVisit?.Visit?.Id,
                TestName = TestName,
                Status = Status,
                RequestedDate = RequestedDate.HasValue
                    ? DateOnly.FromDateTime(RequestedDate.Value)
                    : null,
                TestDate = TestDate.HasValue
                    ? DateOnly.FromDateTime(TestDate.Value)
                    : null,
                Result = Result,
                Unit = Unit,
                ReferenceRange = ReferenceRange,
                Notes = Notes
            };

            await _laboratoryResultService
                .CreateLaboratoryResultAsync(laboratoryResult);

            StatusMessage = "Laboratory result saved successfully.";

            LaboratoryResultSaved?.Invoke();
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

public sealed class LaboratoryResultStatusOption
{
    private LaboratoryResultStatusOption(
        LaboratoryResultStatus value,
        string displayText)
    {
        Value = value;
        DisplayText = displayText;
    }

    public LaboratoryResultStatus Value { get; }

    public string DisplayText { get; }

    public static IReadOnlyList<LaboratoryResultStatusOption> All { get; } =
    [
        new(LaboratoryResultStatus.Requested, "Requested"),
        new(LaboratoryResultStatus.AwaitingResult, "Awaiting Result"),
        new(LaboratoryResultStatus.Resulted, "Resulted")
    ];

    public static IReadOnlyList<LaboratoryResultStatusOption> ForCurrentStatus(
        LaboratoryResultStatus? currentStatus)
    {
        if (currentStatus is null)
        {
            return All;
        }

        return currentStatus.Value switch
        {
            LaboratoryResultStatus.Requested =>
            [
                All[0],
                All[1],
                All[2]
            ],
            LaboratoryResultStatus.AwaitingResult =>
            [
                All[1],
                All[2]
            ],
            LaboratoryResultStatus.Resulted => [All[2]],
            _ => All
        };
    }
}

public class VisitOption
{
    public PrenatalVisit? Visit { get; set; }

    public string DisplayText { get; set; } = string.Empty;
}
