using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class PregnancyRegistrationView : UserControl
{
    private readonly int _patientId;
    private readonly PregnancyRegistrationViewModel _viewModel;

    public PregnancyRegistrationView(int patientId)
    {
        InitializeComponent();

        _patientId = patientId;

        var app = (App)Application.Current;

        var authorizationService = new AuthorizationService(
            app.SessionService);

        var pregnancyService = new PregnancyService(
            new PregnancyRepository(),
            new PatientRepository(),
            authorizationService,
            app.SessionService);

        _viewModel = new PregnancyRegistrationViewModel(
            pregnancyService,
            patientId);

        DataContext = _viewModel;

        _viewModel.PregnancySaved += OnPregnancySaved;
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatientProfile(_patientId);
        }
    }

    private void OnPregnancySaved()
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatientProfile(_patientId);
        }
    }
}
