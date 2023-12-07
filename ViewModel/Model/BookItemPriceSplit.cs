using System;
using System.Globalization;
using Prism.Mvvm;
using VisualHFT.Helpers;

namespace VisualHFT.ViewModel.Model;

public class BookItemPriceSplit : BindableBase, ICloneable
{
    private readonly object _locker = new();
    private readonly string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
    private readonly string thousandsSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;

    public string LastDecimal { get; private set; } = "";

    public string NextTwoDecimals { get; private set; } = "";

    public string Rest { get; private set; } = "";

    public string Size { get; private set; } = "";

    public double Price { get; private set; }

    public object Clone()
    {
        return MemberwiseClone();
    }

    public void SetNumber(double price, double size, int symbolDecimalPlaces)
    {
        lock (_locker)
        {
            Price = price;
            if (price != 0)
                try
                {
                    var sPrice = string.Format("{0:N" + symbolDecimalPlaces + "}", price);
                    if (symbolDecimalPlaces > 0)
                    {
                        /*LastDecimal = sPrice.Last().ToString();
                        NextTwoDecimals = sPrice.Substring(sPrice.Length - 3, 2);
                        Rest = sPrice.Substring(0, sPrice.Length - 3);*/
                        Rest = sPrice.Split(decimalSeparator)[0];
                        NextTwoDecimals = (sPrice.Split(decimalSeparator)[1] + "00").Substring(0, symbolDecimalPlaces);
                        LastDecimal = "";
                    }
                    else
                    {
                        Rest = sPrice.Split(thousandsSeparator)[0];
                        NextTwoDecimals = sPrice.Split(thousandsSeparator)[1];
                    }

                    Size = HelperCommon.GetKiloFormatter(size);
                }
                catch
                {
                    LastDecimal = "-";
                    NextTwoDecimals = "-";
                    Rest = "-";
                    Size = "-";
                }


            if (price == 0)
            {
                LastDecimal = "";
                NextTwoDecimals = "";
                Rest = "";
                Size = "";
            }
        }
    }

    public void Clear()
    {
        lock (_locker)
        {
            Price = 0;
            LastDecimal = "";
            NextTwoDecimals = "";
            Rest = "";
            Size = "";
        }

        RaiseUIThread();
    }

    public void RaiseUIThread()
    {
        lock (_locker)
        {
            RaisePropertyChanged(nameof(LastDecimal));
            RaisePropertyChanged(nameof(NextTwoDecimals));
            RaisePropertyChanged(nameof(Rest));
            RaisePropertyChanged(nameof(Size));
        }
    }
}