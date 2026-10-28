
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class PatientListView : UserControl
{
    private readonly PatientListViewModel _viewModel;

    public PatientListView()
    {
        InitializeComponent();

        _viewModel = new PatientListViewModel(new PatientRepository());
        DataContext = _viewModel;

        _viewModel.OpenProfileRequested += OnOpenProfileRequested;

        Loaded += PatientListView_Loaded;
        IsVisibleChanged += PatientListView_IsVisibleChanged;
    }

    private async void PatientListView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.LoadPatientsCommand.ExecuteAsync(null);
    }

    private async void PatientListView_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            await _viewModel.LoadPatientsCommand.ExecuteAsync(null);
        }
    }



    private void OnOpenProfileRequested(int patientId)
    {
        var parentWindow = Window.GetWindow(this);

        if (parentWindow is DashboardWindow dashboardWindow)
        {
            dashboardWindow.ShowPatientProfile(patientId);
        }
    }


}
