
using System.Windows;
using System.Windows.Controls;
using MaternityManagementSystem.Dialogs;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class AuditLogManagementView : UserControl
{
    public AuditLogManagementView(
        AuditLogManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ViewDetailsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not AuditLogManagementViewModel viewModel)
            return;

        if (viewModel.SelectedAuditLog is null)
        {
            var warning = new CustomDialog
            {
                Owner = Window.GetWindow(this)
            };

            warning.Configure(
                "No Record Selected",
                "Please select an audit record before viewing its details.",
                confirmText: "Understood",
                showCancel: false);

            warning.SetAppearance(
                "!",
                "#6554C0",
                "#F0EEFA",
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(101, 84, 192)));

            warning.ShowDialog();
            return;
        }

        var dialog = new AuditLogDetailsDialog(
            viewModel.SelectedAuditLog)
        {
            Owner = Window.GetWindow(this)
        };

        dialog.ShowDialog();
    }
}
