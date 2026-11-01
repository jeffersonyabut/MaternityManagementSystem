
using System.Windows;
using MaternityManagementSystem.Navigation;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Views;
using MaternityManagementSystem.Models;
using System;

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


    public void ShowPrenatalVisits(
        MaternityManagementSystem.Models.Pregnancy pregnancy)
    {
        var view = new PrenatalVisitView(pregnancy);

        _navigationService.NavigateToView(view);
    }

    public void ShowPrenatalVisitRegistration(
    MaternityManagementSystem.Models.Pregnancy pregnancy)
    {
        var registrationView = new PrenatalVisitRegistrationView(pregnancy);

        registrationView.VisitSaved += () =>
        {
            ShowPrenatalVisits(pregnancy);
        };

        registrationView.CancelRequested += () =>
        {
            ShowPrenatalVisits(pregnancy);
        };

        _navigationService.NavigateToView(registrationView);
    }



    public void ShowPrenatalVisitEdit(
        int visitId,
        MaternityManagementSystem.Models.Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(pregnancy);

        var editView = new PrenatalVisitEditView(visitId);

        editView.VisitUpdated += () =>
        {
            ShowPrenatalVisits(pregnancy);
        };

        editView.CancelRequested += () =>
        {
            ShowPrenatalVisits(pregnancy);
        };

        _navigationService.NavigateToView(editView);
    }

    public void ShowVitalSigns(
        PrenatalVisit visit,
        Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(visit);
        ArgumentNullException.ThrowIfNull(pregnancy);

        var view = new VitalSignsView(visit, pregnancy);

        _navigationService.NavigateToView(view);
    }


    public void ShowVitalSignRegistration(PrenatalVisit visit, Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(visit);
        ArgumentNullException.ThrowIfNull(pregnancy);

        var registrationView = new VitalSignRegistrationView(visit);

        registrationView.VitalSignSaved += () =>
        {
            ShowVitalSigns(visit, pregnancy);
        };

        registrationView.CancelRequested += () =>
        {
            ShowVitalSigns(visit, pregnancy);
        };

        _navigationService.NavigateToView(registrationView);
    }



    public void ShowVitalSignEdit(
        VitalSign vitalSign,
        PrenatalVisit visit,
        Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(vitalSign);
        ArgumentNullException.ThrowIfNull(visit);
        ArgumentNullException.ThrowIfNull(pregnancy);

        var editView = new VitalSignEditView(vitalSign);

        editView.VitalSignUpdated += () =>
        {
            ShowVitalSigns(visit, pregnancy);
        };

        editView.CancelRequested += () =>
        {
            ShowVitalSigns(visit, pregnancy);
        };

        _navigationService.NavigateToView(editView);
    }


    public void ShowLaboratoryResultRegistration(Patient patient)
    {
        ArgumentNullException.ThrowIfNull(patient);

        var registrationView = new LaboratoryResultRegistrationView(patient);

        _navigationService.NavigateToView(registrationView);
    }





    public void ShowLaboratoryResultEdit(
        int laboratoryResultId,
        int patientId,
        Action? cancelAction = null,
        Action? saveAction = null)
    {
        if (laboratoryResultId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(laboratoryResultId));
        }

        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        var editView = new LaboratoryResultEditView(
            laboratoryResultId,
            patientId,
            cancelAction,
            saveAction);

        _navigationService.NavigateToView(editView);
    }








}
