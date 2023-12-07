namespace VisualHFT.Model;

public class Order
{
    public Order()
    {
        IsEmpty = true;
    }

    public double PendingQuantity => Quantity - FilledQuantity;

    /// <summary>
    ///     This override will fire PostedSecondsAgo property change when any other property fires
    /// </summary>
    /// <param name="args"></param>
    public string ProviderName { get; set; }

    public long OrderID { get; set; }

    public string StrategyCode { get; set; }

    public string Symbol { get; set; }

    public int ProviderId { get; set; }

    public string ClOrdId { get; set; }

    public eORDERSIDE Side { get; set; }

    public eORDERTYPE OrderType { get; set; }

    public eORDERTIMEINFORCE TimeInForce { get; set; }

    public eORDERSTATUS Status { get; set; }

    public double Quantity { get; set; }

    public double MinQuantity { get; set; }

    public double FilledQuantity { get; set; }

    public double PricePlaced { get; set; }

    public string Currency { get; set; }

    public string FutSettDate { get; set; }

    public bool IsMM { get; set; }

    public bool IsEmpty { get; set; }

    public string LayerName { get; set; }

    public int AttemptsToClose { get; set; }

    public int SymbolMultiplier { get; set; }

    public int SymbolDecimals { get; set; }

    public string FreeText { get; set; }

    public string OriginPartyID { get; set; }

    public List<Execution> Executions { get; set; }

    public int QuoteID { get; set; }

    public DateTime QuoteServerTimeStamp { get; set; }

    public DateTime QuoteLocalTimeStamp { get; set; }

    public DateTime CreationTimeStamp { get; set; }

    public DateTime LastUpdated { get; set; }

    public DateTime ExecutedTimeStamp { get; set; }

    public DateTime FireSignalTimestamp { get; set; }

    public double StopLoss { get; set; }

    public double TakeProfit { get; set; }

    public bool PipsTrail { get; set; }

    public double UnrealizedPnL { get; set; }

    public double MaxDrowdown { get; set; }

    public double BestBid { get; set; }

    public double BestAsk { get; set; }

    public double GetAvgPrice { get; set; }

    public double GetQuantity { get; set; }

    public double FilledPercentage { get; set; }

    public void Update(Order order)
    {
        ProviderName = order.ProviderName;
        OrderID = order.OrderID;
        StrategyCode = order.StrategyCode;
        Symbol = order.Symbol;
        ProviderId = order.ProviderId;
        ClOrdId = order.ClOrdId;
        Side = order.Side;
        OrderType = order.OrderType;
        TimeInForce = order.TimeInForce;
        Status = order.Status;
        Quantity = order.Quantity;
        MinQuantity = order.MinQuantity;
        FilledQuantity = order.FilledQuantity;
        PricePlaced = order.PricePlaced;
        Currency = order.Currency;
        FutSettDate = order.FutSettDate;
        IsMM = order.IsMM;
        IsEmpty = order.IsEmpty;
        LayerName = order.LayerName;
        AttemptsToClose = order.AttemptsToClose;
        SymbolMultiplier = order.SymbolMultiplier;
        SymbolDecimals = order.SymbolDecimals;
        FreeText = order.FreeText;
        OriginPartyID = order.OriginPartyID;
        Executions = order.Executions;
        QuoteID = order.QuoteID;
        QuoteServerTimeStamp = order.QuoteServerTimeStamp;
        QuoteLocalTimeStamp = order.QuoteLocalTimeStamp;
        CreationTimeStamp = order.CreationTimeStamp;
        FireSignalTimestamp = order.FireSignalTimestamp;
        StopLoss = order.StopLoss;
        TakeProfit = order.TakeProfit;
        PipsTrail = order.PipsTrail;
        UnrealizedPnL = order.UnrealizedPnL;
        MaxDrowdown = order.MaxDrowdown;
        BestAsk = order.BestAsk;
        BestBid = order.BestBid;
        GetAvgPrice = order.GetAvgPrice;
        GetQuantity = order.GetQuantity;


        LastUpdated = DateTime.Now;
    }

   
}