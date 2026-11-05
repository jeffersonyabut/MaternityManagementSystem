
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

    public async Task<Staff?> GetByIdAsync(int staffId)
    {
        await using var db = new ClinicDbContext();

        return await db.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(staff =>
                staff.Id == staffId);
    }

    public async Task<List<Staff>> GetAllAsync()
    {
        await using var db = new ClinicDbContext();

        return await db.StaffMembers
            .AsNoTracking()
            .OrderBy(staff => staff.Username)
            .ToListAsync();
    }

    public async Task<bool> UsernameExistsAsync(
        string username,
        int? excludedStaffId = null)
    {
        await using var db = new ClinicDbContext();

        string normalizedUsername = username.Trim().ToLower();

        return await db.StaffMembers.AnyAsync(staff =>
            staff.Username.ToLower() == normalizedUsername &&
            (!excludedStaffId.HasValue ||
             staff.Id != excludedStaffId.Value));
    }

    public async Task AddAsync(Staff staff)
    {
        await using var db = new ClinicDbContext();

        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();
    }

    public async Task AddWithAuditAsync(
        Staff staff,
        AuditLog auditLog)
    {
        await using var db = new ClinicDbContext();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
            db.StaffMembers.Add(staff);

            await db.SaveChangesAsync();

            auditLog.EntityId = staff.Id;

            db.AuditLogs.Add(auditLog);

            await db.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> UpdateWithAuditAsync(
    int staffId,
    string username,
    string role,
    AuditLog auditLog)
    {
        await using var db = new ClinicDbContext();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
            Staff? staff = await db.StaffMembers
                .FirstOrDefaultAsync(staff => staff.Id == staffId);

            if (staff is null)
            {
                return false;
            }


            if (staff.IsActive &&
                staff.Role == StaffRoles.Admin &&
                role != StaffRoles.Admin)
            {
                int activeAdminCount = await db.StaffMembers.CountAsync(
                    member =>
                        member.IsActive &&
                        member.Role == StaffRoles.Admin);

                if (activeAdminCount <= 1)
                {
                    throw new InvalidOperationException(
                        "You cannot demote the last active administrator.");
                }
            }

            staff.Username = username;
            staff.Role = role;

            auditLog.EntityId = staff.Id;

            db.AuditLogs.Add(auditLog);

            await db.SaveChangesAsync();

            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }


    public async Task<bool> UpdateStatusWithAuditAsync(
        int staffId,
        bool isActive,
        AuditLog auditLog)
    {
        await using var db = new ClinicDbContext();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
            Staff? staff = await db.StaffMembers
                .FirstOrDefaultAsync(staff => staff.Id == staffId);

            if (staff is null)
            {
                return false;
            }

            // Prevent deactivating the last active administrator.
            if (!isActive &&
                staff.IsActive &&
                staff.Role == StaffRoles.Admin)
            {
                int activeAdminCount = await db.StaffMembers.CountAsync(
                    member =>
                        member.IsActive &&
                        member.Role == StaffRoles.Admin);

                if (activeAdminCount <= 1)
                {
                    throw new InvalidOperationException(
                        "You cannot deactivate the last active administrator.");
                }
            }

            staff.IsActive = isActive;

            auditLog.EntityId = staff.Id;

            db.AuditLogs.Add(auditLog);

            await db.SaveChangesAsync();

            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }


    public async Task<bool> ResetPasswordWithAuditAsync(
    int staffId,
    string passwordHash,
    AuditLog auditLog)
    {
        await using var db = new ClinicDbContext();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
            Staff? staff = await db.StaffMembers
                .FirstOrDefaultAsync(staff => staff.Id == staffId);

            if (staff is null)
            {
                return false;
            }

            staff.PasswordHash = passwordHash;

            auditLog.EntityId = staff.Id;

            db.AuditLogs.Add(auditLog);

            await db.SaveChangesAsync();

            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }


    public async Task<bool> HasAnyStaffAsync()
    {
        await using var db = new ClinicDbContext();

        return await db.StaffMembers.AnyAsync();
    }
}
