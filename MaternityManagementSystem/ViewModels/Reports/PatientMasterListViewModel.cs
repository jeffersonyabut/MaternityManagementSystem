
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Services.Reports;

namespace MaternityManagementSystem.ViewModels.Reports;

public partial class PatientMasterListViewModel : ObservableObject
{
    private readonly PatientReportService _patientReportService;

    public PatientMasterListViewModel(
        PatientReportService patientReportService)
    {
        ArgumentNullException.ThrowIfNull(patientReportService);

        _patientReportService = patientReportService;
    }

    [ObservableProperty]
    private DateTime? startDate;

    [ObservableProperty]
    private DateTime? endDate;

    [ObservableProperty]
    private string selectedSex = "All";

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private PatientMasterListReport? report;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public List<string> SexOptions { get; } =
    [
        "All",
        "Female",
        "Male",
        "Prefer not to say"
    ];

    // Report preview states.
    public bool HasGeneratedReport => Report is not null;

    public bool HasPatients =>
        Report is not null && Report.Patients.Count > 0;

    public bool HasNoMatchingPatients =>
        Report is not null && Report.Patients.Count == 0;

    partial void OnReportChanged(PatientMasterListReport? value)
    {
        OnPropertyChanged(nameof(HasGeneratedReport));
        OnPropertyChanged(nameof(HasPatients));
        OnPropertyChanged(nameof(HasNoMatchingPatients));
    }

    [RelayCommand]
    private async Task GenerateReportAsync(
        CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        ErrorMessage = string.Empty;

        if (StartDate.HasValue &&
            EndDate.HasValue &&
            EndDate.Value.Date < StartDate.Value.Date)
        {
            ErrorMessage =
                "The end date cannot be earlier than the start date.";

            return;
        }

        try
        {
            IsLoading = true;

            DateOnly? start = StartDate.HasValue
                ? DateOnly.FromDateTime(StartDate.Value)
                : null;

            DateOnly? end = EndDate.HasValue
                ? DateOnly.FromDateTime(EndDate.Value)
                : null;

            string? sex = SelectedSex == "All"
                ? null
                : SelectedSex;

            Report =
                await _patientReportService
                    .GeneratePatientMasterListAsync(
                        start,
                        end,
                        sex,
                        SearchTerm,
                        cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Report generation was cancelled.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage =
                "Your session has expired. Please sign in again.";
        }
        catch (Exception)
        {
            ErrorMessage =
                "Unable to generate the patient report. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        StartDate = null;
        EndDate = null;
        SelectedSex = "All";
        SearchTerm = string.Empty;
        Report = null;
        ErrorMessage = string.Empty;
    }
}
