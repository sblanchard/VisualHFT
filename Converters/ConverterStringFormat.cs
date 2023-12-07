using System;
using System.Globalization;
using System.Windows.Data;

namespace VisualHFT.Converters;

public class ConverterStringFormat : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        //first value = BrokerID
        //second value = Price Value
        var StringFormat = "F8";
        if (values[0] is int && values[1] is double)
        {
            var decimalsInPrice = (int?)values[0];
            if (!decimalsInPrice.HasValue)
                decimalsInPrice = 8;

            var price = (double?)values[1];
            StringFormat = "F" + decimalsInPrice;
            return price.Value.ToString(StringFormat);
        }

        if (values[0] is int && values[1] == null)
        {
            return null;
        }

        return "err";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}