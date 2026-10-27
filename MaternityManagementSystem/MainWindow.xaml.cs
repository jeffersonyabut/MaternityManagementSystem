
using System.Windows;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new LoginViewModel();
    }
}
