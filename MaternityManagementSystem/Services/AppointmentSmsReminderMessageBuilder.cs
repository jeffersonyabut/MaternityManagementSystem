using System.Globalization;

namespace MaternityManagementSystem.Services;

/// <summary>Builds the intentionally generic, non-clinical reminder message.</summary>
public sealed class AppointmentSmsReminderMessageBuilder
{
    public string Build(
        string clinicName,
        DateOnly appointmentDate,
        TimeOnly appointmentTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clinicName);

        string date = appointmentDate.ToString(
            "MMMM d, yyyy",
            CultureInfo.InvariantCulture);
        string time = appointmentTime.ToString(
            "h:mm tt",
            CultureInfo.InvariantCulture);

        return $"Reminder: You have an upcoming appointment at {clinicName.Trim()} " +
               $"on {date} at {time}. Please contact the clinic if you need to reschedule.";
    }
}
