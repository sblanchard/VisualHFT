using System;
using System.Globalization;

namespace VisualHFT.Helpers;

public class HelperFormat
{
    public static string FormatNumber(double number)
    {
        if (number == 0)
            return "";
        var value = Math.Abs(number);
        var isNegative = number < 0;
        if (value >= 1000000000.0)
            return (isNegative ? "-" : "") +
                   (value / 1000000000.0).ToString("#,##0.##,,,B", CultureInfo.InvariantCulture); // Displays 1B
        if (value >= 1000000.0)
            return (isNegative ? "-" : "") +
                   (value / 1000000.0).ToString("#,##0.##,,M", CultureInfo.InvariantCulture); // Displays 1,235M
        if (value >= 1000.0)
            return (isNegative ? "-" : "") +
                   (value / 1000.0).ToString("#,##0.##,K", CultureInfo.InvariantCulture); // Displays 1,234,568K
        if (value < 1000.0)
            return (isNegative ? "-" : "") + value;

        return (isNegative ? "-" : "") + value;
    }
}