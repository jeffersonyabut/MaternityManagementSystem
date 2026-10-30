
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class VitalSignRegistrationView : UserControl
{
    private readonly VitalSignRegistrationViewModel _viewModel;

    public event Action? VitalSignSaved;
    public event Action? CancelRequested;

    public VitalSignRegistrationView(PrenatalVisit prenatalVisit)
    {
        ArgumentNullException.ThrowIfNull(prenatalVisit);

        InitializeComponent();

        var app = (App)Application.Current;
        var authorizationService = new AuthorizationService(app.SessionService);

        var vitalSignService = new VitalSignService(
            new VitalSignRepository(),
            new PrenatalVisitRepository(),
            authorizationService,
            app.SessionService);

        _viewModel = new VitalSignRegistrationViewModel(
            vitalSignService,
            prenatalVisit);

        DataContext = _viewModel;

        _viewModel.VitalSignSaved += () =>
        {
            VitalSignSaved?.Invoke();
        };
    }

    private async void SaveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.SaveAsync();
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        CancelRequested?.Invoke();
    }
}
