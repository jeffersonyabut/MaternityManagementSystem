
using System.Windows.Controls;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.ViewModels;


namespace MaternityManagementSystem.Views;

public partial class PatientProfileView : UserControl
{
    private readonly PatientProfileViewModel _viewModel;

    public PatientProfileView(int patientId)
    {
        InitializeComponent();

        _viewModel = new PatientProfileViewModel(
            new PatientRepository(),
            new PregnancyRepository(),
            patientId);

        DataContext = _viewModel;

        Loaded += PatientProfileView_Loaded;
    }

    private async void PatientProfileView_Loaded(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        await _viewModel.LoadPatientAsync();
    }


    private void BackToPatientsButton_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        var parentWindow = System.Windows.Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatients();
        }
    }


    private void EditPatientButton_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (_viewModel.Patient is null)
        {
            return;
        }

        var parentWindow = System.Windows.Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatientEdit(_viewModel.Patient);
        }
    }

    private void AddPregnancyButton_Click(
    object sender, System.Windows.RoutedEventArgs e)
    {
        var parentWindow = System.Windows.Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow &&
            _viewModel.Patient is not null)
        {
            dashboardWindow.ShowPregnancyRegistration(
                _viewModel.Patient.Id);
        }
    }



    private void EditPregnancyButton_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        if (button.DataContext is not MaternityManagementSystem.Models.Pregnancy pregnancy)
        {
            return;
        }

        var parentWindow = System.Windows.Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPregnancyEdit(pregnancy);
        }
    }



}
