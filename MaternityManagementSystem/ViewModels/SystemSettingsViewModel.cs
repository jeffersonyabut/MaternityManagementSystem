
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class SystemSettingsViewModel : ObservableObject
{
    private readonly SystemSettingService _systemSettingService;
    private readonly RegionalSettingsService _regionalSettingsService;
    private readonly DatabaseBackupService? _databaseBackupService;

    private readonly Dictionary<string, string> _originalValues =
        new(StringComparer.OrdinalIgnoreCase);

    public SystemSettingsViewModel(
        SystemSettingService systemSettingService,
        RegionalSettingsService regionalSettingsService,
        DatabaseBackupService? databaseBackupService = null)
    {
        ArgumentNullException.ThrowIfNull(systemSettingService);
        ArgumentNullException.ThrowIfNull(regionalSettingsService);

        _systemSettingService = systemSettingService;
        _regionalSettingsService = regionalSettingsService;
        _databaseBackupService = databaseBackupService;
    }

    [ObservableProperty]
    private bool isCreatingBackup;

    [ObservableProperty]
    private string backupStatusMessage = string.Empty;

    public async Task CreateDatabaseBackupAsync(
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        if (IsCreatingBackup)
        {
            return;
        }

        if (_databaseBackupService is null)
        {
            BackupStatusMessage =
                "Database backup is not available in this context.";
            return;
        }

        IsCreatingBackup = true;
        BackupStatusMessage = "Creating and validating database backup...";

        try
        {
            string backupPath = await _databaseBackupService.CreateBackupAsync(
                destinationDirectory,
                cancellationToken);

            BackupStatusMessage =
                $"Backup completed: {System.IO.Path.GetFileName(backupPath)}";
        }
        catch (UnauthorizedAccessException)
        {
            BackupStatusMessage =
                "Your session is not authorized to create database backups.";
        }
        catch (OperationCanceledException)
        {
            BackupStatusMessage = "Database backup was cancelled.";
        }
        catch (Exception)
        {
            BackupStatusMessage =
                "Backup failed. Check the selected folder and available disk space.";
        }
        finally
        {
            IsCreatingBackup = false;
        }
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

            var values = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var setting in settings)
            {
                string? knownKey = null;

                foreach (var defaultSetting in SystemSettingDefaults.Values)
                {
                    if (string.Equals(
                            defaultSetting.Key,
                            setting.Key,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        knownKey = defaultSetting.Key;
                        break;
                    }
                }

                if (knownKey is not null)
                {
                    values.TryAdd(
                        knownKey,
                        setting.Value ?? string.Empty);
                }
            }

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
        catch (UnauthorizedAccessException)
        {
            // Session expiration is handled by DashboardWindow.
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
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int duration) ||
                duration < SystemSettingService.MinimumAppointmentDurationMinutes ||
                duration > SystemSettingService.MaximumAppointmentDurationMinutes)
            {
                StatusMessage =
                    $"Default appointment duration must be between " +
                    $"{SystemSettingService.MinimumAppointmentDurationMinutes} and " +
                    $"{SystemSettingService.MaximumAppointmentDurationMinutes} minutes.";

                return;
            }

            var updatedValues = new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                [SystemSettingKeys.Clinic.Name] = ClinicName.Trim(),
                [SystemSettingKeys.Clinic.LogoPath] = ClinicLogoPath.Trim(),
                [SystemSettingKeys.Clinic.Address] = ClinicAddress.Trim(),
                [SystemSettingKeys.Clinic.ContactNumber] = ClinicContactNumber.Trim(),
                [SystemSettingKeys.Clinic.Email] = ClinicEmail.Trim(),

                [SystemSettingKeys.Appointments.DefaultDuration] =
                    duration.ToString(CultureInfo.InvariantCulture),

                [SystemSettingKeys.Notifications.SmsEnabled] =
                    IsSmsEnabled.ToString().ToLowerInvariant(),

                [SystemSettingKeys.Notifications.AppointmentReminders] =
                    AreAppointmentRemindersEnabled.ToString().ToLowerInvariant(),

                [SystemSettingKeys.Regional.TimeZone] = TimeZone.Trim(),
                [SystemSettingKeys.Regional.DateFormat] = DateFormat.Trim()
            };

            bool regionalSettingsChanged =
                HasSettingChanged(
                    updatedValues,
                    SystemSettingKeys.Regional.TimeZone) ||
                HasSettingChanged(
                    updatedValues,
                    SystemSettingKeys.Regional.DateFormat);

            var changedValues = new Dictionary<string, string>(
                StringComparer.Ordinal);

            foreach (var setting in updatedValues)
            {
                if (HasSettingChanged(updatedValues, setting.Key))
                {
                    changedValues.Add(setting.Key, setting.Value);
                }
            }

            // Save every changed setting in one database transaction.
            await _systemSettingService.SaveBatchAsync(changedValues);

            // Update the local baseline after the database save succeeds.
            foreach (var setting in updatedValues)
            {
                _originalValues[setting.Key] = setting.Value;
            }

            // Apply regional settings only after the database transaction succeeds.
            if (regionalSettingsChanged)
            {
                try
                {
                    await _regionalSettingsService.RefreshAsync();
                }
                catch (Exception)
                {
                    StatusMessage =
                        "Settings were saved successfully, but regional settings " +
                        "could not be refreshed. Please restart the application.";

                    return;
                }
            }

            StatusMessage = "System settings saved successfully.";
        }
        catch (UnauthorizedAccessException)
        {
            // Session expiration is handled by DashboardWindow.
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception)
        {
            StatusMessage =
                "Unable to save system settings. No changes were committed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool HasSettingChanged(
        IReadOnlyDictionary<string, string> updatedValues,
        string key)
    {
        return updatedValues.TryGetValue(key, out var updatedValue) &&
               (!_originalValues.TryGetValue(key, out var originalValue) ||
                !string.Equals(
                    originalValue,
                    updatedValue,
                    StringComparison.Ordinal));
    }
}
