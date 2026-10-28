
using System.Windows;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;
using MaternityManagementSystem.Views;

namespace MaternityManagementSystem;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var sessionService = ((App)Application.Current).SessionService;

        var loginViewModel = new LoginViewModel(
            new AuthenticationService(new StaffRepository()),
            sessionService);

        loginViewModel.LoginSucceeded += OnLoginSucceeded;

        DataContext = loginViewModel;
    }

    private void OnLoginSucceeded()
    {
        var sessionService = ((App)Application.Current).SessionService;

        var dashboard = new DashboardWindow(sessionService);

        // Make the dashboard the main window before closing login.
        Application.Current.MainWindow = dashboard;

        dashboard.Show();
        Close();
    }
}
