
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class PrenatalVisitRegistrationViewModel : ObservableObject
{
    private readonly PrenatalVisitService _prenatalVisitService;
    private readonly int _pregnancyId;

    public PrenatalVisitRegistrationViewModel(
        PrenatalVisitService prenatalVisitService,
        Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(prenatalVisitService);
        ArgumentNullException.ThrowIfNull(pregnancy);

        if (pregnancy.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pregnancy));
        }

        _prenatalVisitService = prenatalVisitService;
        _pregnancyId = pregnancy.Id;

        VisitDate = DateTime.Today;
    }

    [ObservableProperty]
    private DateTime? visitDate;

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public bool CanSave => !IsBusy && VisitDate.HasValue;

    public event Action? VisitSaved;

    partial void OnVisitDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    public async Task SaveAsync()
    {
        if (IsBusy || !CanSave)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            DateOnly visitDate = DateOnly.FromDateTime(
                VisitDate!.Value);

            var visit = new PrenatalVisit
            {
                PregnancyId = _pregnancyId,
                VisitDate = visitDate,
                Notes = string.IsNullOrWhiteSpace(Notes)
                    ? null
                    : Notes.Trim()
            };

            await _prenatalVisitService.CreateVisitAsync(visit);

            VisitSaved?.Invoke();
        }
        catch (Exception)
        {
            StatusMessage = "Unable to save the prenatal visit. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
