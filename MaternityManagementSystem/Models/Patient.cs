
namespace MaternityManagementSystem.Models;

public class Patient
{
    public int Id { get; set; }

    // Patient identification
    public string PatientNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;

    // Personal information
    public DateOnly? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public string? CivilStatus { get; set; }

    // Contact information
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    // Emergency contact
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactNumber { get; set; }

    // SMS appointment reminder consent
    public bool HasSmsReminderConsent { get; set; }
    public int SmsReminderConsentGeneration { get; set; }
    public DateTime? SmsReminderConsentRecordedAtUtc { get; set; }
    public int? SmsReminderConsentRecordedByStaffId { get; set; }
    public Staff? SmsReminderConsentRecordedByStaff { get; set; }

    // Audit information
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
