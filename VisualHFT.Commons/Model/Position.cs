namespace VisualHFT.Model;

public class Position
{
    public Position()
    {
        CloseExecutions = new List<Execution>();
        OpenExecutions = new List<Execution>();
    }

    public Position(Position p)
    {
        CloseExecutions = p.CloseExecutions.Select(x => new Execution(x, p.Symbol)).ToList();
        OpenExecutions = p.OpenExecutions.Select(x => new Execution(x, p.Symbol)).ToList();


        AttemptsToClose = p.AttemptsToClose;
        CloseBestAsk = p.CloseBestAsk;
        CloseBestBid = p.CloseBestBid;
        CloseClOrdId = p.CloseClOrdId;
        CloseFireSignalTimestamp = p.CloseFireSignalTimestamp;
        CloseOriginPartyID = p.CloseOriginPartyID;
        CloseProviderId = p.CloseProviderId;
        CloseQuoteId = p.CloseQuoteId;
        CloseQuoteLocalTimeStamp = p.CloseQuoteLocalTimeStamp;
        CloseQuoteServerTimeStamp = p.CloseQuoteServerTimeStamp;
        CloseStatus = p.CloseStatus;
        CloseTimeStamp = p.CloseTimeStamp;
        CreationTimeStamp = p.CreationTimeStamp;
        Currency = p.Currency;
        FreeText = p.FreeText;
        FutSettDate = p.FutSettDate;
        GetCloseAvgPrice = p.GetCloseAvgPrice;
        GetCloseQuantity = p.GetCloseQuantity;
        GetOpenAvgPrice = p.GetOpenAvgPrice;
        GetOpenQuantity = p.GetOpenQuantity;
        GetPipsPnL = p.GetPipsPnL;
        ID = p.ID;
        IsCloseMM = p.IsCloseMM;
        IsOpenMM = p.IsOpenMM;
        LayerName = LayerName;
        MaxDrowdown = p.MaxDrowdown;
        OpenBestAsk = p.OpenBestAsk;
        OpenBestBid = p.OpenBestBid;
        OpenClOrdId = p.OpenClOrdId;
        OpenFireSignalTimestamp = p.OpenFireSignalTimestamp;
        OpenOriginPartyID = p.OpenOriginPartyID;
        OpenProviderId = p.OpenProviderId;
        OpenQuoteId = p.OpenQuoteId;
        OpenQuoteLocalTimeStamp = p.OpenQuoteLocalTimeStamp;
        OpenQuoteServerTimeStamp = p.OpenQuoteServerTimeStamp;
        OpenStatus = p.OpenStatus;
        OrderQuantity = p.OrderQuantity;
        PipsPnLInCurrency = p.PipsPnLInCurrency;
        PipsTrail = p.PipsTrail;
        PositionID = p.PositionID;
        Side = p.Side;
        StopLoss = p.StopLoss;
        StrategyCode = p.StrategyCode;
        Symbol = p.Symbol;
        SymbolDecimals = p.SymbolDecimals;
        SymbolMultiplier = p.SymbolMultiplier;
        TakeProfit = p.TakeProfit;
        UnrealizedPnL = p.UnrealizedPnL;
    }

    public string OpenProviderName { get; set; }
    public string CloseProviderName { get; set; }

    public List<Execution> AllExecutions
    {
        get
        {
            var _ret = new List<Execution>();
            if (OpenExecutions != null && OpenExecutions.Any())
                _ret.AddRange(OpenExecutions);

            if (CloseExecutions != null && CloseExecutions.Any())
                _ret.AddRange(CloseExecutions);
            return _ret /*.OrderBy(x => x.ServerTimeStamp)*/.ToList();
        }
    }


