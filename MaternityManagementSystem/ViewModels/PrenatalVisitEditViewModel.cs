using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class PrenatalVisitEditViewModel : ObservableObject
{
    private readonly PrenatalVisitService _prenatalVisitService;
    private readonly int _visitId;

    private PrenatalVisit? _originalVisit;

    public PrenatalVisitEditViewModel(
        PrenatalVisitService prenatalVisitService,
        int visitId)
    {
        ArgumentNullException.ThrowIfNull(prenatalVisitService);

        if (visitId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitId));
        }

        _prenatalVisitService = prenatalVisitService;
        _visitId = visitId;
    }

    [ObservableProperty]
    private DateTime? visitDate;

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isLoaded;

    public bool CanSave =>
        !IsBusy && IsLoaded && VisitDate.HasValue;

    public event Action? VisitUpdated;

    partial void OnVisitDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnIsLoadedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    public async Task LoadVisitAsync()
    {
        if (IsBusy || IsLoaded)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var visit = await _prenatalVisitService
                .GetByIdAsync(_visitId);

            if (visit is null)
            {
                StatusMessage =
                    "The prenatal visit record could not be found.";
                return;
            }

            _originalVisit = visit;

            VisitDate = visit.VisitDate.ToDateTime(TimeOnly.MinValue);
            Notes = visit.Notes ?? string.Empty;

            IsLoaded = true;
        }
        catch (Exception)
        {
            StatusMessage =
                "Unable to load the prenatal visit. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveAsync()
    {
        if (IsBusy || !CanSave || _originalVisit is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            DateOnly visitDate = DateOnly.FromDateTime(
                VisitDate!.Value);

            var updatedVisit = new PrenatalVisit
            {
                Id = _originalVisit.Id,
                PregnancyId = _originalVisit.PregnancyId,
                VisitDate = visitDate,
                Notes = string.IsNullOrWhiteSpace(Notes)
                    ? null
                    : Notes.Trim()
            };

            await _prenatalVisitService.UpdateVisitAsync(updatedVisit);

            VisitUpdated?.Invoke();
        }
        catch (Exception)
        {
            StatusMessage =
                "Unable to update the prenatal visit. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
