using System;
using System.Globalization;
using System.Windows.Data;

namespace VisualHFT.Converters;

public class IsNaNConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double dValue) return double.IsNaN(dValue);
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}