using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Prism.Mvvm;
using VisualHFT.Helpers;
using VisualHFT.Model;
using VisualHFT.ViewModel.Model;
using Order = VisualHFT.Model.Order;
using Provider = VisualHFT.ViewModel.Model.Provider;
using Trade = VisualHFT.ViewModel.Model.Trade;

namespace VisualHFT.ViewModel;

public class vmOrderBook : BindableBase, IDisposable
{
    private readonly AggregationLevel _AGG_LEVEL_CHARTS = AggregationLevel.Ms100;
    private ObservableCollection<BookItem> _asksGrid;
    private BookItem _AskTOB = new();
    private BookItemPriceSplit _AskTOB_SPLIT;
    private ObservableCollection<BookItem> _bidsGrid;
    private BookItem _BidTOB = new();

    private BookItemPriceSplit _BidTOB_SPLIT;
    private readonly ObservableCollection<BookItem> _depthGrid;

    private Dictionary<string, Func<string, string, bool>> _dialogs;
    private bool _disposed; // to track whether the object has been disposed
    private string _layerName;
    private readonly int _MAX_CHART_POINTS = 300;
    private double _maxOrderSize;

    private double _MidPoint;
    private double _minOrderSize;

    private double _PercentageWidth = 1;

    private AggregatedCollection<PlotInfoPriceChart> _realTimePrices;
    private AggregatedCollection<PlotInfoPriceChart> _realTimeSpread;
    private ObservableCollection<Trade> _realTimeTrades;
    private Provider _selectedProvider;
    private string _selectedSymbol;
    private double _Spread;

    private int _switchView;
    protected object MTX_ORDERBOOK = new();


    private readonly UIUpdater uiUpdater;


    public vmOrderBook(Dictionary<string, Func<string, string, bool>> dialogs)
    {
        _dialogs = dialogs;

        _bidsGrid = new ObservableCollection<BookItem>();
        _asksGrid = new ObservableCollection<BookItem>();
        _depthGrid = new ObservableCollection<BookItem>();
        Asks = CollectionViewSource.GetDefaultView(_asksGrid);
        Bids = CollectionViewSource.GetDefaultView(_bidsGrid);
        Depth = CollectionViewSource.GetDefaultView(_depthGrid);
        SetSortDescriptions();


        HelperProvider.Instance.OnDataReceived += PROVIDERS_OnDataReceived;
        HelperProvider.Instance.OnHeartBeatFail += PROVIDERS_OnHeartBeatFail;
        HelperCommon.ACTIVEORDERS.OnDataReceived += ACTIVEORDERS_OnDataReceived;
        HelperCommon.ACTIVEORDERS.OnDataRemoved += ACTIVEORDERS_OnDataRemoved;

        HelperTrade.Instance.Subscribe(TRADES_OnDataReceived);
        HelperOrderBook.Instance.Subscribe(LIMITORDERBOOK_OnDataReceived);


        uiUpdater = new UIUpdater(uiUpdaterAction, 200);
        Providers = Provider.CreateObservableCollection();
        RaisePropertyChanged(nameof(Providers));


        BidTOB_SPLIT = new BookItemPriceSplit();
        AskTOB_SPLIT = new BookItemPriceSplit();
        RaisePropertyChanged(nameof(BidTOB_SPLIT));
        RaisePropertyChanged(nameof(AskTOB_SPLIT));
    }

    public OrderBook OrderBook { get; private set; }

    public string SelectedSymbol
    {
        get => _selectedSymbol;
        set => SetProperty(ref _selectedSymbol, value, () => Clear());
    }

    public Provider SelectedProvider
    {
        get => _selectedProvider;
        set => SetProperty(ref _selectedProvider, value, () => Clear());
    }

    public string SelectedLayer
    {
        get => _layerName;
        set => SetProperty(ref _layerName, value, () => Clear());
    }

    public IReadOnlyList<PlotInfoPriceChart> RealTimePrices => _realTimePrices?.AsReadOnly();
    public IEnumerable<OrderBookLevel> RealTimeOrderLevelsAsk { get; private set; }

