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

        if (Application.Current is not App app)
        {
            return "Not available";
        }

        return app.RegionalSettings.FormatDateTime(utcDateTime);
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
