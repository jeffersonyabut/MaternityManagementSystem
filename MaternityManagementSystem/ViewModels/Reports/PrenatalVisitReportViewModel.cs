using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Services.Reports;

namespace MaternityManagementSystem.ViewModels.Reports;

public partial class PrenatalVisitReportViewModel : ObservableObject
{
    public static string GetLaboratoryStatusDisplay(
        LaboratoryResultStatus? status)
    {
        return status?.ToString() ?? "Unknown / Unclassified";
    }

    public static string GetLaboratoryResultDisplay(
        LaboratoryResultStatus? status,
        string? result)
    {
        if (!string.IsNullOrWhiteSpace(result))
        {
            return result;
        }

        return status == LaboratoryResultStatus.AwaitingResult
            ? "Result not yet available"
            : "—";
    }

    public static string GetLaboratoryValueDisplay(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    public static string GetLaboratoryDateDisplay(
        DateOnly? date,
        RegionalSettingsService regionalSettingsService)
    {
        ArgumentNullException.ThrowIfNull(regionalSettingsService);

        return date.HasValue
            ? regionalSettingsService.FormatDate(date.Value)
            : "—";
    }

    private readonly PrenatalVisitReportService _prenatalVisitReportService;
    private readonly int _visitId;

    public bool HasError =>
    !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasNoVitalSigns =>
        Report is not null && Report.TotalVitalSigns == 0;

    public bool HasNoLaboratoryResults =>
        Report is not null && Report.TotalLaboratoryResults == 0;

    public PrenatalVisitReportViewModel(
        PrenatalVisitReportService prenatalVisitReportService,
        int visitId)
    {
        ArgumentNullException.ThrowIfNull(prenatalVisitReportService);

        if (visitId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitId));
        }

        _prenatalVisitReportService = prenatalVisitReportService;
        _visitId = visitId;
    }

    [ObservableProperty]
    private PrenatalVisitReport? report;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public bool HasGeneratedReport => Report is not null;

    partial void OnReportChanged(PrenatalVisitReport? value)
    {
        OnPropertyChanged(nameof(HasGeneratedReport));
        OnPropertyChanged(nameof(HasNoVitalSigns));
        OnPropertyChanged(nameof(HasNoLaboratoryResults));
        OnPropertyChanged(nameof(HasError));
    }

    [RelayCommand]
    private async Task GenerateReportAsync(
        CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            Report = await _prenatalVisitReportService
                .GeneratePrenatalVisitReportAsync(
                    _visitId,
                    cancellationToken); // 


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
                "Unable to generate the prenatal visit report. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
