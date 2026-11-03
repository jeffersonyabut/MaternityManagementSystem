using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Services.Reports;

namespace MaternityManagementSystem.ViewModels.Reports;

public partial class AppointmentReportViewModel : ObservableObject
{
    private readonly AppointmentReportService _reportService;

    public AppointmentReportViewModel(
        AppointmentReportService reportService)
    {
        ArgumentNullException.ThrowIfNull(reportService);

        _reportService = reportService;
    }

    public List<string> StatusOptions { get; } =
    [
        "All",
        "Scheduled",
        "Confirmed",
        "Completed",
        "Cancelled",
        "No-show"
    ];

    [ObservableProperty]
    private DateTime? startDate;

    [ObservableProperty]
    private DateTime? endDate;

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private string status = "All";

    [ObservableProperty]
    private AppointmentReport? report;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasGeneratedReport => Report is not null;

    public bool HasAppointments =>
        Report is not null &&
        Report.Appointments.Count > 0;

    public bool HasNoAppointments =>
        Report is not null &&
        Report.Appointments.Count == 0;

    public bool IsNotLoading => !IsLoading;

    public int TotalAppointments =>
        Report?.TotalAppointments ?? 0;

    public int UniquePatientCount =>
        Report?.UniquePatientCount ?? 0;

    partial void OnReportChanged(AppointmentReport? value)
    {
        OnPropertyChanged(nameof(HasGeneratedReport));
        OnPropertyChanged(nameof(HasAppointments));
        OnPropertyChanged(nameof(HasNoAppointments));
        OnPropertyChanged(nameof(TotalAppointments));
        OnPropertyChanged(nameof(UniquePatientCount));
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

        if (StartDate.HasValue &&
            EndDate.HasValue &&
            StartDate.Value.Date > EndDate.Value.Date)
        {
            ErrorMessage =
                "The start date cannot be later than the end date.";

            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            Report = null;

            DateOnly? reportStartDate = StartDate.HasValue
                ? DateOnly.FromDateTime(StartDate.Value)
                : null;

            DateOnly? reportEndDate = EndDate.HasValue
                ? DateOnly.FromDateTime(EndDate.Value)
                : null;

            Report = await _reportService.GenerateAppointmentReportAsync(
                reportStartDate,
                reportEndDate,
                SearchTerm,
                Status,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Report generation was cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
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
        Status = "All";

        Report = null;
        ErrorMessage = null;
    }
}
