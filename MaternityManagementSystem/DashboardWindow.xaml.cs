
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Models.Reports;
using MaternityManagementSystem.Navigation;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.Services.Reports;
using MaternityManagementSystem.ViewModels;
using MaternityManagementSystem.ViewModels.Reports;
using MaternityManagementSystem.Views;
using MaternityManagementSystem.Views.Reports;

namespace MaternityManagementSystem.Views;

public partial class DashboardWindow : Window
{
    private readonly SessionService _sessionService;

    private readonly NavigationService _navigationService =
        new NavigationService();

    private bool _isHandlingSessionExpiration;

    public DashboardWindow(SessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(sessionService);

        InitializeComponent();

        _sessionService = sessionService;

        _sessionService.SessionExpired += SessionService_SessionExpired;
        Closed += DashboardWindow_Closed;

        var user = _sessionService.CurrentUser;

        if (user is null)
        {
            Loaded += DashboardWindow_MissingSession;
            return;
        }

        UserInfoText.Text = $"{user.Username} · {user.Role}";

        StaffManagementButton.Visibility = user.IsAdmin
            ? Visibility.Visible
            : Visibility.Collapsed;

        AuditLogManagementButton.Visibility = user.IsAdmin
            ? Visibility.Visible
            : Visibility.Collapsed;

        SystemSettingsButton.Visibility = user.IsAdmin
            ? Visibility.Visible
            : Visibility.Collapsed;

        AuditLogManagementButton.Click += AuditLogManagementButton_Click;

        StaffManagementButton.Click += StaffManagementButton_Click;

        DataContext = _navigationService;
        Loaded += DashboardWindow_Loaded;
    }

    private void LogoutButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            this,
            "Are you sure you want to sign out?",
            "Confirm Sign Out",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _sessionService.EndSession();