    public IEnumerable<OrderBookLevel> RealTimeOrderLevelsBid { get; private set; }

    public ReadOnlyCollection<PlotInfoPriceChart> RealTimeSpread => _realTimeSpread?.AsReadOnly();
    public ObservableCollection<Provider> Providers { get; }

    public BookItemPriceSplit BidTOB_SPLIT
    {
        get => _BidTOB_SPLIT;
        set => SetProperty(ref _BidTOB_SPLIT, value);
    }

    public BookItemPriceSplit AskTOB_SPLIT
    {
        get => _AskTOB_SPLIT;
        set => SetProperty(ref _AskTOB_SPLIT, value);
    }

    public double MidPoint
    {
        get => _MidPoint;
        set => SetProperty(ref _MidPoint, value);
    }

    public double LOBImbalanceValue => OrderBook?.ImbalanceValue ?? 0;

    public BookItem AskTOB
    {
        get => _AskTOB;
        set => SetProperty(ref _AskTOB, value);
    }

    public BookItem BidTOB
    {
        get => _BidTOB;
        set => SetProperty(ref _BidTOB, value);
    }

    public double Spread
    {
        get => _Spread;
        set => SetProperty(ref _Spread, value);
    }

    public double ChartPercentageWidth
    {
        get => _PercentageWidth;
        set => SetProperty(ref _PercentageWidth, value);
    }

    public ReadOnlyCollection<BookItem> AskCummulative => OrderBook?.AskCummulative;
    public ReadOnlyCollection<BookItem> BidCummulative => OrderBook?.BidCummulative;

    public ICollectionView Asks { get; }
    public ICollectionView Bids { get; }
    public ICollectionView Depth { get; }


    public ObservableCollection<Trade> Trades
    {
        get => _realTimeTrades;
        set => SetProperty(ref _realTimeTrades, value);
    }

    public double RealTimeYAxisMinimum { get; private set; }

    public double RealTimeYAxisMaximum { get; private set; }

    public double DepthChartMaxY { get; private set; }

    public int SwitchView
    {
        get => _switchView;
        set => SetProperty(ref _switchView, value);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void SetSortDescriptions()
    {
        Asks.SortDescriptions.Add(new SortDescription("Price", ListSortDirection.Ascending));
        Bids.SortDescriptions.Add(new SortDescription("Price", ListSortDirection.Descending));
        Depth.SortDescriptions.Add(new SortDescription("Price", ListSortDirection.Descending));
    }

    ~vmOrderBook()
    {
        Dispose(false);
    }

    private void uiUpdaterAction()
    {
        lock (MTX_ORDERBOOK)
        {
            _AskTOB_SPLIT?.RaiseUIThread();
            _BidTOB_SPLIT?.RaiseUIThread();

            RaisePropertyChanged(nameof(MidPoint));
            RaisePropertyChanged(nameof(Spread));

            BidAskGridUpdate();

            RaisePropertyChanged(nameof(AskCummulative));
            RaisePropertyChanged(nameof(BidCummulative));

            CalculateMaximumCummulativeSizeOnBothSides();
            RaisePropertyChanged(nameof(DepthChartMaxY));


            RaisePropertyChanged(nameof(RealTimeOrderLevelsAsk));
            RaisePropertyChanged(nameof(RealTimeOrderLevelsBid));

            RaisePropertyChanged(nameof(RealTimePrices));
            RaisePropertyChanged(nameof(RealTimeSpread));


            RaisePropertyChanged(nameof(LOBImbalanceValue));
            RaisePropertyChanged(nameof(RealTimeYAxisMinimum));
            RaisePropertyChanged(nameof(RealTimeYAxisMaximum));
        }
    }

    private void Clear()
    {
        lock (MTX_ORDERBOOK)
        {
            MidPoint = 0;
            AskTOB = new BookItem();
            BidTOB = new BookItem();
            Spread = 0;

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                _bidsGrid.Clear();
                _asksGrid.Clear();
            }));

