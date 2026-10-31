using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.ViewModels;

public class PregnancyCardDisplayViewModel
{
    public Pregnancy Pregnancy { get; set; } = null!;

    public bool HasHistory { get; set; }
}
