using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;
using MaternityManagementSystem.ViewModels;

namespace MaternityManagementSystem.Views;

public partial class AppointmentSmsReminderMonitoringView : UserControl
{
    public AppointmentSmsReminderMonitoringView(
        AppointmentSmsReminderMonitoringViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }
}

public sealed class DateOnlyDateTimeConverter : IValueConverter
{
    public object? Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        return value is DateOnly date
            ? date.ToDateTime(TimeOnly.MinValue)
            : null;
    }

    public object? ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        return value is DateTime dateTime
            ? DateOnly.FromDateTime(dateTime)
            : null;
    }
}

public sealed class RegionalDateTimeConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not DateTime utcDateTime)
        {
            return string.Empty;
        }

        App app = (App)Application.Current;
        return app.RegionalSettings.FormatDateTime(utcDateTime);
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
