namespace MaternityManagementSystem.Models;

public class VitalSign
{
    public int Id { get; set; }

    public int PrenatalVisitId { get; set; }

    public PrenatalVisit PrenatalVisit { get; set; } = null!;

    public int? SystolicBloodPressure { get; set; }

    public int? DiastolicBloodPressure { get; set; }

    public int? HeartRate { get; set; }

    public int? RespiratoryRate { get; set; }

    public decimal? TemperatureCelsius { get; set; }

    public decimal? OxygenSaturation { get; set; }

    public decimal? WeightKg { get; set; }

    public int? FetalHeartRate { get; set; }

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public string? Notes { get; set; }

    public bool IsArchived { get; set; } = false;

    public DateTime? ArchivedAtUtc { get; set; }

    public string? ArchiveReason { get; set; }

}
