
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class PrenatalVisitViewModel : ObservableObject
{
    private readonly PrenatalVisitService _prenatalVisitService;
    private readonly int _pregnancyId;

    public PrenatalVisitViewModel(
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

        Pregnancy = pregnancy;
    }

    [ObservableProperty]
    private Pregnancy pregnancy = null!;

    [ObservableProperty]
    private ObservableCollection<PrenatalVisit> visits = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public async Task LoadVisitsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var records = await _prenatalVisitService
                .GetByPregnancyIdAsync(_pregnancyId);

            Visits.Clear();

            foreach (var visit in records)
            {
                Visits.Add(visit);
            }

            if (Visits.Count == 0)
            {
                StatusMessage = "No prenatal visits recorded yet.";
            }
        }
        catch (Exception)
        {
            StatusMessage = "Unable to load prenatal visit records.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