            _AskTOB_SPLIT.Clear();
            _BidTOB_SPLIT.Clear();
            OrderBook = new OrderBook();
            _realTimePrices = new AggregatedCollection<PlotInfoPriceChart>(_AGG_LEVEL_CHARTS, _MAX_CHART_POINTS,
                x => x.Date, _realTimePrices_OnAggregate);
            _realTimeSpread = new AggregatedCollection<PlotInfoPriceChart>(_AGG_LEVEL_CHARTS, _MAX_CHART_POINTS,
                x => x.Date, _realTimePrices_OnAggregate);
            _realTimeTrades = new ObservableCollection<Trade>();
            RealTimeOrderLevelsAsk = new List<OrderBookLevel>();
            RealTimeOrderLevelsBid = new List<OrderBookLevel>();
            _maxOrderSize = 0; //reset
            _minOrderSize = 0; //reset
            RealTimeYAxisMaximum = 0;
            RealTimeYAxisMinimum = 0;
            DepthChartMaxY = 0;

            RaisePropertyChanged(nameof(AskCummulative));
            RaisePropertyChanged(nameof(BidCummulative));
            RaisePropertyChanged(nameof(Asks));
            RaisePropertyChanged(nameof(Bids));
            RaisePropertyChanged(nameof(RealTimePrices));
            RaisePropertyChanged(nameof(RealTimeSpread));
            RaisePropertyChanged(nameof(Trades));
        }
    }

    private void _realTimePrices_OnAggregate(PlotInfoPriceChart existing, PlotInfoPriceChart newItem)
    {
        // Update the existing bucket with the new values
        existing.Volume = newItem.Volume;
        existing.MidPrice = newItem.MidPrice;
        existing.BidPrice = newItem.BidPrice;
        existing.AskPrice = newItem.AskPrice;
        existing.BuyActiveOrder = newItem.BuyActiveOrder;
        existing.SellActiveOrder = newItem.SellActiveOrder;
    }

    private void ACTIVEORDERS_OnDataRemoved(object sender, Order e)
    {
        if (_selectedProvider == null || string.IsNullOrEmpty(_selectedSymbol) ||
            _selectedProvider.ProviderCode != e.ProviderId)
            return;
        if (string.IsNullOrEmpty(_selectedSymbol) || _selectedSymbol == "-- All symbols --")
            return;

        lock (MTX_ORDERBOOK)
        {
            if (OrderBook != null)
            {
                var comp = 1.0 / Math.Pow(10, e.SymbolDecimals);
                var o = OrderBook.Asks.Where(x => x.Price.HasValue && Math.Abs(x.Price.Value - e.PricePlaced) < comp)
                    .FirstOrDefault();
                if (o == null)
                    o = OrderBook.Bids.Where(x => x.Price.HasValue && Math.Abs(x.Price.Value - e.PricePlaced) < comp)
                        .FirstOrDefault();

                if (o != null)
                {
                    if (o.ActiveSize != null && o.ActiveSize - e.Quantity > 0)
                        o.ActiveSize -= e.Quantity;
                    else
                        o.ActiveSize = null;
                }
            }
        }
    }

    private void ACTIVEORDERS_OnDataReceived(object sender, Order e)
    {
        if (_selectedProvider == null || string.IsNullOrEmpty(_selectedSymbol) ||
            _selectedProvider.ProviderCode != e.ProviderId)
            return;
        if (string.IsNullOrEmpty(_selectedSymbol) || _selectedSymbol == "-- All symbols --")
            return;
        lock (MTX_ORDERBOOK)
        {
            if (OrderBook != null)
            {
                var comp = 1.0 / Math.Pow(10, e.SymbolDecimals);

                var o = OrderBook.Asks.Where(x => x.Price.HasValue && Math.Abs(x.Price.Value - e.PricePlaced) < comp)
                    .FirstOrDefault();
                if (o == null)
                    o = OrderBook.Bids.Where(x => x.Price.HasValue && Math.Abs(x.Price.Value - e.PricePlaced) < comp)
                        .FirstOrDefault();

                if (o != null)
                {
                    if (o.ActiveSize != null)
                        o.ActiveSize += e.Quantity;
                    else
                        o.ActiveSize = e.Quantity;
                }
            }
        }
    }

    private void LIMITORDERBOOK_OnDataReceived(OrderBook e)
    {
        if (e == null)
            return;
        if (_selectedProvider == null || string.IsNullOrEmpty(_selectedSymbol) ||
            _selectedProvider.ProviderCode != e.ProviderID)
            return;
        if (string.IsNullOrEmpty(_selectedSymbol) || _selectedSymbol == "-- All symbols --" ||
            _selectedSymbol != e.Symbol)
            return;

        lock (MTX_ORDERBOOK)
        {
            if (OrderBook == null || OrderBook.ProviderID != e.ProviderID || OrderBook.Symbol != e.Symbol)
            {
                _maxOrderSize = 0; //reset
                _minOrderSize = 0; //reset
                OrderBook = e;
                OrderBook.DecimalPlaces = e.DecimalPlaces;
                OrderBook.SymbolMultiplier = e.SymbolMultiplier;

                _realTimePrices = new AggregatedCollection<PlotInfoPriceChart>(_AGG_LEVEL_CHARTS, _MAX_CHART_POINTS,
                    x => x.Date, _realTimePrices_OnAggregate);
                _realTimeSpread = new AggregatedCollection<PlotInfoPriceChart>(_AGG_LEVEL_CHARTS, _MAX_CHART_POINTS,
                    x => x.Date, _realTimePrices_OnAggregate);
                RealTimeOrderLevelsAsk = new List<OrderBookLevel>();
                RealTimeOrderLevelsBid = new List<OrderBookLevel>();
                _AskTOB_SPLIT.Clear();
                _BidTOB_SPLIT.Clear();
            }

            if (!OrderBook.LoadData(e.Asks, e.Bids))
                return; //if nothing to update, then exit


            #region Calculate TOB values

            var tobBid = OrderBook?.GetTOB(true);
            var tobAsk = OrderBook?.GetTOB(false);
            _MidPoint = OrderBook != null ? OrderBook.MidPrice : 0;
            _Spread = OrderBook != null ? OrderBook.Spread : 0;

            if (tobAsk != null && (tobAsk.Price != _AskTOB.Price || tobAsk.Size != _AskTOB.Size))
            {
                AskTOB = tobAsk;
                if (tobAsk.Price.HasValue && tobAsk.Size.HasValue)
                    _AskTOB_SPLIT.SetNumber(tobAsk.Price.Value, tobAsk.Size.Value, OrderBook.DecimalPlaces);
            }

            if (tobBid != null && (tobBid.Price != _BidTOB.Price || tobBid.Size != _BidTOB.Size))
            {
                BidTOB = tobBid;
                if (tobBid.Price.HasValue && tobBid.Size.HasValue)
                    _BidTOB_SPLIT.SetNumber(tobBid.Price.Value, tobBid.Size.Value, OrderBook.DecimalPlaces);
            }

            #endregion

            #region REAL TIME PRICES

            if (_realTimePrices != null && tobAsk != null && tobBid != null)
            {
                var maxDateIncoming = DateTime.Now; // Max(tobAsk.LocalTimeStamp, tobBid.LocalTimeStamp);

                var objToAdd = _realTimePrices.GetObjectPool().Get();
                objToAdd.Date = maxDateIncoming;
                objToAdd.MidPrice = MidPoint;
                objToAdd.AskPrice = tobAsk.Price.Value;
                objToAdd.BidPrice = tobBid.Price.Value;
                objToAdd.Volume = tobAsk.Size.Value + tobBid.Size.Value;


                if (HelperCommon.ACTIVEORDERS.Any(x =>
                        x.Value.ProviderId == _selectedProvider.ProviderCode && x.Value.Symbol == _selectedSymbol))
                {
                    objToAdd.BuyActiveOrder = HelperCommon.ACTIVEORDERS.Where(x => x.Value.Side == eORDERSIDE.Buy)
                        .Select(x => x.Value).DefaultIfEmpty(new Order()).OrderByDescending(x => x.PricePlaced)
                        .FirstOrDefault().PricePlaced;
                    objToAdd.SellActiveOrder = HelperCommon.ACTIVEORDERS.Where(x => x.Value.Side == eORDERSIDE.Sell)
                        .Select(x => x.Value).DefaultIfEmpty(new Order()).OrderBy(x => x.PricePlaced).FirstOrDefault()
                        .PricePlaced;
                    objToAdd.BuyActiveOrder = objToAdd.BuyActiveOrder == 0 ? null : objToAdd.BuyActiveOrder;
                    objToAdd.SellActiveOrder = objToAdd.SellActiveOrder == 0 ? null : objToAdd.SellActiveOrder;
                }

                #region Resting Orders at different levels [SCATTER BUBBLES]

                var sizeMinMax = OrderBook.GetMinMaxSizes();
                _minOrderSize = Math.Min(sizeMinMax.Item1, _minOrderSize);
                _maxOrderSize = Math.Max(sizeMinMax.Item2, _maxOrderSize);

                double minBubbleSize = 1; // Minimum size for bubbles in pixels
                double maxBubbleSize = 10; // Maximum size for bubbles in pixels
                if (_realTimePrices.Count() >= _MAX_CHART_POINTS - 10)
                {
                    if (OrderBook.Bids != null)
                        foreach (var bid in OrderBook.Bids)
                            if (bid.Price.HasValue && bid.Size.HasValue)
                                objToAdd.BidLevelOrders.Add(new OrderBookLevel
                                {
                                    Date = objToAdd.Date,
                                    Price = bid.Price.Value,
                                    Size = minBubbleSize + (bid.Size.Value - _minOrderSize) /
                                        (_maxOrderSize - _minOrderSize) * (maxBubbleSize - minBubbleSize)
                                });
                    if (OrderBook.Asks != null)
                        foreach (var ask in OrderBook.Asks)
                            if (ask.Price.HasValue && ask.Size.HasValue)
                                objToAdd.AskLevelOrders.Add(new OrderBookLevel
                                {
                                    Date = objToAdd.Date,
                                    Price = ask.Price.Value,
                                    Size = minBubbleSize + (ask.Size.Value - _minOrderSize) /
                                        (_maxOrderSize - _minOrderSize) * (maxBubbleSize - minBubbleSize)
                                });
                }

                #endregion

                _realTimePrices.Add(objToAdd);
                RealTimeOrderLevelsAsk = _realTimePrices.SelectMany(x => x.AskLevelOrders);
                RealTimeOrderLevelsBid = _realTimePrices.SelectMany(x => x.BidLevelOrders);


                //calculate min/max axis
                RealTimeYAxisMinimum =
                    _realTimePrices.Min(x => Math.Min(x.AskPrice, x.BidPrice)) * 0.9999; // midPrice * 0.9; //-20%
                RealTimeYAxisMaximum =
                    _realTimePrices.Max(x => Math.Max(x.AskPrice, x.BidPrice)) * 1.0001; // midPrice * 1.1; //+20%
            }

            #endregion

            #region REAL TIME SPREADS

            if (_realTimeSpread != null)
            {
                var lastAddedFromPrice = _realTimePrices.LastOrDefault();
                var lastSpread = _realTimeSpread.LastOrDefault();
                if (AskTOB != null && BidTOB != null && lastAddedFromPrice != null)
                    if (lastSpread == null || lastSpread.Date != lastAddedFromPrice.Date)
                    {
                        var _spreadPlot = _realTimeSpread.GetObjectPool().Get();
                        _spreadPlot.Date = lastAddedFromPrice.Date;
                        _spreadPlot.MidPrice = Spread;
                        _realTimeSpread.Add(_spreadPlot);
                    }
            }

            #endregion
        }
    }

    private void BidAskGridUpdate()
    {
        if (_selectedProvider == null || string.IsNullOrEmpty(_selectedSymbol))
            return;
        if (string.IsNullOrEmpty(_selectedSymbol) || _selectedSymbol == "-- All symbols --")
            return;

        if (_bidsGrid == null && OrderBook != null && OrderBook.Bids != null)
        {
            _bidsGrid.Clear();
            RaisePropertyChanged(nameof(Bids));
        }
        else if (OrderBook != null && OrderBook.Bids != null)
        {
            OrderBook.GetAddDeleteUpdate(ref _bidsGrid, OrderBook.Bids);
        }

        if (_asksGrid == null && OrderBook != null && OrderBook.Asks != null)
        {
            _asksGrid.Clear();
            RaisePropertyChanged(nameof(Asks));
        }
        else if (OrderBook != null && OrderBook.Asks != null)
        {
            OrderBook.GetAddDeleteUpdate(ref _asksGrid, OrderBook.Asks);
        }

        if (_asksGrid != null && _bidsGrid != null)
        {
            _depthGrid.Clear();
            foreach (var item in _asksGrid)
                _depthGrid.Add(item);
            foreach (var item in _bidsGrid)
                _depthGrid.Add(item);
        }
    }

    private void TRADES_OnDataReceived(VisualHFT.Model.Trade e)
    {
        if (e == null)
            return;
        if (_selectedProvider == null || string.IsNullOrEmpty(_selectedSymbol) ||
            _selectedProvider.ProviderCode != e.ProviderId)
            return;
        if (string.IsNullOrEmpty(_selectedSymbol) || _selectedSymbol == "-- All symbols --" ||
            _selectedSymbol != e.Symbol)
            return;


        if (_realTimeTrades != null)
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                _realTimeTrades.Insert(0, new Trade(e));
                while (_realTimeTrades.Count > 100)
                    _realTimeTrades.RemoveAt(_realTimeTrades.Count - 1);
            }));
    }

    private void PROVIDERS_OnDataReceived(object? sender, VisualHFT.Model.Provider e)
    {
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            var item = new Provider(e);
            if (!Providers.Any(x => x.ProviderCode == e.ProviderCode))
                Providers.Add(item);
            //if nothing is selected
            if (_selectedProvider == null) //default provider must be the first who's Active
                SelectedProvider = item;
        }));
    }

    private void PROVIDERS_OnHeartBeatFail(object? sender, VisualHFT.Model.Provider e)
    {
        if (_selectedProvider != null && e.ProviderCode == _selectedProvider.ProviderCode &&
            _selectedProvider.Status != e.Status && (e.Status == eSESSIONSTATUS.PRICE_DSICONNECTED_ORDER_CONNECTED ||
                                                     e.Status == eSESSIONSTATUS.BOTH_DISCONNECTED))
        {
            _selectedProvider.Status = e.Status;
            Clear();
        }
    }

    private void CalculateMaximumCummulativeSizeOnBothSides()
    {
        var _maxValueAsks = OrderBook?.AskCummulative?.DefaultIfEmpty(new BookItem { Size = 0 }).Max(x => x.Size.Value);
        if (!_maxValueAsks.HasValue) _maxValueAsks = 0;
        var _maxValueBids = OrderBook?.BidCummulative?.DefaultIfEmpty(new BookItem { Size = 0 }).Max(x => x.Size.Value);
        if (!_maxValueBids.HasValue) _maxValueBids = 0;

        DepthChartMaxY = Math.Max(_maxValueBids.Value, _maxValueAsks.Value);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                uiUpdater.Dispose();
                HelperProvider.Instance.OnDataReceived -= PROVIDERS_OnDataReceived;
                HelperProvider.Instance.OnHeartBeatFail -= PROVIDERS_OnHeartBeatFail;
                HelperCommon.ACTIVEORDERS.OnDataReceived -= ACTIVEORDERS_OnDataReceived;
                HelperCommon.ACTIVEORDERS.OnDataRemoved -= ACTIVEORDERS_OnDataRemoved;
                HelperTrade.Instance.Unsubscribe(TRADES_OnDataReceived);
                HelperOrderBook.Instance.Unsubscribe(LIMITORDERBOOK_OnDataReceived);

                OrderBook?.Dispose();
                _dialogs = null;
                _realTimePrices?.Clear();
                _realTimeSpread?.Clear();
                _realTimeTrades?.Clear();
                Providers?.Clear();
            }

            _disposed = true;
        }
    }
}