        ReturnToLogin(
            "You have successfully signed out. Please sign in again.",
            "Signed Out");
    }



    private void DashboardWindow_MissingSession(
        object sender,
        RoutedEventArgs e)
    {
        ReturnToLogin(
            "No active session was found. Please sign in again.",
            "Authentication Required");
    }

    private void SessionService_SessionExpired()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(
                new Action(HandleSessionExpired));

            return;
        }

        HandleSessionExpired();
    }

    private void HandleSessionExpired()
    {
        ReturnToLogin(
            "Your session has expired or your account permissions have changed. Please sign in again.",
            "Session Expired");
    }


    private void ReturnToLogin(
        string message,
        string title)
    {
        if (_isHandlingSessionExpiration)
        {
            return;
        }

        _isHandlingSessionExpiration = true;

        _sessionService.SessionExpired -= SessionService_SessionExpired;

        var loginWindow = new MainWindow(message);

        Application.Current.MainWindow = loginWindow;

        loginWindow.Show();
        loginWindow.Activate();

        Close();
    }


    private void DashboardWindow_Closed(
        object? sender,
        EventArgs e)
    {
        _sessionService.SessionExpired -= SessionService_SessionExpired;
    }

    private async void DashboardWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        var authorizationService = new AuthorizationService(
            _sessionService);

        var systemSettingService = new SystemSettingService(
            new SystemSettingRepository(),
            authorizationService,
            _sessionService);

        try
        {
            var setting = await systemSettingService.GetByKeyAsync(
                SystemSettingKeys.Clinic.Name);

            ClinicNameText.Text = string.IsNullOrWhiteSpace(setting?.Value)
                ? SystemSettingDefaults.Values[SystemSettingKeys.Clinic.Name]
                : setting.Value;
        }
        catch (UnauthorizedAccessException)
        {
            // Session expiration is handled by SessionService_SessionExpired.
            // A permission denial does not require closing the dashboard.
        }
        catch
        {
            ClinicNameText.Text =
                SystemSettingDefaults.Values[SystemSettingKeys.Clinic.Name];
        }
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

    public void ShowPatientEdit(Patient patient)
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

    public void ShowPregnancyEdit(Pregnancy pregnancy)
    {
        var editView = new PregnancyEditView(pregnancy);
        _navigationService.NavigateToView(editView);
    }

    public void ShowPrenatalVisits(Pregnancy pregnancy)
    {
        var view = new PrenatalVisitView(pregnancy);

        _navigationService.NavigateToView(view);
    }

    public void ShowPrenatalVisitRegistration(Pregnancy pregnancy)
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
        Pregnancy pregnancy)
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

    public void ShowVitalSignRegistration(
        PrenatalVisit visit,
        Pregnancy pregnancy)
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

    // REPORTS NAVIGATION

    private void ReportsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowReports();
    }

    public void ShowReports()
    {
        _navigationService.NavigateToView(
            new ReportsView());
    }

    public void NavigateToReportView(UserControl reportView)
    {
        ArgumentNullException.ThrowIfNull(reportView);

        _navigationService.NavigateToView(reportView);
    }

    public void ShowIndividualPatientRecordSelection()
    {
        var view = new IndividualPatientRecordSelectionView();

        view.ReportGenerated += ShowIndividualPatientRecordPreview;
        view.BackRequested += ShowReports;

        _navigationService.NavigateToView(view);
    }

    public void ShowIndividualPatientRecordPreview(
        IndividualPatientRecordReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var view = new IndividualPatientRecordReportView(report);

        view.BackRequested += ShowIndividualPatientRecordSelection;

        _navigationService.NavigateToView(view);
    }

    public void ShowPrenatalVisitReport(
        int visitId,
        Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(pregnancy);

        if (visitId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitId));
        }

        var app = (App)Application.Current;

        var authorizationService = new AuthorizationService(
            app.SessionService);

        var prenatalVisitService = new PrenatalVisitService(
            new PrenatalVisitRepository(),
            new PregnancyRepository(),
            authorizationService,
            app.SessionService);

        var laboratoryResultService = new LaboratoryResultService(
            new LaboratoryResultRepository(),
            new PatientRepository(),
            new PrenatalVisitRepository(),
            new PregnancyRepository(),
            authorizationService,
            app.SessionService);

        var reportService = new PrenatalVisitReportService(
            prenatalVisitService,
            laboratoryResultService);

        var viewModel = new PrenatalVisitReportViewModel(
            reportService,
            visitId);

        var view = new PrenatalVisitReportView
        {
            DataContext = viewModel
        };

        view.BackRequested += () =>
        {
            ShowPrenatalVisits(pregnancy);
        };

        view.Loaded += async (_, _) =>
        {
            await viewModel.GenerateReportCommand.ExecuteAsync(null);
        };

        _navigationService.NavigateToView(view);
    }

    // APPOINTMENT OVERVIEW

    private void AppointmentsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowAppointments();
    }

    public void ShowAppointments()
    {
        _navigationService.NavigateToView(
            new AppointmentOverviewView());
    }

    // APPOINTMENT REGISTRATION

    public void ShowAppointmentRegistration()
    {
        var registrationView = new AppointmentRegistrationView();

        registrationView.AppointmentSaved += ShowAppointments;
        registrationView.CancelRequested += ShowAppointments;

        _navigationService.NavigateToView(registrationView);
    }

    // STAFF MANAGEMENT NAVIGATION

    private void StaffManagementButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowStaffManagement();
    }

    private void AuditLogManagementButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowAuditLogManagement();
    }

    public void ShowStaffManagement()
    {
        var app = (App)Application.Current;

        var sessionService = app.SessionService;

        var authorizationService = new AuthorizationService(
            sessionService);

        var staffRepository = new StaffRepository();

        var auditLogRepository = new AuditLogRepository();

        var auditLogService = new AuditLogService(
            new AuditLogRepository(),
            _sessionService,
            authorizationService,
            app.RegionalSettings);

        var staffManagementService = new StaffManagementService(
            staffRepository,
            authorizationService,
            auditLogService,
            sessionService);

        var viewModel = new StaffManagementViewModel(
            staffManagementService,
            app.RegionalSettings);

        var view = new StaffManagementView
        {
            DataContext = viewModel
        };

        _navigationService.NavigateToView(view);

        view.Loaded += async (_, _) =>
        {
            await viewModel.LoadStaffAsync();
        };
    }

    // AUDIT LOG MANAGEMENT NAVIGATION

    public void ShowAuditLogManagement()
    {
        var app = (App)Application.Current;

        var sessionService = app.SessionService;

        var authorizationService = new AuthorizationService(
            sessionService);

        var staffRepository = new StaffRepository();

        var auditLogRepository = new AuditLogRepository();

        var auditLogService = new AuditLogService(
            new AuditLogRepository(),
            _sessionService,
            authorizationService,
            app.RegionalSettings);

        var staffManagementService = new StaffManagementService(
            staffRepository,
            authorizationService,
            auditLogService,
            sessionService);

        var viewModel = new AuditLogManagementViewModel(
            auditLogService,
            staffManagementService,
            app.RegionalSettings);

        var view = new AuditLogManagementView(viewModel);

        view.Loaded += async (_, _) =>
        {
            await viewModel.LoadCommand.ExecuteAsync(null);
        };

        _navigationService.NavigateToView(view);
    }

    // SYSTEM SETTINGS NAVIGATION

    public void ShowSystemSettings()
    {
        var app = (App)Application.Current;

        var sessionService = app.SessionService;

        var authorizationService = new AuthorizationService(
            sessionService);

        var systemSettingRepository = new SystemSettingRepository();

        var systemSettingService = new SystemSettingService(
            systemSettingRepository,
            authorizationService,
            sessionService);

        var viewModel = new SystemSettingsViewModel(
            systemSettingService,
            app.RegionalSettings,
            new DatabaseBackupService(authorizationService));

        var view = new SystemSettingsView
        {
            DataContext = viewModel
        };

        view.Loaded += async (_, _) =>
        {
            await viewModel.LoadSettingsAsync();
        };

        _navigationService.NavigateToView(view);
    }

    private void SystemSettingsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowSystemSettings();
    }
}
