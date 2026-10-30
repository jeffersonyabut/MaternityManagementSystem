
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class PrenatalVisitView : UserControl
{
    private readonly Pregnancy _pregnancy;
    private readonly PrenatalVisitViewModel _viewModel;

    public PrenatalVisitView(Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(pregnancy);

        InitializeComponent();

        _pregnancy = pregnancy;

        var app = (App)Application.Current;

        var authorizationService = new AuthorizationService(
            app.SessionService);

        var prenatalVisitService = new PrenatalVisitService(
            new PrenatalVisitRepository(),
            new PregnancyRepository(),
            authorizationService,
            app.SessionService);

        _viewModel = new PrenatalVisitViewModel(
            prenatalVisitService,
            pregnancy);

        DataContext = _viewModel;

        Loaded += PrenatalVisitView_Loaded;
    }

    private async void PrenatalVisitView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.LoadVisitsAsync();
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatientProfile(_pregnancy.PatientId);
        }
    }

    private void AddVisitButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPrenatalVisitRegistration(_pregnancy);
        }
    }



    private void EditVisitButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (button.DataContext is not PrenatalVisit visit)
        {
            return;
        }

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPrenatalVisitEdit(
                visit.Id,
                _pregnancy);
        }
    }

    private void VitalSignsButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (button.DataContext is not PrenatalVisit visit)
        {
            return;
        }

        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowVitalSigns(visit, _pregnancy);
        }
    }


}
