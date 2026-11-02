
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Services.Reports;

namespace MaternityManagementSystem.ViewModels.Reports;

public partial class PrenatalCareReportViewModel : ObservableObject
{
    private readonly PrenatalCareReportService _prenatalCareReportService;

    public PrenatalCareReportViewModel(
        PrenatalCareReportService prenatalCareReportService)
    {
        ArgumentNullException.ThrowIfNull(prenatalCareReportService);

        _prenatalCareReportService = prenatalCareReportService;
    }

    [ObservableProperty]
    private DateTime? startDate;

    [ObservableProperty]
    private DateTime? endDate;

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private PrenatalCareReport? report;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public bool HasGeneratedReport => Report is not null;

    public bool HasPrenatalVisits =>
        Report is not null && Report.PrenatalVisits.Count > 0;

    public bool HasNoPrenatalVisits =>
        Report is not null && Report.PrenatalVisits.Count == 0;

    partial void OnReportChanged(PrenatalCareReport? value)
    {
        OnPropertyChanged(nameof(HasGeneratedReport));
        OnPropertyChanged(nameof(HasPrenatalVisits));
        OnPropertyChanged(nameof(HasNoPrenatalVisits));
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

            Report = await _prenatalCareReportService
                .GeneratePrenatalCareReportAsync(
                    start,
                    end,
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
                "Unable to generate the prenatal care report. Please try again.";
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
        Report = null;
        ErrorMessage = string.Empty;
    }
}
