using System.Text;
using Microsoft.Data.Sqlite;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;

namespace MaternityManagementSystem.Tests;

public class UniSmsCredentialServiceTests
{
    [Fact]
    public async Task SaveApiKeyAsync_StoresEncryptedKeyThatAdminCanRetrieve()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var directory = TemporaryDirectory.Create();
        const string apiKey = "unis-mock-test-key-9f2c3a";
        string credentialPath = Path.Combine(
            directory.Path,
            "unisms-api-key.dat");
        var service = await CreateServiceAsync(
            database,
            StaffRoles.Admin,
            credentialPath);

        await service.SaveApiKeyAsync(apiKey);

        Assert.True(File.Exists(credentialPath));
        Assert.True(await service.IsConfiguredAsync());
        Assert.Equal(apiKey, await service.GetApiKeyAsync());

        byte[] encryptedBytes = await File.ReadAllBytesAsync(credentialPath);
        string storedContent = Encoding.Latin1.GetString(encryptedBytes);

        Assert.False(storedContent.Contains(apiKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RemoveApiKeyAsync_DeletesStoredCredential()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var directory = TemporaryDirectory.Create();
        string credentialPath = Path.Combine(
            directory.Path,
            "unisms-api-key.dat");
        var service = await CreateServiceAsync(
            database,
            StaffRoles.Admin,
            credentialPath);

        await service.SaveApiKeyAsync("temporary-test-key");
        await service.RemoveApiKeyAsync();

        Assert.False(File.Exists(credentialPath));
        Assert.False(await service.IsConfiguredAsync());
        Assert.Null(await service.GetApiKeyAsync());
    }

    [Fact]
    public async Task CredentialOperations_WhenUserIsNotAdmin_AreRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var directory = TemporaryDirectory.Create();
        string credentialPath = Path.Combine(
            directory.Path,
            "unisms-api-key.dat");
        var service = await CreateServiceAsync(
            database,
            StaffRoles.Staff,
            credentialPath);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SaveApiKeyAsync("nurse-test-key"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetApiKeyAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.IsConfiguredAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RemoveApiKeyAsync());

        Assert.False(File.Exists(credentialPath));
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenEncryptedFileIsInvalid_ThrowsGenericError()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var directory = TemporaryDirectory.Create();
        string credentialPath = Path.Combine(
            directory.Path,
            "unisms-api-key.dat");
        var service = await CreateServiceAsync(
            database,
            StaffRoles.Admin,
            credentialPath);

        await File.WriteAllBytesAsync(credentialPath, [1, 2, 3, 4]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetApiKeyAsync());

        Assert.Equal(
            "The UniSMS API key could not be read from secure storage.",
            exception.Message);
    }

    private static async Task<UniSmsCredentialService> CreateServiceAsync(
        TestDatabase database,
        string role,
        string credentialPath)
    {
        int staffId;
        string securityStamp;
        string username = $"unisms-test-{Guid.NewGuid():N}";

        await using (var context =
            await database.Factory.CreateDbContextAsync())
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
            new StaffRepository(database.Factory));

        return new UniSmsCredentialService(
            authorizationService,
            credentialPath);
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
                $"GolddieUniSmsTests_{Guid.NewGuid():N}");

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
