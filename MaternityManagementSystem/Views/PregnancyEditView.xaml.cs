
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class PregnancyEditView : UserControl
{
    private readonly int _patientId;
    private readonly PregnancyEditViewModel _viewModel;

    public PregnancyEditView(Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(pregnancy);

        InitializeComponent();

        _patientId = pregnancy.PatientId;

        var pregnancyService = new PregnancyService(
            new PregnancyRepository(),
            new PatientRepository());

        _viewModel = new PregnancyEditViewModel(
            pregnancyService,
            pregnancy);

        _viewModel.PregnancyUpdated += OnPregnancyUpdated;

        DataContext = _viewModel;
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

    private void OnPregnancyUpdated()
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatientProfile(_patientId);
        }
    }
}
