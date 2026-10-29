
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MaternityManagementSystem.Data;
using Microsoft.Data.Sqlite;

namespace MaternityManagementSystem.Tests;

public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _databasePath;

    public TestClinicDbContextFactory Factory { get; }

    private TestDatabase(string databasePath)
    {
        _databasePath = databasePath;
        Factory = new TestClinicDbContextFactory(databasePath);
    }

    public static async Task<TestDatabase> CreateAsync()
    {
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"GolddieTests_{Guid.NewGuid():N}.db");

        var database = new TestDatabase(databasePath);

        try
        {
            await using var dbContext =
                await database.Factory.CreateDbContextAsync();

            await dbContext.Database.EnsureCreatedAsync();

            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        // Release SQLite's pooled connections before deleting test files.
        SqliteConnection.ClearAllPools();

        DeleteIfExists(_databasePath);
        DeleteIfExists(_databasePath + "-journal");
        DeleteIfExists(_databasePath + "-wal");
        DeleteIfExists(_databasePath + "-shm");

        return ValueTask.CompletedTask;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
