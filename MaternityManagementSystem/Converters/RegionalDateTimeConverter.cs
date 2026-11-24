
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MaternityManagementSystem.Converters;

public class RegionalDateTimeConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not DateTime dateTime)
            return string.Empty;

        if (Application.Current is not App app)
            return dateTime.ToString(culture);

        return app.RegionalSettings.FormatDateTime(dateTime);
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
