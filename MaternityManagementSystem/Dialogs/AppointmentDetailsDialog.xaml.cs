
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MaternityManagementSystem.Dialogs;

public partial class AppointmentDetailsDialog : Window
{
    public AppointmentDetailsDialog()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}

public sealed class UtcToPhilippineTimeConverter : IValueConverter
{
    private static readonly TimeZoneInfo PhilippineTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not DateTime utcDateTime)
        {
            return "Not available";
        }

        DateTime utc = DateTime.SpecifyKind(
            utcDateTime,
            DateTimeKind.Utc);

        DateTime philippineTime =
            TimeZoneInfo.ConvertTimeFromUtc(
                utc,
                PhilippineTimeZone);

        return philippineTime.ToString(
            "MMMM dd, yyyy · hh:mm tt",
            CultureInfo.InvariantCulture);
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
