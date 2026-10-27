using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Repositories;

public class StaffRepository
{
    public async Task<Staff?> GetByUsernameAsync(string username)
    {
        await using var db = new ClinicDbContext();

        return await db.StaffMembers
            .FirstOrDefaultAsync(staff =>
                staff.Username == username);
    }

    public async Task AddAsync(Staff staff)
    {
        await using var db = new ClinicDbContext();

        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();
    }

    public async Task<bool> HasAnyStaffAsync()
    {
        await using var db = new ClinicDbContext();

        return await db.StaffMembers.AnyAsync();
    }

}
