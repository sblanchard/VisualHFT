using System.Collections.Generic;

namespace VisualHFT.AnalyticReports.ViewModel;

public class cListPLRanges
{
    public cListPLRanges(string range, int qty)
    {
        Range = range;
        Qty = qty;
    }

    public string Range { get; }
    public int Qty { get; }

    public override bool Equals(object obj)
    {
        return obj is cListPLRanges other &&
               Range == other.Range &&
               Qty == other.Qty;
    }

    public override int GetHashCode()
    {
        var hashCode = -1292202763;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Range);
        hashCode = hashCode * -1521134295 + Qty.GetHashCode();
        return hashCode;
    }
}