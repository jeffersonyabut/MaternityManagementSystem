
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class VitalSignEditViewModel : ObservableObject
{
    private readonly VitalSignService _vitalSignService;
    private readonly VitalSign _originalVitalSign;

    public VitalSignEditViewModel(
        VitalSignService vitalSignService,
        VitalSign vitalSign)
    {
        ArgumentNullException.ThrowIfNull(vitalSignService);
        ArgumentNullException.ThrowIfNull(vitalSign);

        if (vitalSign.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vitalSign));
        }

        _vitalSignService = vitalSignService;
        _originalVitalSign = vitalSign;

        SystolicBloodPressure = vitalSign.SystolicBloodPressure;
        DiastolicBloodPressure = vitalSign.DiastolicBloodPressure;
        HeartRate = vitalSign.HeartRate;
        RespiratoryRate = vitalSign.RespiratoryRate;
        TemperatureCelsius = vitalSign.TemperatureCelsius;
        OxygenSaturation = vitalSign.OxygenSaturation;
        WeightKg = vitalSign.WeightKg;
        FetalHeartRate = vitalSign.FetalHeartRate;
        RecordedAtUtc = vitalSign.RecordedAtUtc;
        Notes = vitalSign.Notes ?? string.Empty;
    }

    [ObservableProperty]
    private int? systolicBloodPressure;

    [ObservableProperty]
    private int? diastolicBloodPressure;

    [ObservableProperty]
    private int? heartRate;

    [ObservableProperty]
    private int? respiratoryRate;

    [ObservableProperty]
    private decimal? temperatureCelsius;

    [ObservableProperty]
    private decimal? oxygenSaturation;

    [ObservableProperty]
    private decimal? weightKg;

    [ObservableProperty]
    private int? fetalHeartRate;

    [ObservableProperty]
    private DateTime recordedAtUtc;

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public bool CanSave => !IsBusy &&
        (
            SystolicBloodPressure.HasValue ||
            DiastolicBloodPressure.HasValue ||
            HeartRate.HasValue ||
            RespiratoryRate.HasValue ||
            TemperatureCelsius.HasValue ||
            OxygenSaturation.HasValue ||
            WeightKg.HasValue ||
            FetalHeartRate.HasValue
        );

    public event Action? VitalSignUpdated;

    partial void OnSystolicBloodPressureChanged(int? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnDiastolicBloodPressureChanged(int? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnHeartRateChanged(int? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnRespiratoryRateChanged(int? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnTemperatureCelsiusChanged(decimal? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnOxygenSaturationChanged(decimal? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnWeightKgChanged(decimal? value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnFetalHeartRateChanged(int? value)
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
            var updatedVitalSign = new VitalSign
            {
                Id = _originalVitalSign.Id,
                PrenatalVisitId = _originalVitalSign.PrenatalVisitId,

                SystolicBloodPressure = SystolicBloodPressure,
                DiastolicBloodPressure = DiastolicBloodPressure,
                HeartRate = HeartRate,
                RespiratoryRate = RespiratoryRate,
                TemperatureCelsius = TemperatureCelsius,
                OxygenSaturation = OxygenSaturation,
                WeightKg = WeightKg,
                FetalHeartRate = FetalHeartRate,

                RecordedAtUtc = RecordedAtUtc,
                Notes = string.IsNullOrWhiteSpace(Notes)
                    ? null
                    : Notes.Trim()
            };

            await _vitalSignService.UpdateVitalSignAsync(
                updatedVitalSign);

            VitalSignUpdated?.Invoke();
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception)
        {
            StatusMessage =
                "Unable to update the vital signs record. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
