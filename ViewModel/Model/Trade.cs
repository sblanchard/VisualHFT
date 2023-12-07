using PropertyChanged;

namespace VisualHFT.ViewModel.Model;

[AddINotifyPropertyChangedInterface]
public class Trade : VisualHFT.Model.Trade
{
    public Trade(VisualHFT.Model.Trade t)
    {
        if (t == null)
            return;
        ProviderId = t.ProviderId;
        ProviderName = t.ProviderName;
        IsBuy = t.IsBuy;
        Symbol = t.Symbol;
        Size = t.Size;
        Price = t.Price;
        Flags = t.Flags;
        Timestamp = t.Timestamp;
    }
}