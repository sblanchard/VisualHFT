using System;
using System.Globalization;
using System.Windows.Data;

namespace VisualHFT.Converters;

public class StringEqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() == parameter?.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Not needed for this use case
        //throw new NotImplementedException();
        return null;
    }
}