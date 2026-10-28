using System.Windows.Controls;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class PatientRegistrationView : UserControl
{
    public PatientRegistrationView()
    {
        InitializeComponent();

        var patientRepository = new PatientRepository();
        var patientService = new PatientService(patientRepository);

        DataContext = new PatientRegistrationViewModel(patientService);
    }
}
