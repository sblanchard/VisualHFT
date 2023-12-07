using System;
using System.Globalization;
using System.Windows.Data;

namespace VisualHFT.Converters;

public class ConverterValueToWidth : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            var value = (double)values[0]; // LOBImbalanceValue 
            var width = (double)values[1]; // ActualWidth of the Canvas
            double needleWidth = 10;

            //We are forced to use this, which are related on how the grid gauge is positioned 
            // with respect of the needle.

            // YES, this is terrible. Need to be improved.

            // In order to the needle to be at the very left, we need to return -95
            // In order to the needle to be at the very right, we need to return 355

            double outputMin = -95;
            double outputMax = 355;

            // Apply the linear transformation
            var output = outputMin + (value + 1) / 2 * (outputMax - outputMin);

            return output;
        }
        catch
        {
            return null;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}