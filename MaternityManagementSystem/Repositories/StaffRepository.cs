
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.Repositories;

public class StaffRepository
{
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public StaffRepository()
    {
    }

    public StaffRepository(
        IDbContextFactory<ClinicDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    private ClinicDbContext CreateDbContext()
    {
        return _contextFactory?.CreateDbContext()
            ?? new ClinicDbContext();
    }

    private static void PrepareUsername(Staff staff)
    {
        ArgumentNullException.ThrowIfNull(staff);

        if (string.IsNullOrWhiteSpace(staff.Username))
        {
            throw new ArgumentException("Username is required.");
        }

        staff.Username = staff.Username.Trim();
        staff.NormalizedUsername =
            UsernameNormalizer.Normalize(staff.Username);
    }

    public async Task<Staff?> GetByUsernameAsync(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        string normalizedUsername =
            UsernameNormalizer.Normalize(username);

        await using var db = CreateDbContext();

        return await db.StaffMembers
            .FirstOrDefaultAsync(staff =>
                staff.NormalizedUsername == normalizedUsername);
    }

    public async Task<Staff?> GetByIdAsync(int staffId)
    {
        await using var db = CreateDbContext();

        return await db.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(staff =>
                staff.Id == staffId);
    }

    public async Task<List<Staff>> GetAllAsync()
    {
        await using var db = CreateDbContext();

        return await db.StaffMembers
            .AsNoTracking()
            .OrderBy(staff => staff.Username)
            .ToListAsync();
    }

    public async Task<bool> UsernameExistsAsync(
        string username,
        int? excludedStaffId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        string normalizedUsername =
            UsernameNormalizer.Normalize(username);

        await using var db = CreateDbContext();

        return await db.StaffMembers.AnyAsync(staff =>
            staff.NormalizedUsername == normalizedUsername &&
            (!excludedStaffId.HasValue ||
             staff.Id != excludedStaffId.Value));
    }

    public async Task AddAsync(Staff staff)
    {
        PrepareUsername(staff);

        await using var db = CreateDbContext();

        db.StaffMembers.Add(staff);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (IsUsernameUniqueConstraintViolation(ex))
        {
            throw new InvalidOperationException(
                "This username is already taken.",
                ex);
        }
    }

    public async Task AddWithAuditAsync(
        Staff staff,
        AuditLog auditLog)
    {
        PrepareUsername(staff);
        ArgumentNullException.ThrowIfNull(auditLog);

        await using var db = CreateDbContext();

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
        catch (DbUpdateException ex)
            when (IsUsernameUniqueConstraintViolation(ex))
        {
            await transaction.RollbackAsync();

            throw new InvalidOperationException(
                "This username is already taken.",
                ex);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentNullException.ThrowIfNull(auditLog);

        string displayUsername = username.Trim();

        string normalizedUsername =
            UsernameNormalizer.Normalize(displayUsername);

        await using var db = CreateDbContext();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
            Staff? staff = await db.StaffMembers
                .FirstOrDefaultAsync(staff =>
                    staff.Id == staffId);

            if (staff is null)
            {
                return false;
            }

            if (staff.IsActive &&
                staff.Role == StaffRoles.Admin &&
                role != StaffRoles.Admin)
            {
                int activeAdminCount =
                    await db.StaffMembers.CountAsync(member =>
                        member.IsActive &&
                        member.Role == StaffRoles.Admin);

                if (activeAdminCount <= 1)
                {
                    throw new InvalidOperationException(
                        "You cannot demote the last active administrator.");
                }
            }

            bool identityChanged =
                staff.NormalizedUsername != normalizedUsername ||
                staff.Role != role;

            staff.Username = displayUsername;
            staff.NormalizedUsername = normalizedUsername;
            staff.Role = role;

            if (identityChanged)
            {
                staff.SecurityStamp = Guid.NewGuid().ToString("N");
            }

            auditLog.EntityId = staff.Id;

            db.AuditLogs.Add(auditLog);

            await db.SaveChangesAsync();

            await transaction.CommitAsync();

            return true;
        }
        catch (DbUpdateException ex)
            when (IsUsernameUniqueConstraintViolation(ex))
        {
            await transaction.RollbackAsync();

            throw new InvalidOperationException(
                "This username is already taken.",
                ex);
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
        ArgumentNullException.ThrowIfNull(auditLog);

        await using var db = CreateDbContext();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
            Staff? staff = await db.StaffMembers
                .FirstOrDefaultAsync(staff =>
                    staff.Id == staffId);

            if (staff is null)
            {
                return false;
            }

            if (!isActive &&
                staff.IsActive &&
                staff.Role == StaffRoles.Admin)
            {
                int activeAdminCount =
                    await db.StaffMembers.CountAsync(member =>
                        member.IsActive &&
                        member.Role == StaffRoles.Admin);

                if (activeAdminCount <= 1)
                {
                    throw new InvalidOperationException(
                        "You cannot deactivate the last active administrator.");
                }
            }

            if (staff.IsActive != isActive)
            {
                staff.IsActive = isActive;
                staff.SecurityStamp = Guid.NewGuid().ToString("N");
            }

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
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentNullException.ThrowIfNull(auditLog);

        await using var db = CreateDbContext();

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
            Staff? staff = await db.StaffMembers
                .FirstOrDefaultAsync(staff =>
                    staff.Id == staffId);

            if (staff is null)
            {
                return false;
            }

            staff.PasswordHash = passwordHash;
            staff.SecurityStamp = Guid.NewGuid().ToString("N");

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
        await using var db = CreateDbContext();

        return await db.StaffMembers.AnyAsync();
    }

    private static bool IsUsernameUniqueConstraintViolation(
        Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is SqliteException sqliteException &&
                sqliteException.SqliteErrorCode == 19 &&
                sqliteException.SqliteExtendedErrorCode == 2067 &&
                sqliteException.Message.Contains(
                    "StaffMembers.NormalizedUsername",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
