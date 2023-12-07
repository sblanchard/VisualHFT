namespace VisualHFT.Model;

public class Trade
{
    public int ProviderId { get; set; }

    public string ProviderName { get; set; }

    public string Symbol { get; set; }

    public decimal Price { get; set; }

    public decimal Size { get; set; }

    public DateTime Timestamp { get; set; }

    public bool IsBuy { get; set; }

    public string Flags { get; set; }

    internal void CopyTo(Trade target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        target.Symbol = Symbol;
        target.Price = Price;
        target.Size = Size;
        target.Timestamp = Timestamp;
        target.IsBuy = IsBuy;
        target.Flags = Flags;
        target.ProviderId = ProviderId;
        target.ProviderName = ProviderName;
    }
}