    public long ID { get; set; }
    public long PositionID { get; set; }
    public int AttemptsToClose { get; set; }
    public string CloseClOrdId { get; set; }
    public int CloseProviderId { get; set; }
    public int? CloseQuoteId { get; set; }
    public DateTime? CloseQuoteLocalTimeStamp { get; set; }
    public DateTime? CloseQuoteServerTimeStamp { get; set; }
    public int CloseStatus { get; set; }
    public DateTime CloseTimeStamp { get; set; }
    public DateTime CreationTimeStamp { get; set; }
    public string Currency { get; set; }
    public string FreeText { get; set; }
    public DateTime? FutSettDate { get; set; }
    public decimal GetCloseAvgPrice { get; set; }
    public decimal GetCloseQuantity { get; set; }
    public decimal GetOpenAvgPrice { get; set; }
    public decimal GetOpenQuantity { get; set; }
    public decimal GetPipsPnL { get; set; }
    public bool IsCloseMM { get; set; }
    public bool IsOpenMM { get; set; }
    public decimal MaxDrowdown { get; set; }
    public string OpenClOrdId { get; set; }
    public int OpenProviderId { get; set; }
    public int? OpenQuoteId { get; set; }
    public DateTime? OpenQuoteLocalTimeStamp { get; set; }
    public DateTime? OpenQuoteServerTimeStamp { get; set; }
    public int OpenStatus { get; set; }
    public decimal OrderQuantity { get; set; }
    public decimal PipsTrail { get; set; }
    public ePOSITIONSIDE Side { get; set; }
    public decimal StopLoss { get; set; }
    public string StrategyCode { get; set; }
    public string Symbol { get; set; }
    public int SymbolDecimals { get; set; }
    public int SymbolMultiplier { get; set; }
    public decimal TakeProfit { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal? OpenBestBid { get; set; }
    public decimal? OpenBestAsk { get; set; }
    public decimal? CloseBestBid { get; set; }
    public decimal? CloseBestAsk { get; set; }
    public string OpenOriginPartyID { get; set; }
    public string CloseOriginPartyID { get; set; }
    public string LayerName { get; set; }
    public DateTime? OpenFireSignalTimestamp { get; set; }
    public DateTime? CloseFireSignalTimestamp { get; set; }
    public decimal? PipsPnLInCurrency { get; set; }

    public virtual List<Execution> CloseExecutions { get; set; }
    public virtual List<Execution> OpenExecutions { get; set; }

    ~Position()
    {
        if (CloseExecutions != null)
            CloseExecutions.Clear();
        CloseExecutions = null;

        if (OpenExecutions != null)
            OpenExecutions.Clear();
        OpenExecutions = null;
    }

    private Order GetOrder(bool isOpen)
    {
        if (!string.IsNullOrEmpty(OpenClOrdId))
        {
            var o = new Order();
            //o.OrderID
            o.Currency = Currency;
            o.ClOrdId = isOpen ? OpenClOrdId : CloseClOrdId;
            o.ProviderId = isOpen ? OpenProviderId : CloseProviderId;
            o.ProviderName = isOpen ? OpenProviderName : CloseProviderName;
            o.LayerName = LayerName;
            o.AttemptsToClose = AttemptsToClose;
            o.BestAsk = isOpen ? OpenBestAsk.ToDouble() : CloseBestAsk.ToDouble();
            o.BestBid = isOpen ? OpenBestBid.ToDouble() : CloseBestBid.ToDouble();
            o.CreationTimeStamp = isOpen ? OpenQuoteLocalTimeStamp.ToDateTime() : CloseQuoteLocalTimeStamp.ToDateTime();
            o.Executions = isOpen ? OpenExecutions.ToList() : CloseExecutions.ToList();
            o.SymbolMultiplier = SymbolMultiplier;
            o.Symbol = Symbol;
            o.FreeText = FreeText;
            o.Status = (eORDERSTATUS)(isOpen ? OpenStatus : CloseStatus);
            o.GetAvgPrice = isOpen ? GetOpenAvgPrice.ToDouble() : GetCloseAvgPrice.ToDouble();

            o.GetQuantity = isOpen ? GetOpenQuantity.ToDouble() : GetCloseQuantity.ToDouble();
            o.Quantity = OrderQuantity.ToDouble();
            o.FilledQuantity = isOpen ? GetOpenQuantity.ToDouble() : GetCloseQuantity.ToDouble();

            o.IsEmpty = false;
            o.IsMM = isOpen ? IsOpenMM : IsCloseMM;
            //o.MaxDrowdown = 
            //o.MinQuantity = 
            //o.OrderID = 

            //TO-DO: we need to find a way to add this.
            //*************o.OrderType = this 

            //o.PipsTrail

            o.PricePlaced = o.Executions.Where(x =>
                    x.Status == ePOSITIONSTATUS.SENT || x.Status == ePOSITIONSTATUS.NEW ||
                    x.Status == ePOSITIONSTATUS.REPLACESENT)
                .First().Price.ToDouble();
            if (o.PricePlaced ==
                0) //if this happens, is because the data is corrupted. But, in order to auto-fix it, we use AvgPrice
                o.PricePlaced = o.GetAvgPrice;
            o.QuoteID = isOpen ? OpenQuoteId.ToInt() : CloseQuoteId.ToInt();
            o.QuoteLocalTimeStamp =
                isOpen ? OpenQuoteLocalTimeStamp.ToDateTime() : CloseQuoteLocalTimeStamp.ToDateTime();
            o.QuoteServerTimeStamp =
                isOpen ? OpenQuoteServerTimeStamp.ToDateTime() : CloseQuoteServerTimeStamp.ToDateTime();
            if (isOpen)
                o.Side = (eORDERSIDE)Side;
            else
                o.Side = Side == ePOSITIONSIDE.Sell ? eORDERSIDE.Buy : eORDERSIDE.Sell; //the opposite
            //o.StopLoss = 
            o.StrategyCode = StrategyCode;
            o.SymbolDecimals = SymbolDecimals;
            o.SymbolMultiplier = SymbolMultiplier;
            //o.TakeProfit

            //TO-DO: we need to find a way to add this.
            //*************o.TimeInForce = 

            //o.UnrealizedPnL               
            o.LastUpdated = DateTime.Now;
            o.FilledPercentage = 100 * (o.FilledQuantity / o.Quantity);
            return o;
        }

        return null;
    }

    public List<Order> GetOrders()
    {
        var openOrder = GetOrder(true);
        var closeOrder = GetOrder(false);
        var orders = new List<Order>();
        if (openOrder != null)
            orders.Add(openOrder);
        if (closeOrder != null)
            orders.Add(closeOrder);


        return orders;
    }
}