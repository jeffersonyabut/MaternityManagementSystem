
namespace MaternityManagementSystem.Models;

public static class SystemSettingDefaults
{
    public static readonly IReadOnlyDictionary<string, string> Values =
        new Dictionary<string, string>
        {
            // Clinic Profile
            [SystemSettingKeys.Clinic.Name] = "My Maternity Clinic",
            [SystemSettingKeys.Clinic.LogoPath] = string.Empty,
            [SystemSettingKeys.Clinic.Address] = string.Empty,
            [SystemSettingKeys.Clinic.ContactNumber] = string.Empty,
            [SystemSettingKeys.Clinic.Email] = string.Empty,

            // Appointment Settings
            [SystemSettingKeys.Appointments.DefaultDuration] = "30",

            // Notification Settings
            [SystemSettingKeys.Notifications.SmsEnabled] = "false",
            [SystemSettingKeys.Notifications.AppointmentReminders] = "true",

            // Regional Settings
            [SystemSettingKeys.Regional.TimeZone] = "Singapore Standard Time",
            [SystemSettingKeys.Regional.DateFormat] = "dd/MM/yyyy"
        };
}
