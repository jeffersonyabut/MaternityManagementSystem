
using System;

namespace MaternityManagementSystem.Models;

public class Pregnancy
{
    public int Id { get; set; }

    // Patient relationship
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    // Pregnancy details
    public string Status { get; set; } = "Current";

    public DateOnly? LastMenstrualPeriod { get; set; }

    public DateOnly? EstimatedDueDate { get; set; }

    public DateOnly? PregnancyEndDate { get; set; }

    // Pregnancy outcome
    public string? PregnancyOutcome { get; set; }

    // Delivery information
    public string? DeliveryMethod { get; set; }

    public int? GestationalAgeAtEndWeeks { get; set; }

    public int? GestationalAgeAtEndDays { get; set; }

    public int? NumberOfInfants { get; set; }

    public int? LivingChildrenCount { get; set; }

    // Pregnancy or delivery complications
    public string? Complications { get; set; }

    // Clinical notes
    public string? Notes { get; set; }

    // Record timestamps
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
