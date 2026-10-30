using System;
using System.Globalization;
using System.Windows.Data;

namespace MaternityManagementSystem.Converters;

public class UtcToLocalDateTimeConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not DateTime utcDateTime)
        {
            return value;
        }

        return DateTime.SpecifyKind(
            utcDateTime,
            DateTimeKind.Utc).ToLocalTime();
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
