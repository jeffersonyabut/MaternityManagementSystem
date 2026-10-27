
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthenticationService _authenticationService;

    public LoginViewModel()
        : this(new AuthenticationService(new StaffRepository()))
    {
    }

    public LoginViewModel(
        AuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
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

            Message = $"Welcome, {staff.Username}! Role: {staff.Role}.";
        }
        catch (Exception)
        {
            Message = "Unable to sign in. Please try again.";
        }
    }
}
