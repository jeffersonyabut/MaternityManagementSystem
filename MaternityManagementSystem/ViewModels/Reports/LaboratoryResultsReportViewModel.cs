
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Services.Reports;

namespace MaternityManagementSystem.ViewModels.Reports;

public partial class LaboratoryResultsReportViewModel : ObservableObject
{
    private readonly LaboratoryResultsReportService
        _laboratoryResultsReportService;

    public LaboratoryResultsReportViewModel(
        LaboratoryResultsReportService laboratoryResultsReportService)
    {
        ArgumentNullException.ThrowIfNull(
            laboratoryResultsReportService);

        _laboratoryResultsReportService =
            laboratoryResultsReportService;
    }

    [ObservableProperty]
    private DateTime? startDate;

    [ObservableProperty]
    private DateTime? endDate;

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private string testName = string.Empty;

    [ObservableProperty]
    private LaboratoryResultsReport? report;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public bool HasGeneratedReport => Report is not null;

    public bool HasLaboratoryResults =>
        Report is not null && Report.TotalResults > 0;

    public bool HasNoLaboratoryResults =>
        Report is not null && Report.TotalResults == 0;

    public bool HasError =>
        !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsNotLoading => !IsLoading;

    public int UniquePatientCount =>
        Report?.LaboratoryResults
            .Select(result => result.PatientId)
            .Distinct()
            .Count() ?? 0;

    partial void OnReportChanged(LaboratoryResultsReport? value)
    {
        OnPropertyChanged(nameof(HasGeneratedReport));
        OnPropertyChanged(nameof(HasLaboratoryResults));
        OnPropertyChanged(nameof(HasNoLaboratoryResults));
        OnPropertyChanged(nameof(UniquePatientCount));
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotLoading));
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

            Report = await _laboratoryResultsReportService
                .GenerateLaboratoryResultsReportAsync(
                    start,
                    end,
                    SearchTerm,
                    TestName,
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
                "Unable to generate the laboratory results report. Please try again.";
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
        SearchTerm = string.Empty;
        TestName = string.Empty;
        Report = null;
        ErrorMessage = string.Empty;
    }
}
