
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class PrenatalVisitRegistrationView : UserControl
{
    private readonly PrenatalVisitRegistrationViewModel _viewModel;

    public event Action? VisitSaved;
    public event Action? CancelRequested;

    public PrenatalVisitRegistrationView(Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(pregnancy);

        InitializeComponent();

        var app = (App)Application.Current;

        var authorizationService = new AuthorizationService(
            app.SessionService);

        var prenatalVisitService = new PrenatalVisitService(
            new PrenatalVisitRepository(),
            new PregnancyRepository(),
            authorizationService,
            app.SessionService);

        _viewModel = new PrenatalVisitRegistrationViewModel(
            prenatalVisitService,
            pregnancy);

        DataContext = _viewModel;

        _viewModel.VisitSaved += () =>
        {
            VisitSaved?.Invoke();
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
