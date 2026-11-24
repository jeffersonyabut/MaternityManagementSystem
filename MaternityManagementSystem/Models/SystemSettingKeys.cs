
namespace MaternityManagementSystem.Models;

public static class SystemSettingKeys
{
    public static class Clinic
    {
        public const string Name = "Clinic.Name";
        public const string LogoPath = "Clinic.LogoPath";
        public const string Address = "Clinic.Address";
        public const string ContactNumber = "Clinic.ContactNumber";
        public const string Email = "Clinic.Email";
    }

    public static class Appointments
    {
        public const string DefaultDuration = "Appointments.DefaultDuration";
    }

    public static class Notifications
    {
        public const string SmsEnabled = "Notifications.SmsEnabled";
        public const string AppointmentReminders = "Notifications.AppointmentReminders";
    }

    public static class Regional
    {
        public const string TimeZone = "Regional.TimeZone";
        public const string DateFormat = "Regional.DateFormat";
    }
}
