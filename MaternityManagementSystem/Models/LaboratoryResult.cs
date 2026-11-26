
using System;

namespace MaternityManagementSystem.Models;

public class LaboratoryResult
{
    public int Id { get; set; }

    // Patient relationship
    public int PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    // Optional prenatal visit relationship
    public int? PrenatalVisitId { get; set; }

    public PrenatalVisit? PrenatalVisit { get; set; }

    // Laboratory test information
    public string TestName { get; set; } = string.Empty;

    // Null status is reserved for legacy rows whose blank result does not
    // provide enough information to infer a lifecycle state.
    public LaboratoryResultStatus? Status { get; set; } =
        LaboratoryResultStatus.Resulted;

    public DateOnly? RequestedDate { get; set; } =
        DateOnly.FromDateTime(DateTime.Today);

    public DateOnly? TestDate { get; set; } =
        DateOnly.FromDateTime(DateTime.Today);

    // Laboratory result
    public string? Result { get; set; } = string.Empty;

    public string? Unit { get; set; }

    public string? ReferenceRange { get; set; }

    // Additional information
    public string? Notes { get; set; }

    // Record timestamps
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    // Archive information
    public bool IsArchived { get; set; } = false;

    public DateTime? ArchivedAtUtc { get; set; }

    public string? ArchiveReason { get; set; }
}
