
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Views;
using MaternityManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MaternityManagementSystem;

public partial class App : Application
{
    private const string NormalizedUsernameColumnMigration =
        "20261125035926_AddNormalizedUsernameColumn";

    private const string EnforceNormalizedUsernameMigration =
        "20261125041051_EnforceNormalizedUsername";

    public SessionService SessionService { get; } = new();

    public RegionalSettingsService RegionalSettings { get; } =
        new(new SystemSettingRepository());

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            await InitializeDatabaseAsync();

            // Initialize missing system settings.
            var settingsInitializer = new SystemSettingInitializer();
            await settingsInitializer.InitializeAsync();

            await RegionalSettings.RefreshAsync();

            // Check whether an administrator/staff account exists.
            var repository = new StaffRepository();
            bool hasStaff = await repository.HasAnyStaffAsync();

            // Show initial admin setup if no staff exists.
            if (!hasStaff)
            {
                var setupWindow = new AdminSetupWindow();
                bool? setupCompleted = setupWindow.ShowDialog();

                if (setupCompleted != true)
                {
                    Shutdown();
                    return;
                }
            }

            // Launch the main application.
            var mainWindow = new MainWindow();

            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Golddie Database Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }

    private static async Task InitializeDatabaseAsync()
    {
        await using var db = new ClinicDbContext();

        var appliedMigrations =
            (await db.Database.GetAppliedMigrationsAsync()).ToHashSet();

        // If the final migration has already been applied,
        // the normalization process is complete.
        if (!appliedMigrations.Contains(EnforceNormalizedUsernameMigration))
        {
            // Apply Stage 1 only if it has not already been applied.
            if (!appliedMigrations.Contains(NormalizedUsernameColumnMigration))
            {
                await db.Database.MigrateAsync(
                    NormalizedUsernameColumnMigration);
            }

            // Backfill existing usernames before enforcing NOT NULL
            // and the unique normalized username index.
            var normalizationService =
                new UsernameNormalizationMigrationService();

            await normalizationService.BackfillAsync(db);

            // Apply Stage 2 and any remaining pending migrations.
            await db.Database.MigrateAsync();
        }
        else
        {
            // Normalization is already complete.
            // Apply any other migrations that may have been added later.
            await db.Database.MigrateAsync();
        }
    }
}
