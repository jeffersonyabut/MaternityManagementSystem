
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MaternityManagementSystem.Converters;

public class RegionalDateOnlyConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not DateOnly date)
            return string.Empty;

        if (Application.Current is not App app)
            return date.ToString(culture);

        return app.RegionalSettings.FormatDate(date);
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
