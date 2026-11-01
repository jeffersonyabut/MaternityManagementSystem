
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using MaternityManagementSystem.Views;

namespace MaternityManagementSystem.Navigation;

public partial class NavigationService : ObservableObject
{
    private readonly Dictionary<Type, UserControl> _views = new();

    [ObservableProperty]
    private UserControl? currentView;

    [ObservableProperty]
    private string currentPage = string.Empty;

    public NavigationService()
    {
        NavigateTo<DashboardView>();
    }

    public void NavigateTo<TView>()
        where TView : UserControl, new()
    {
        Type viewType = typeof(TView);

        if (!_views.TryGetValue(viewType, out UserControl? view))
        {
            view = new TView();
            _views.Add(viewType, view);
        }

        NavigateToView(view);
    }

    public void NavigateToView(UserControl view)
    {
        ArgumentNullException.ThrowIfNull(view);

        CurrentView = view;
        CurrentPage = view.GetType().Name;
    }
}
