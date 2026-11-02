namespace MaternityManagementSystem.Models.Reports;

public class ObstetricHistorySummary
{
    public int Gravida { get; set; }

    public int Term { get; set; }

    public int Preterm { get; set; }

    public int Abortion { get; set; }

    public int UnclassifiedPregnancies { get; set; }

    public string LivingChildrenDisplay { get; set; } =
        "Not recorded";
}
