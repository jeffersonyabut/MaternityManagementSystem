using System.Data;
using MaternityManagementSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace MaternityManagementSystem.Services;

public sealed class UsernameNormalizationMigrationService
{
    public async Task BackfillAsync(
        ClinicDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var connection = db.Database.GetDbConnection();
        bool openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);

            var accounts = new List<StaffUsernameRecord>();

            // Read existing accounts and calculate their canonical usernames.
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    """
                    SELECT Id, Username
                    FROM StaffMembers
                    ORDER BY Id;
                    """;

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    int id = reader.GetInt32(0);

                    string username = reader.IsDBNull(1)
                        ? string.Empty
                        : reader.GetString(1);

                    string normalizedUsername =
                        UsernameNormalizer.Normalize(username);

                    if (string.IsNullOrWhiteSpace(normalizedUsername))
                    {
                        throw new InvalidOperationException(
                            $"Staff account ID {id} has an empty username. " +
                            "Resolve this account before continuing.");
                    }

                    if (normalizedUsername.Length > 100)
                    {
                        throw new InvalidOperationException(
                            $"Staff account ID {id} has a normalized username " +
                            "longer than the allowed 100 characters. " +
                            "Resolve this account before continuing.");
                    }

                    accounts.Add(new StaffUsernameRecord(
                        id,
                        username,
                        normalizedUsername));
                }
            }

            // Detect accounts that would become identical after normalization.
            var collisions = accounts
                .GroupBy(
                    account => account.NormalizedUsername,
                    StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .ToList();

            if (collisions.Count > 0)
            {
                var details = collisions.SelectMany(group =>
                    group.Select(account =>
                        $"- Staff ID {account.Id}: '{account.Username}'"));

                throw new InvalidOperationException(
                    "Username normalization conflicts were found.\n\n" +
                    string.Join(Environment.NewLine, details) +
                    "\n\nNo usernames were changed. Rename the conflicting " +
                    "accounts so each has a unique normalized username, " +
                    "then run the migration again.");
            }

            // Backfill only after every account passes validation.
            await using (var updateCommand = connection.CreateCommand())
            {
                updateCommand.Transaction = transaction;
                updateCommand.CommandText =
                    """
                    UPDATE StaffMembers
                    SET NormalizedUsername = $normalizedUsername
                    WHERE Id = $id;
                    """;

                var normalizedParameter =
                    updateCommand.CreateParameter();

                normalizedParameter.ParameterName =
                    "$normalizedUsername";

                updateCommand.Parameters.Add(normalizedParameter);

                var idParameter = updateCommand.CreateParameter();
                idParameter.ParameterName = "$id";

                updateCommand.Parameters.Add(idParameter);

                foreach (var account in accounts)
                {
                    normalizedParameter.Value =
                        account.NormalizedUsername;

                    idParameter.Value = account.Id;

                    await updateCommand.ExecuteNonQueryAsync(
                        cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private sealed record StaffUsernameRecord(
        int Id,
        string Username,
        string NormalizedUsername);
}
