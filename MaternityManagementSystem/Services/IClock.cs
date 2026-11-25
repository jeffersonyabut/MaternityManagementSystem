namespace MaternityManagementSystem.Services;

public interface IClock
{
    DateTime UtcNow { get; }
}
