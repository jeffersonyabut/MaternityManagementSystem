using System.Windows.Controls;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;
using System.Windows;

namespace MaternityManagementSystem.Views;

public partial class PatientRegistrationView : UserControl
{
    public PatientRegistrationView()
    {
        InitializeComponent();

        var app = (App)Application.Current;

        var patientRepository = new PatientRepository();

        var authorizationService = new AuthorizationService(
            app.SessionService);

        var patientService = new PatientService(
            patientRepository,
            authorizationService,
            app.SessionService);

        DataContext = new PatientRegistrationViewModel(patientService);
    }
}
