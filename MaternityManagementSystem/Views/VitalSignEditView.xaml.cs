
using System;
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Repositories;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class VitalSignEditView : UserControl
{
    private readonly VitalSignEditViewModel _viewModel;

    public event Action? VitalSignUpdated;
    public event Action? CancelRequested;

    public VitalSignEditView(VitalSign vitalSign)
    {
        ArgumentNullException.ThrowIfNull(vitalSign);
        InitializeComponent();

        var app = (App)Application.Current;
        var authorizationService = new AuthorizationService(app.SessionService);

        var vitalSignService = new VitalSignService(
            new VitalSignRepository(),
            new PrenatalVisitRepository(),
            authorizationService,
            app.SessionService);

        _viewModel = new VitalSignEditViewModel(
            vitalSignService,
            vitalSign);

        DataContext = _viewModel;

        _viewModel.VitalSignUpdated += () =>
        {
            VitalSignUpdated?.Invoke();
        };
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveAsync();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke();
    }
}
