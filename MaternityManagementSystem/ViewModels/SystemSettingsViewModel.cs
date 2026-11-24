
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class SystemSettingsViewModel : ObservableObject
{
    private readonly SystemSettingService _systemSettingService;

    private readonly Dictionary<string, string> _originalValues =
        new(StringComparer.OrdinalIgnoreCase);

    public SystemSettingsViewModel(
        SystemSettingService systemSettingService)
    {
        ArgumentNullException.ThrowIfNull(systemSettingService);

        _systemSettingService = systemSettingService;
    }

    // CLINIC PROFILE

    [ObservableProperty]
    private string clinicName = string.Empty;

    [ObservableProperty]
    private string clinicLogoPath = string.Empty;

    [ObservableProperty]
    private string clinicAddress = string.Empty;

    [ObservableProperty]
    private string clinicContactNumber = string.Empty;

    [ObservableProperty]
    private string clinicEmail = string.Empty;

    // APPOINTMENT SETTINGS

    [ObservableProperty]
    private string defaultAppointmentDuration = "30";

    // NOTIFICATION SETTINGS

    [ObservableProperty]
    private bool isSmsEnabled;

    [ObservableProperty]
    private bool areAppointmentRemindersEnabled = true;

    // REGIONAL SETTINGS

    [ObservableProperty]
    private string timeZone = "Singapore Standard Time";

    [ObservableProperty]
    private string dateFormat = "dd/MM/yyyy";

    // GENERAL STATE

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    // LOAD SETTINGS

    public async Task LoadSettingsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var settings =
                await _systemSettingService.GetAllAsync();

            var values = settings.ToDictionary(
                setting => setting.Key,
                setting => setting.Value,
                StringComparer.OrdinalIgnoreCase);

            _originalValues.Clear();

            foreach (var setting in SystemSettingDefaults.Values)
            {
                _originalValues[setting.Key] =
                    values.TryGetValue(setting.Key, out var value)
                        ? value
                        : setting.Value;
            }

            LoadPropertyValues(_originalValues);

            StatusMessage = "System settings loaded successfully.";
        }
        catch (Exception)
        {
            StatusMessage = "Unable to load system settings.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadPropertyValues(
        IReadOnlyDictionary<string, string> values)
    {
        ClinicName = GetValue(
            values,
            SystemSettingKeys.Clinic.Name);

        ClinicLogoPath = GetValue(
            values,
            SystemSettingKeys.Clinic.LogoPath);

        ClinicAddress = GetValue(
            values,
            SystemSettingKeys.Clinic.Address);

        ClinicContactNumber = GetValue(
            values,
            SystemSettingKeys.Clinic.ContactNumber);

        ClinicEmail = GetValue(
            values,
            SystemSettingKeys.Clinic.Email);

        DefaultAppointmentDuration = GetValue(
            values,
            SystemSettingKeys.Appointments.DefaultDuration);

        IsSmsEnabled = bool.TryParse(
            GetValue(values, SystemSettingKeys.Notifications.SmsEnabled),
            out bool smsEnabled) && smsEnabled;

        AreAppointmentRemindersEnabled = bool.TryParse(
            GetValue(values, SystemSettingKeys.Notifications.AppointmentReminders),
            out bool remindersEnabled) && remindersEnabled;

        TimeZone = GetValue(
            values,
            SystemSettingKeys.Regional.TimeZone);

        DateFormat = GetValue(
            values,
            SystemSettingKeys.Regional.DateFormat);
    }

    private static string GetValue(
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        return values.TryGetValue(key, out var value)
            ? value
            : string.Empty;
    }

    // SAVE SETTINGS

    public async Task SaveSettingsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            if (!int.TryParse(
                DefaultAppointmentDuration,
                out int duration) ||
                duration <= 0)
            {
                StatusMessage =
                    "Default appointment duration must be a positive number.";

                return;
            }

            var updatedValues = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                [SystemSettingKeys.Clinic.Name] = ClinicName.Trim(),
                [SystemSettingKeys.Clinic.LogoPath] = ClinicLogoPath.Trim(),
                [SystemSettingKeys.Clinic.Address] = ClinicAddress.Trim(),
                [SystemSettingKeys.Clinic.ContactNumber] = ClinicContactNumber.Trim(),
                [SystemSettingKeys.Clinic.Email] = ClinicEmail.Trim(),

                [SystemSettingKeys.Appointments.DefaultDuration] =
                    duration.ToString(),

                [SystemSettingKeys.Notifications.SmsEnabled] =
                    IsSmsEnabled.ToString().ToLowerInvariant(),

                [SystemSettingKeys.Notifications.AppointmentReminders] =
                    AreAppointmentRemindersEnabled.ToString().ToLowerInvariant(),

                [SystemSettingKeys.Regional.TimeZone] = TimeZone.Trim(),
                [SystemSettingKeys.Regional.DateFormat] = DateFormat.Trim()
            };

            foreach (var setting in updatedValues)
            {
                if (_originalValues.TryGetValue(
                        setting.Key,
                        out var originalValue) &&
                    string.Equals(
                        originalValue,
                        setting.Value,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                await _systemSettingService.SaveAsync(
                    setting.Key,
                    setting.Value);
            }

            foreach (var setting in updatedValues)
            {
                _originalValues[setting.Key] = setting.Value;
            }

            StatusMessage = "System settings saved successfully.";
        }
        catch (Exception)
        {
            StatusMessage = "Unable to save system settings.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
