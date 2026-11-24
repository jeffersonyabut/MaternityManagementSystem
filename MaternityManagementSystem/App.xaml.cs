
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Views;
using MaternityManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace MaternityManagementSystem;

public partial class App : Application
{
    public SessionService SessionService { get; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // 1. Initialize the database and apply pending migrations.
            await using (var db = new ClinicDbContext())
            {
                await db.Database.MigrateAsync();
            }

            // 2. Initialize missing system settings.
            var settingsInitializer = new SystemSettingInitializer();
            await settingsInitializer.InitializeAsync();

            // 3. Check whether an administrator/staff account exists.
            var repository = new StaffRepository();
            bool hasStaff = await repository.HasAnyStaffAsync();

            // 4. Show initial admin setup if no staff exists.
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

            // 5. Launch the main application.
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
}
