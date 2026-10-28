
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using MaternityManagementSystem.Views;


namespace MaternityManagementSystem.ViewModels;

public partial class PatientListViewModel : ObservableObject
{
    private readonly PatientRepository _patientRepository;

    private CancellationTokenSource? _searchCancellationTokenSource;
    private int _searchRequestId;

    private const int PageSize = 25;

    public PatientListViewModel(PatientRepository patientRepository)
    {
        ArgumentNullException.ThrowIfNull(patientRepository);
        _patientRepository = patientRepository;
    }

    public ObservableCollection<Patient> Patients { get; } = new();


    [ObservableProperty]
    private Patient? selectedPatient;


    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int totalPages = 1;

    public bool CanGoToPreviousPage => CurrentPage > 1;

    public bool CanGoToNextPage => CurrentPage < TotalPages;

    public bool HasSelectedPatient => SelectedPatient is not null;

    partial void OnSelectedPatientChanged(Patient? value)
    {
        OnPropertyChanged(nameof(HasSelectedPatient));
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

    partial void OnSearchTextChanged(string value)
    {
        CurrentPage = 1;
        _ = SearchPatientsAsync();
    }

    [RelayCommand]
    private async Task LoadPatientsAsync()
    {
        CurrentPage = 1;
        await SearchPatientsAsync();
    }


    [RelayCommand]
    private void OpenProfile()
    {
        if (SelectedPatient is null)
        {
            StatusMessage = "Please select a patient first.";
            return;
        }

        OpenProfileRequested?.Invoke(SelectedPatient.Id);
    }

    public event Action<int>? OpenProfileRequested;



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
        StatusMessage = string.Empty;

        try
        {
            string searchTerm = SearchText;

            int totalResults =
                await _patientRepository.CountSearchResultsAsync(
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

            var results = await _patientRepository.SearchPageAsync(
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
                StatusMessage = "Unable to load patients. Please try again.";
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
            await SearchPatientsAsync();
        }
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CanGoToNextPage)
        {
            CurrentPage++;
            await SearchPatientsAsync();
        }
    }
}
