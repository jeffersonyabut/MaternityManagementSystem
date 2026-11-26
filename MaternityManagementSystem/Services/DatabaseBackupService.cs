using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;

namespace MaternityManagementSystem.Services;

public sealed class DatabaseBackupService
{
    private readonly AuthorizationService _authorizationService;
    private readonly IDbContextFactory<ClinicDbContext>? _contextFactory;

    public DatabaseBackupService(
        AuthorizationService authorizationService)
        : this(authorizationService, null)
    {
    }

    public DatabaseBackupService(
        AuthorizationService authorizationService,
        IDbContextFactory<ClinicDbContext>? contextFactory)
    {
        ArgumentNullException.ThrowIfNull(authorizationService);

        _authorizationService = authorizationService;
        _contextFactory = contextFactory;
    }

    public async Task<string> CreateBackupAsync(
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureAdminAsync();

        string? temporaryPath = null;

        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

            string fullDestinationDirectory =
                Path.GetFullPath(destinationDirectory);

            if (!Directory.Exists(fullDestinationDirectory))
            {
                throw new DirectoryNotFoundException();
            }

            string timestamp = DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss_fff",
                CultureInfo.InvariantCulture);

            string backupId = Guid.NewGuid().ToString("N");
            string fileName = $"Golddie_Backup_{timestamp}_{backupId}.db";
            string finalPath = Path.Combine(
                fullDestinationDirectory,
                fileName);

            temporaryPath = Path.Combine(
                fullDestinationDirectory,
                $".{fileName}.{Guid.NewGuid():N}.tmp");

            await using (ClinicDbContext dbContext =
                _contextFactory is null
                    ? new ClinicDbContext()
                    : await _contextFactory.CreateDbContextAsync(
                        cancellationToken))
            {
                if (dbContext.Database.GetDbConnection()
                    is not SqliteConnection sourceConnection)
                {
                    throw new InvalidOperationException();
                }

                await sourceConnection.OpenAsync(cancellationToken);

                var destinationConnectionString =
                    new SqliteConnectionStringBuilder
                    {
                        DataSource = temporaryPath,
                        Mode = SqliteOpenMode.ReadWriteCreate,
                        Pooling = false
                    }.ToString();

                await using var destinationConnection =
                    new SqliteConnection(destinationConnectionString);

                await destinationConnection.OpenAsync(cancellationToken);

                sourceConnection.BackupDatabase(destinationConnection);

                cancellationToken.ThrowIfCancellationRequested();

                await ValidateBackupAsync(
                    destinationConnection,
                    cancellationToken);
            }

            if (!File.Exists(temporaryPath) ||
                new FileInfo(temporaryPath).Length == 0)
            {
                throw new IOException();
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, finalPath);
            temporaryPath = null;

            return finalPath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new IOException(
                "The database backup could not be completed. Check the selected folder and available disk space.");
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static async Task ValidateBackupAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var integrityCommand = connection.CreateCommand())
        {
            integrityCommand.CommandText = "PRAGMA integrity_check;";

            object? integrityResult = await integrityCommand.ExecuteScalarAsync(
                cancellationToken);

            if (!string.Equals(
                    integrityResult as string,
                    "ok",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException();
            }
        }

        await using var foreignKeyCommand = connection.CreateCommand();
        foreignKeyCommand.CommandText = "PRAGMA foreign_key_check;";

        await using var foreignKeyReader =
            await foreignKeyCommand.ExecuteReaderAsync(cancellationToken);

        if (await foreignKeyReader.ReadAsync(cancellationToken))
        {
            throw new InvalidDataException();
        }
    }

    private static void TryDeleteTemporaryFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Do not expose file-system details from temporary cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Do not expose file-system details from temporary cleanup.
        }
    }
}
