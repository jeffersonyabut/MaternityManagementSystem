
using System;
using System.Windows.Controls;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;
using System.Windows;

namespace MaternityManagementSystem.Views;

public partial class PatientEditView : UserControl
{
    private readonly PatientEditViewModel _viewModel;
    private readonly int _patientId;

    public event Action<int>? PatientUpdated;

    public PatientEditView(Patient patient)
    {
        InitializeComponent();

        _patientId = patient.Id;

        var app = (App)Application.Current;

        var authorizationService = new AuthorizationService(
            app.SessionService);

        var patientService = new PatientService(
            new PatientRepository(),
            authorizationService,
            app.SessionService);

        _viewModel = new PatientEditViewModel(
            patientService,
            patient);

        _viewModel.PatientUpdated += OnPatientUpdated;

        DataContext = _viewModel;
    }

    private void OnPatientUpdated()
    {
        PatientUpdated?.Invoke(_patientId);
    }

    private void CancelButton_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        var parentWindow = System.Windows.Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatientProfile(_patientId);
        }
    }

}
