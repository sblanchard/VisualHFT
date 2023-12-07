using System;
using System.Globalization;
using System.Windows.Data;
using VisualHFT.Helpers;

namespace VisualHFT.Converters;

public class KiloFormatter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var num = value.ToDouble();
        var isNegative = num < 0;
        if (num != 0)
            return (isNegative ? "-" : "") + HelperCommon.GetKiloFormatter(Math.Abs(num));
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}