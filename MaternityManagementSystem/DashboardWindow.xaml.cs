
using System.Windows;
using MaternityManagementSystem.Navigation;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Views;

namespace MaternityManagementSystem.Views;

public partial class DashboardWindow : Window
{
    private readonly SessionService _sessionService;
    private readonly NavigationService _navigationService =
        new NavigationService();

    public DashboardWindow(SessionService sessionService)
    {
        InitializeComponent();

        _sessionService = sessionService;

        var user = _sessionService.CurrentUser;

        if (user is null)
        {
            MessageBox.Show(
                "No active session was found. Please sign in again.",
                "Authentication Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Loaded += (_, _) => Close();
            return;
        }

        UserInfoText.Text = $"{user.Username} · {user.Role}";

        DataContext = _navigationService;
    }

    private void DashboardButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _navigationService.NavigateTo<DashboardView>();
    }

    private void PatientsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _navigationService.NavigateTo<PatientsView>();
    }


    public void ShowPatientProfile(int patientId)
    {
        _navigationService.NavigateToView(
            new PatientProfileView(patientId));
    }

    public void ShowPatients()
    {
        _navigationService.NavigateTo<PatientsView>();
    }


    public void ShowPatientEdit(
        MaternityManagementSystem.Models.Patient patient)
    {
        var editView = new PatientEditView(patient);

        editView.PatientUpdated += patientId =>
        {
            ShowPatientProfile(patientId);
        };

        _navigationService.NavigateToView(editView);
    }

    public void ShowPregnancyRegistration(int patientId)
    {
        var registrationView = new PregnancyRegistrationView(patientId);

        _navigationService.NavigateToView(registrationView);
    }


    public void ShowPregnancyEdit(
        MaternityManagementSystem.Models.Pregnancy pregnancy)
    {
        var editView = new PregnancyEditView(pregnancy);
        _navigationService.NavigateToView(editView);
    }







}
