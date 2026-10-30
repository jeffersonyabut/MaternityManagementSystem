
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class VitalSignsViewModel : ObservableObject
{
    private readonly VitalSignService _vitalSignService;
    private readonly int _prenatalVisitId;

    public VitalSignsViewModel(
        VitalSignService vitalSignService,
        PrenatalVisit prenatalVisit)
    {
        ArgumentNullException.ThrowIfNull(vitalSignService);
        ArgumentNullException.ThrowIfNull(prenatalVisit);

        if (prenatalVisit.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(prenatalVisit));
        }

        _vitalSignService = vitalSignService;
        _prenatalVisitId = prenatalVisit.Id;

        PrenatalVisit = prenatalVisit;
    }

    [ObservableProperty]
    private PrenatalVisit prenatalVisit = null!;

    [ObservableProperty]
    private ObservableCollection<VitalSign> vitalSigns = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public async Task LoadVitalSignsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var records = await _vitalSignService
                .GetByPrenatalVisitIdAsync(_prenatalVisitId);

            VitalSigns.Clear();

            foreach (var record in records)
            {
                VitalSigns.Add(record);
            }

            if (VitalSigns.Count == 0)
            {
                StatusMessage = "No vital signs recorded for this visit yet.";
            }
        }
        catch (Exception)
        {
            StatusMessage = "Unable to load vital signs records.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
