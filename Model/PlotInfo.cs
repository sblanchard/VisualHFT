using System;
using VisualHFT.Commons.Model;

namespace VisualHFT.Model;

public class PlotInfo : IResettable
{
    public DateTime Date { get; set; }

    public double Value { get; set; }

    public void Reset()
    {
        Value = 0;
        Date = DateTime.MinValue;
    }
}