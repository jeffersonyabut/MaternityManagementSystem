
using System.Collections.ObjectModel;
using System.Windows;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Dialogs;

public partial class RecordHistoryDialog : Window
{
    public RecordHistoryDialog()
    {
        InitializeComponent();
    }

    public void Configure(
        string title,
        string subtitle,
        ObservableCollection<RecordVersionDisplayViewModel> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        TitleText.Text = title;
        SubtitleText.Text = subtitle;

        HistoryItemsControl.ItemsSource = history;

        EmptyMessage.Visibility = history.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
