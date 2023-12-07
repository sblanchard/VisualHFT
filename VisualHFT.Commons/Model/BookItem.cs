using VisualHFT.Commons.Model;
using VisualHFT.Helpers;

namespace VisualHFT.Model;

public class BookItem : IEquatable<BookItem>, IEqualityComparer<BookItem>, IResettable
{
    public int DecimalPlaces { get; set; }


    public string Symbol { get; set; }

    public int ProviderID { get; set; }

    public string EntryID { get; set; }

    public string LayerName { get; set; }

    public DateTime LocalTimeStamp { get; set; }

    public double? Price { get; set; }

    public DateTime ServerTimeStamp { get; set; }

    public double? Size { get; set; }

    public bool IsBid { get; set; }

    public string FormattedPrice => Price.HasValue ? Price.Value.ToString("N" + DecimalPlaces) : "";
    public string FormattedSize => Size.HasValue ? HelperCommon.GetKiloFormatter(Size.Value) : "";
    public string FormattedActiveSize => ActiveSize.HasValue ? HelperCommon.GetKiloFormatter(ActiveSize.Value) : "";

    public double? ActiveSize { get; set; }

    public bool Equals(BookItem x, BookItem y)
    {
        return x.Price == y.Price;
    }

    public int GetHashCode(BookItem obj)
    {
        return obj.Price.GetHashCode();
    }

    public bool Equals(BookItem other)
    {
        if (other == null)
            return false;
        if (IsBid != other.IsBid)
            return false;
        if (EntryID != other.EntryID)
            return false;
        if (Price != other.Price)
            return false;
        if (Size != other.Size)
            return false;
        return true;
    }

    public void Reset()
    {
        Symbol = "";
        ProviderID = 0;
        EntryID = "";
        LayerName = "";
        LocalTimeStamp = DateTime.MinValue;
        ServerTimeStamp = DateTime.MinValue;
        Price = 0;
        Size = 0;
        IsBid = false;
        DecimalPlaces = 0;
        ActiveSize = 0;
    }

    public void Update(BookItem b)
    {
        Symbol = b.Symbol;
        ProviderID = b.ProviderID;
        EntryID = b.EntryID;
        LayerName = b.LayerName;
        LocalTimeStamp = b.LocalTimeStamp;
        ServerTimeStamp = b.ServerTimeStamp;
        Price = b.Price;
        Size = b.Size;
        IsBid = b.IsBid;
        DecimalPlaces = b.DecimalPlaces;
        ActiveSize = b.ActiveSize;
    }
}