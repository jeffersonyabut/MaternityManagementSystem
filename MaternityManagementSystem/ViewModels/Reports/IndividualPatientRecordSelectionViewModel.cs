
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Services.Reports;

namespace MaternityManagementSystem.ViewModels.Reports;

public partial class IndividualPatientRecordSelectionViewModel
    : ObservableObject
{
    private readonly PatientService _patientService;
    private readonly IndividualPatientRecordReportService _reportService;

    private CancellationTokenSource? _searchCancellationTokenSource;
    private int _searchRequestId;

    private const int PageSize = 25;

    public IndividualPatientRecordSelectionViewModel(
        PatientService patientService,
        IndividualPatientRecordReportService reportService)
    {
        ArgumentNullException.ThrowIfNull(patientService);
        ArgumentNullException.ThrowIfNull(reportService);

        _patientService = patientService;
        _reportService = reportService;
    }

    public ObservableCollection<Patient> Patients { get; } = new();

    [ObservableProperty]
    private Patient? selectedPatient;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isGeneratingReport;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int totalPages = 1;

    public bool HasSelectedPatient => SelectedPatient is not null;

    public bool CanGoToPreviousPage => CurrentPage > 1;

    public bool CanGoToNextPage => CurrentPage < TotalPages;

    public bool CanGenerateReport =>
        HasSelectedPatient && !IsBusy && !IsGeneratingReport;

    public event Action<IndividualPatientRecordReport>?
        ReportGenerated;

    partial void OnSelectedPatientChanged(Patient? value)
    {
        OnPropertyChanged(nameof(HasSelectedPatient));
        OnPropertyChanged(nameof(CanGenerateReport));
    }

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
    }

    partial void OnTotalPagesChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoToNextPage));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGenerateReport));
    }

    partial void OnIsGeneratingReportChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGenerateReport));
    }

    partial void OnSearchTextChanged(string value)
    {
        CurrentPage = 1;
        SelectedPatient = null;

        _ = SearchPatientsAsync();
    }

    [RelayCommand]
    private async Task LoadPatientsAsync()
    {
        CurrentPage = 1;
        await SearchPatientsAsync();
    }

    [RelayCommand]
    private async Task SearchPatientsAsync()
    {
        int requestId = Interlocked.Increment(ref _searchRequestId);

        var cancellationTokenSource = new CancellationTokenSource();

        var previousCancellationTokenSource =
            Interlocked.Exchange(
                ref _searchCancellationTokenSource,
                cancellationTokenSource);

        previousCancellationTokenSource?.Cancel();

        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Searching patients...";

        try
        {
            string searchTerm = SearchText;

            int totalResults =
                await _patientService.CountPatientSearchResultsAsync(
                    searchTerm,
                    cancellationTokenSource.Token);

            if (requestId != Volatile.Read(ref _searchRequestId))
            {
                return;
            }

            TotalPages = Math.Max(
                1,
                (int)Math.Ceiling(totalResults / (double)PageSize));

            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }

            var results = await _patientService.SearchPatientsPageAsync(
                searchTerm,
                CurrentPage,
                PageSize,
                cancellationTokenSource.Token);

            if (requestId != Volatile.Read(ref _searchRequestId))
            {
                return;
            }

            Patients.Clear();

            foreach (Patient patient in results)
            {
                Patients.Add(patient);
            }

            StatusMessage = totalResults == 0
                ? "No patients found."
                : $"Showing {results.Count} of {totalResults} patient(s).";
        }
        catch (OperationCanceledException)
        {
            // A newer search replaced this request.
        }
        catch (Exception)
        {
            if (requestId == Volatile.Read(ref _searchRequestId))
            {
                Patients.Clear();
                StatusMessage = "Unable to load patients.";
                ErrorMessage =
                    "Unable to retrieve patient records. Please try again.";
            }
        }
        finally
        {
            if (requestId == Volatile.Read(ref _searchRequestId))
            {
                IsBusy = false;
            }

            Interlocked.CompareExchange(
                ref _searchCancellationTokenSource,
                null,
                cancellationTokenSource);

            cancellationTokenSource.Dispose();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CanGoToPreviousPage)
        {
            CurrentPage--;
            SelectedPatient = null;
            await SearchPatientsAsync();
        }
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CanGoToNextPage)
        {
            CurrentPage++;
            SelectedPatient = null;
            await SearchPatientsAsync();
        }
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        if (SelectedPatient is null)
        {
            StatusMessage = "Please select a patient first.";
            return;
        }

        IsGeneratingReport = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Generating individual patient record...";

        try
        {
            IndividualPatientRecordReport report =
                await _reportService.GenerateIndividualPatientRecordAsync(
                    SelectedPatient.Id);

            StatusMessage = "Patient report generated successfully.";

            ReportGenerated?.Invoke(report);
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage =
                "Your session has expired. Please sign in again.";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            ErrorMessage =
                "Unable to generate the patient report. Please try again.";
        }
        finally
        {
            IsGeneratingReport = false;
        }
    }
}
