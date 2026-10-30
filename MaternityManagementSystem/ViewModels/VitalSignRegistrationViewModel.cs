
using System;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class VitalSignRegistrationViewModel : ObservableObject
{
    private readonly VitalSignService _vitalSignService;
    private readonly int _prenatalVisitId;

    private bool HasIncompleteBloodPressure =>
    string.IsNullOrWhiteSpace(SystolicBloodPressureInput) !=
    string.IsNullOrWhiteSpace(DiastolicBloodPressureInput);

    public VitalSignRegistrationViewModel(
        VitalSignService vitalSignService,
        PrenatalVisit prenatalVisit)
    {
        ArgumentNullException.ThrowIfNull(vitalSignService);
        ArgumentNullException.ThrowIfNull(prenatalVisit);

        if (prenatalVisit.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(prenatalVisit));
        }

        _vitalSignService = vitalSignService;
        _prenatalVisitId = prenatalVisit.Id;

        RecordedAtUtc = DateTime.UtcNow;
    }

    // Text input properties
    [ObservableProperty]
    private string systolicBloodPressureInput = string.Empty;

    [ObservableProperty]
    private string diastolicBloodPressureInput = string.Empty;

    [ObservableProperty]
    private string heartRateInput = string.Empty;

    [ObservableProperty]
    private string respiratoryRateInput = string.Empty;

    [ObservableProperty]
    private string temperatureCelsiusInput = string.Empty;

    [ObservableProperty]
    private string oxygenSaturationInput = string.Empty;

    [ObservableProperty]
    private string weightKgInput = string.Empty;

    [ObservableProperty]
    private string fetalHeartRateInput = string.Empty;

    [ObservableProperty]
    private DateTime recordedAtUtc;

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    // Determine whether any input contains invalid numeric data.
    private bool HasInvalidInput =>
        !IsValidIntegerInput(SystolicBloodPressureInput) ||
        !IsValidIntegerInput(DiastolicBloodPressureInput) ||
        !IsValidIntegerInput(HeartRateInput) ||
        !IsValidIntegerInput(RespiratoryRateInput) ||
        !IsValidDecimalInput(TemperatureCelsiusInput) ||
        !IsValidDecimalInput(OxygenSaturationInput) ||
        !IsValidDecimalInput(WeightKgInput) ||
        !IsValidIntegerInput(FetalHeartRateInput);

    public bool CanSave =>
        !IsBusy &&
        !HasInvalidInput &&
        !HasIncompleteBloodPressure &&
        (
            !string.IsNullOrWhiteSpace(SystolicBloodPressureInput) ||
            !string.IsNullOrWhiteSpace(DiastolicBloodPressureInput) ||
            !string.IsNullOrWhiteSpace(HeartRateInput) ||
            !string.IsNullOrWhiteSpace(RespiratoryRateInput) ||
            !string.IsNullOrWhiteSpace(TemperatureCelsiusInput) ||
            !string.IsNullOrWhiteSpace(OxygenSaturationInput) ||
            !string.IsNullOrWhiteSpace(WeightKgInput) ||
            !string.IsNullOrWhiteSpace(FetalHeartRateInput)
        );

    public event Action? VitalSignSaved;

    // Refresh CanSave whenever an input changes.
    partial void OnSystolicBloodPressureInputChanged(string value)
    {
        RefreshCanSave();
    }

    partial void OnDiastolicBloodPressureInputChanged(string value)
    {
        RefreshCanSave();
    }

    partial void OnHeartRateInputChanged(string value)
    {
        RefreshCanSave();
    }

    partial void OnRespiratoryRateInputChanged(string value)
    {
        RefreshCanSave();
    }

    partial void OnTemperatureCelsiusInputChanged(string value)
    {
        RefreshCanSave();
    }

    partial void OnOxygenSaturationInputChanged(string value)
    {
        RefreshCanSave();
    }

    partial void OnWeightKgInputChanged(string value)
    {
        RefreshCanSave();
    }

    partial void OnFetalHeartRateInputChanged(string value)
    {
        RefreshCanSave();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    private void RefreshCanSave()
    {
        OnPropertyChanged(nameof(CanSave));
    }

    private static bool IsValidIntegerInput(string input)
    {
        return string.IsNullOrWhiteSpace(input) ||
               int.TryParse(
                   input,
                   NumberStyles.Integer,
                   CultureInfo.CurrentCulture,
                   out _);
    }

    private static bool IsValidDecimalInput(string input)
    {
        return string.IsNullOrWhiteSpace(input) ||
               decimal.TryParse(
                   input,
                   NumberStyles.Number,
                   CultureInfo.CurrentCulture,
                   out _);
    }

    private static int? ParseIntegerInput(string input)
    {
        return int.TryParse(
            input,
            NumberStyles.Integer,
            CultureInfo.CurrentCulture,
            out int value)
                ? value
                : null;
    }

    private static decimal? ParseDecimalInput(string input)
    {
        return decimal.TryParse(
            input,
            NumberStyles.Number,
            CultureInfo.CurrentCulture,
            out decimal value)
                ? value
                : null;
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
            var vitalSign = new VitalSign
            {
                PrenatalVisitId = _prenatalVisitId,

                SystolicBloodPressure =
                    ParseIntegerInput(SystolicBloodPressureInput),

                DiastolicBloodPressure =
                    ParseIntegerInput(DiastolicBloodPressureInput),

                HeartRate =
                    ParseIntegerInput(HeartRateInput),

                RespiratoryRate =
                    ParseIntegerInput(RespiratoryRateInput),

                TemperatureCelsius =
                    ParseDecimalInput(TemperatureCelsiusInput),

                OxygenSaturation =
                    ParseDecimalInput(OxygenSaturationInput),

                WeightKg =
                    ParseDecimalInput(WeightKgInput),

                FetalHeartRate =
                    ParseIntegerInput(FetalHeartRateInput),

                RecordedAtUtc = RecordedAtUtc,

                Notes = string.IsNullOrWhiteSpace(Notes)
                    ? null
                    : Notes.Trim()
            };

            await _vitalSignService.CreateVitalSignAsync(vitalSign);

            VitalSignSaved?.Invoke();
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception)
        {
            StatusMessage =
                "Unable to save the vital signs record. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
