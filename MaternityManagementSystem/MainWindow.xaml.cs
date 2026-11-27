
using System;
using System.Windows;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;
using MaternityManagementSystem.Views;

namespace MaternityManagementSystem;

public partial class MainWindow : Window
{
    public MainWindow(string? initialMessage = null)
    {
        InitializeComponent();

        var sessionService = ((App)Application.Current).SessionService;

        var loginViewModel = new LoginViewModel(
            new AuthenticationService(new StaffRepository()),
            sessionService);

        if (!string.IsNullOrWhiteSpace(initialMessage))
        {
            loginViewModel.Message = initialMessage;
        }

        loginViewModel.LoginSucceeded += OnLoginSucceeded;

        DataContext = loginViewModel;
    }

    private void OnLoginSucceeded()
    {
        var app = (App)Application.Current;
        var sessionService = app.SessionService;

        if (sessionService.CurrentUser is { } user)
        {
            app.ThemeService.LoadPreferenceForStaff(user.StaffId);
        }

        var dashboard = new DashboardWindow(sessionService);

        // Make the dashboard the main window before closing login.
        Application.Current.MainWindow = dashboard;

        dashboard.Show();
        Close();
    }
}
