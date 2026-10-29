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

    // Clinical notes
    public string? Notes { get; set; }

    // Record timestamps
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
