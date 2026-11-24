
using System.Globalization;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Services;

public sealed class RegionalSettingsService
{
    private readonly SystemSettingRepository _repository;

    private TimeZoneInfo _timeZone = GetDefaultTimeZone();

    public RegionalSettingsService(
        SystemSettingRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        _repository = repository;
    }

    public string TimeZoneId => _timeZone.Id;

    public string DateFormat { get; private set; } =
        "dd/MM/yyyy";

    public async Task RefreshAsync()
    {
        // Reset to defaults before loading saved settings.
        _timeZone = GetDefaultTimeZone();
        DateFormat = "dd/MM/yyyy";

        var settings = await _repository.GetAllAsync();

        var timeZoneId = settings.FirstOrDefault(
            setting => setting.Key ==
                SystemSettingKeys.Regional.TimeZone)?.Value;

        var dateFormat = settings.FirstOrDefault(
            setting => setting.Key ==
                SystemSettingKeys.Regional.DateFormat)?.Value;

        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                _timeZone =
                    TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                _timeZone = GetDefaultTimeZone();
            }
            catch (InvalidTimeZoneException)
            {
                _timeZone = GetDefaultTimeZone();
            }
        }

        if (!string.IsNullOrWhiteSpace(dateFormat))
        {
            try
            {
                _ = DateTime.Now.ToString(
                    dateFormat,
                    CultureInfo.InvariantCulture);

                DateFormat = dateFormat;
            }
            catch (FormatException)
            {
                DateFormat = "dd/MM/yyyy";
            }
        }
    }

    // UTC TO CONFIGURED LOCAL TIME

    public DateTime ConvertUtcToLocal(DateTime utcDateTime)
    {
        DateTime utc = utcDateTime.Kind == DateTimeKind.Utc
            ? utcDateTime
            : DateTime.SpecifyKind(
                utcDateTime,
                DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(
            utc,
            _timeZone);
    }

    // CONFIGURED LOCAL TIME TO UTC

    public DateTime ConvertLocalToUtc(DateTime localDateTime)
    {
        DateTime local = DateTime.SpecifyKind(
            localDateTime,
            DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(
            local,
            _timeZone);
    }

    // DATE FORMATTING

    public string FormatDate(DateOnly date)
    {
        return date.ToString(
            DateFormat,
            CultureInfo.InvariantCulture);
    }

    public string FormatDateTime(DateTime utcDateTime)
    {
        return ConvertUtcToLocal(utcDateTime).ToString(
            $"{DateFormat} hh:mm tt",
            CultureInfo.InvariantCulture);
    }

    private static TimeZoneInfo GetDefaultTimeZone()
    {
        return TimeZoneInfo.FindSystemTimeZoneById(
            "Singapore Standard Time");
    }
}
