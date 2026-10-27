
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;

namespace MaternityManagementSystem.Services;

public class AuthenticationService
{
    private readonly StaffRepository _staffRepository;

    public AuthenticationService(StaffRepository staffRepository)
    {
        _staffRepository = staffRepository;
    }

    public async Task<Staff?> AuthenticateAsync(
        string username,
        string password)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrEmpty(password))
        {
            return null;
        }

        Staff? staff = await _staffRepository.GetByUsernameAsync(
            username.Trim());

        if (staff is null)
        {
            return null;
        }

        if (!staff.IsActive)
        {
            return null;
        }

        bool isPasswordValid = PasswordHasher.VerifyPassword(
            password,
            staff.PasswordHash);

        if (!isPasswordValid)
        {
            return null;
        }

        return staff;
    }
}
