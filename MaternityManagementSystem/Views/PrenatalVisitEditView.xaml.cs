using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class PrenatalVisitEditView : UserControl
{
    private readonly PrenatalVisitEditViewModel _viewModel;

    public event Action? VisitUpdated;
    public event Action? CancelRequested;

    public PrenatalVisitEditView(int visitId)
    {
        if (visitId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitId));
        }

        InitializeComponent();

        var app = (App)Application.Current;

        var authorizationService = new AuthorizationService(
            app.SessionService);

        var prenatalVisitService = new PrenatalVisitService(
            new PrenatalVisitRepository(),
            new PregnancyRepository(),
            authorizationService,
            app.SessionService);

        _viewModel = new PrenatalVisitEditViewModel(
            prenatalVisitService,
            visitId);

        DataContext = _viewModel;

        Loaded += PrenatalVisitEditView_Loaded;

        _viewModel.VisitUpdated += () =>
        {
            VisitUpdated?.Invoke();
        };
    }

    private async void PrenatalVisitEditView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await _viewModel.LoadVisitAsync();
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
