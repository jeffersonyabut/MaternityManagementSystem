
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Views;
using MaternityManagementSystem.Data;
using MaternityManagementSystem.Themes;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;

namespace MaternityManagementSystem;

public partial class App : Application
{
    private static readonly TimeSpan WorkerShutdownTimeout = TimeSpan.FromSeconds(5);
    private CancellationTokenSource? _reminderWorkerCancellation;
    private Task? _reminderWorkerTask;
    private const string NormalizedUsernameColumnMigration =
        "20261125035926_AddNormalizedUsernameColumn";

    private const string EnforceNormalizedUsernameMigration =
        "20261125041051_EnforceNormalizedUsername";

    public SessionService SessionService { get; } = new();

    public ThemeService ThemeService { get; private set; } = null!;

    public RegionalSettingsService RegionalSettings { get; } =
        new(new SystemSettingRepository());

    public AppointmentSmsReminderService ReminderService { get; } = new(
        new AppointmentSmsReminderRepository(new ClinicDbContextFactory()),
        new SystemSettingRepository(new ClinicDbContextFactory()));

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            ThemeService = new ThemeService(
                new ThemePreferenceStore(),
                new WindowsSystemThemeMonitor(Dispatcher),
                new WpfThemeResourceApplier(Resources));

            await InitializeDatabaseAsync();

            // Initialize missing system settings.
            var settingsInitializer = new SystemSettingInitializer();
            await settingsInitializer.InitializeAsync();

            await RegionalSettings.RefreshAsync();

            // Run a single catch-up reconciliation at startup. This schedules
            // and suppresses local records only; it does not contact UniSMS.
            Task reminderReconciliation = ReconcileRemindersSafelyAsync();
            await reminderReconciliation;

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

            StartReminderWorker(reminderReconciliation);
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

    private async Task ReconcileRemindersSafelyAsync()
    {
        try
        {
            await ReminderService.ReconcileAllAsync();
        }
        catch (Exception)
        {
            System.Diagnostics.Debug.WriteLine(
                "Appointment reminder reconciliation will be retried at the next application startup.");
        }
    }

    private void StartReminderWorker(Task reminderReconciliation)
    {
        var contextFactory = new ClinicDbContextFactory();
        var reminderRepository = new AppointmentSmsReminderRepository(contextFactory);
        var settingsRepository = new SystemSettingRepository(contextFactory);
        var schedulingService = new AppointmentSmsReminderService(
            reminderRepository,
            settingsRepository);
        var authorizationService = new AuthorizationService(
            SessionService,
            new StaffRepository());
        var credentialService = new UniSmsCredentialService(authorizationService);
        var dispatchService = new AppointmentSmsReminderDispatchService(
            schedulingService,
            reminderRepository,
            settingsRepository,
            new UniSmsClient(credentialService));

        _reminderWorkerCancellation = new CancellationTokenSource();
        var worker = new AppointmentSmsReminderWorker(dispatchService);
        _reminderWorkerTask = Task.Run(() => worker.RunAfterReconciliationAsync(
            reminderReconciliation,
            _reminderWorkerCancellation.Token));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeService?.Dispose();

        bool workerStopped = true;
        if (_reminderWorkerCancellation is not null && _reminderWorkerTask is not null)
        {
            workerStopped = AppointmentSmsReminderWorker.Stop(
                _reminderWorkerCancellation,
                _reminderWorkerTask,
                WorkerShutdownTimeout);
        }

        if (workerStopped)
        {
            _reminderWorkerCancellation?.Dispose();
        }
        base.OnExit(e);
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
