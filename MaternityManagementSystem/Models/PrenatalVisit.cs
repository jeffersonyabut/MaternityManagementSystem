namespace MaternityManagementSystem.Models;

public class PrenatalVisit
{
    public int Id { get; set; }

    public int PregnancyId { get; set; }

    public Pregnancy Pregnancy { get; set; } = null!;

    public DateOnly VisitDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<VitalSign> VitalSigns { get; set; } = new List<VitalSign>();
}
