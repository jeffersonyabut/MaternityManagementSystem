using MaternityManagementSystem.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Views;

namespace MaternityManagementSystem;

public partial class App : Application
{
    public SessionService SessionService { get; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var repository = new StaffRepository();
            bool hasStaff = await repository.HasAnyStaffAsync();

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

            var mainWindow = new MainWindow();

            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Golddie could not initialize its database. " +
                "Please check the database configuration.",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }
}
