using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Sessions;


namespace MaternityManagementSystem.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    public event Action? LoginSucceeded;
    private readonly AuthenticationService _authenticationService;
    private readonly SessionService _sessionService;

    public LoginViewModel()
        : this(
            new AuthenticationService(new StaffRepository()),
            new SessionService())
    {
    }

    public LoginViewModel(
        AuthenticationService authenticationService,
        SessionService sessionService)
    {
        _authenticationService = authenticationService;
        _sessionService = sessionService;
    }

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string message = "Please sign in to continue.";

    [RelayCommand]
    private async Task SignInAsync(string password)
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            Message = "Please enter your username.";
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            Message = "Please enter your password.";
            return;
        }

        try
        {
            Message = "Signing in...";

            var staff = await _authenticationService.AuthenticateAsync(
                Username,
                password);

            if (staff is null)
            {
                Message = "Invalid username or password.";
                return;
            }

            var session = new UserSession(
                staff.Id,
                staff.Username,
                staff.Role);

            _sessionService.StartSession(session);

            Message = "Login successful!";
            LoginSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("LOGIN ERROR:");
            Debug.WriteLine(ex.ToString());

            Message = "Unable to sign in. Please try again.";
        }
    }
}
