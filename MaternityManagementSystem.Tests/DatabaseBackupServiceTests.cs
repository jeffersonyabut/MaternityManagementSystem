using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public class DatabaseBackupServiceTests
{
    [Fact]
    public async Task CreateBackupAsync_WhenAdmin_CreatesValidatedBackupWithData()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var destination = TemporaryDirectory.Create();
        var backupService = await CreateBackupServiceAsync(
            database.Factory,
            StaffRoles.Admin);

        await using (var context = await database.Factory.CreateDbContextAsync())
        {
            context.Patients.Add(new Patient
            {
                PatientNumber = "BACKUP-TEST-001",
                FirstName = "Backup",
                LastName = "Patient"
            });

            await context.SaveChangesAsync();
        }

        string backupPath = await backupService.CreateBackupAsync(
            destination.Path);

        Assert.True(File.Exists(backupPath));
        Assert.Matches(
            "^Golddie_Backup_\\d{8}_\\d{6}_\\d{3}_[a-f0-9]{32}\\.db$",
            System.IO.Path.GetFileName(backupPath));
        Assert.Single(Directory.GetFiles(destination.Path));

        await using (var backupContext =
            await new TestClinicDbContextFactory(backupPath)
                .CreateDbContextAsync())
        {
            var patient = await backupContext.Patients.SingleAsync();

            Assert.Equal("BACKUP-TEST-001", patient.PatientNumber);
            Assert.True(await backupContext.Database
                .CanConnectAsync());
        }

        await using var backupConnection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = backupPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());

        await backupConnection.OpenAsync();

        await using (var integrityCommand = backupConnection.CreateCommand())
        {
            integrityCommand.CommandText = "PRAGMA integrity_check;";

            Assert.Equal(
                "ok",
                await integrityCommand.ExecuteScalarAsync());
        }

        await using var foreignKeyCommand = backupConnection.CreateCommand();
        foreignKeyCommand.CommandText = "PRAGMA foreign_key_check;";

        await using var foreignKeyReader =
            await foreignKeyCommand.ExecuteReaderAsync();

        Assert.False(await foreignKeyReader.ReadAsync());
    }

    [Fact]
    public async Task CreateBackupAsync_WhenNurse_ThrowsAndCreatesNoFile()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var destination = TemporaryDirectory.Create();
        var backupService = await CreateBackupServiceAsync(
            database.Factory,
            StaffRoles.Staff);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => backupService.CreateBackupAsync(destination.Path));

        Assert.Empty(Directory.GetFiles(destination.Path));
    }

    [Fact]
    public async Task CreateBackupAsync_WhenDestinationDoesNotExist_ThrowsGenericFailure()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var destination = TemporaryDirectory.Create();
        var backupService = await CreateBackupServiceAsync(
            database.Factory,
            StaffRoles.Admin);
        string missingDirectory = System.IO.Path.Combine(
            destination.Path,
            "missing-folder");

        var exception = await Assert.ThrowsAsync<IOException>(
            () => backupService.CreateBackupAsync(missingDirectory));

        Assert.DoesNotContain(missingDirectory, exception.Message);
        Assert.Empty(Directory.GetFiles(destination.Path));
    }

    private static async Task<DatabaseBackupService> CreateBackupServiceAsync(
        TestClinicDbContextFactory contextFactory,
        string role)
    {
        int staffId;
        string securityStamp;
        string username = $"backup-test-{Guid.NewGuid():N}";

        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var staff = new Staff
            {
                Username = username,
                NormalizedUsername = UsernameNormalizer.Normalize(username),
                PasswordHash = "unused-test-hash",
                Role = role,
                IsActive = true
            };

            context.StaffMembers.Add(staff);
            await context.SaveChangesAsync();

            staffId = staff.Id;
            securityStamp = staff.SecurityStamp;
        }

        var sessionService = new SessionService();
        sessionService.StartSession(new UserSession(
            staffId,
            username,
            role,
            securityStamp));

        var authorizationService = new AuthorizationService(
            sessionService,
            new StaffRepository(contextFactory));

        return new DatabaseBackupService(
            authorizationService,
            contextFactory);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; }

        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"GolddieBackupTests_{Guid.NewGuid():N}");

            Directory.CreateDirectory(path);

            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
