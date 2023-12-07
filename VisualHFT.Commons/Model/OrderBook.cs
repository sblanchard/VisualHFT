using System.Collections.ObjectModel;
using VisualHFT.Commons.Pools;
using VisualHFT.Helpers;
using VisualHFT.Studies;

namespace VisualHFT.Model;

public class OrderBook : ICloneable, IDisposable
{
    private readonly ObjectPool<BookItem> _cummObjectPool = new();


    private readonly ObjectPool<OrderBook> orderBookPool = new();

    protected CachedCollection<BookItem> _Asks;
    private BookItem _askTOP;
    protected CachedCollection<BookItem> _Bids;
    private BookItem _bidTOP;
    private CachedCollection<BookItem> _Cummulative_Asks;

    private CachedCollection<BookItem> _Cummulative_Bids;
    private bool _disposed; // to track whether the object has been disposed

    private readonly OrderFlowAnalysis lobMetrics = new();

    private readonly object LOCK_OBJECT = new();

    public OrderBook() //emtpy constructor for JSON deserialization
    {
        _Cummulative_Asks = new CachedCollection<BookItem>();
        _Cummulative_Bids = new CachedCollection<BookItem>();
        _Bids = new CachedCollection<BookItem>();
        _Asks = new CachedCollection<BookItem>();
    }

    public OrderBook(string symbol, int decimalPlaces)
    {
        _Cummulative_Asks = new CachedCollection<BookItem>();
        _Cummulative_Bids = new CachedCollection<BookItem>();
        _Bids = new CachedCollection<BookItem>();
        _Asks = new CachedCollection<BookItem>();

        Symbol = symbol;
        DecimalPlaces = decimalPlaces;
    }

    public ReadOnlyCollection<BookItem> Asks
    {
        get => _Asks.ToList().AsReadOnly();
        set => _Asks.Update(value); //do not remove setter: it is used to auto parse json
    }

    public ReadOnlyCollection<BookItem> Bids
    {
        get => _Bids.ToList().AsReadOnly();
        set => _Bids.Update(value); //do not remove setter: it is used to auto parse json
    }

    public ReadOnlyCollection<BookItem> BidCummulative
    {
        get
        {
            lock (LOCK_OBJECT)
            {
                return _Cummulative_Bids.ToList().AsReadOnly();
            }
        }
    }

    public ReadOnlyCollection<BookItem> AskCummulative
    {
        get
        {
            lock (LOCK_OBJECT)
            {
                return _Cummulative_Asks.ToList().AsReadOnly();
            }
        }
    }

    public string Symbol { get; set; }

    public int DecimalPlaces { get; set; }

    public double SymbolMultiplier { get; set; }

    public int ProviderID { get; set; }

    public string ProviderName { get; set; }

    public eSESSIONSTATUS ProviderStatus { get; set; }

    public double ImbalanceValue { get; set; }
    public double MidPrice { get; private set; }

    public double Spread { get; private set; }

    public object Clone()
    {
        var clone = orderBookPool.Get(); // Get a pooled instance instead of creating a new one
        clone.DecimalPlaces = DecimalPlaces;
        clone.ProviderID = ProviderID;
        clone.ProviderName = ProviderName;
        clone.Symbol = Symbol;
        clone.SymbolMultiplier = SymbolMultiplier;
        clone.ImbalanceValue = ImbalanceValue;
        clone.ProviderStatus = ProviderStatus;

        clone.LoadData(Asks, Bids);
        return clone;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~OrderBook()
    {
        Dispose(false);
    }

    public void GetAddDeleteUpdate(ref ObservableCollection<BookItem> inputExisting,
        ReadOnlyCollection<BookItem> listToMatch)
    {
        var inputNew = listToMatch;
        List<BookItem> outAdds;
        List<BookItem> outUpdates;
        List<BookItem> outRemoves;

        var existingSet = inputExisting; // new HashSet<BookItem>(inputExisting);
        var newSet = inputNew; // new HashSet<BookItem>(inputNew);

        outRemoves = inputExisting.Where(e => !newSet.Contains(e)).ToList();
        outUpdates = inputNew
            .Where(e => existingSet.Contains(e) && e.Size != existingSet.FirstOrDefault(i => i.Equals(e)).Size)
            .ToList();
        outAdds = inputNew.Where(e => !existingSet.Contains(e)).ToList();

        foreach (var b in outRemoves)
            inputExisting.Remove(b);
        foreach (var b in outUpdates)
        {
            var itemToUpd = inputExisting.Where(x => x.Price == b.Price).FirstOrDefault();
            if (itemToUpd != null)
            {
                itemToUpd.Size = b.Size;
                itemToUpd.ActiveSize = b.ActiveSize;
                itemToUpd.LocalTimeStamp = b.LocalTimeStamp;
                itemToUpd.ServerTimeStamp = b.ServerTimeStamp;
            }
        }

        foreach (var b in outAdds)
            inputExisting.Add(b);
    }

    private void CalculateMetrics()
    {
        lobMetrics.LoadData(_Asks, _Bids);
        ImbalanceValue = lobMetrics.Calculate_OrderImbalance();
    }

    public void Clear()
    {
        lock (LOCK_OBJECT)
        {
            _Cummulative_Asks?.Clear();
            _Cummulative_Bids?.Clear();
            _Bids?.Clear();
            _Asks?.Clear();
        }
    }

    public bool LoadData()
    {
        return LoadData(Asks, Bids);
    }

    public bool LoadData(IEnumerable<BookItem> asks, IEnumerable<BookItem> bids)
    {
        var ret = true;
        lock (LOCK_OBJECT)
        {
            #region Bids

            if (bids != null)
                _Bids.Update(bids
                    .Where(x => x != null && x.Price.HasValue && x.Size.HasValue)
                    .OrderByDescending(x => x.Price.Value)
                );

            foreach (var item in _Cummulative_Bids)
                _cummObjectPool.Return(item);
            _Cummulative_Bids.Clear();

            double cumSize = 0;
            foreach (var o in _Bids)
            {
                cumSize += o.Size.Value;
                var _item = _cummObjectPool.Get();
                _item.Price = o.Price;
                _item.Size = cumSize;
                _item.IsBid = true;
                _Cummulative_Bids.Add(_item);
            }

            #endregion

            #region Asks

            if (asks != null)
                _Asks.Update(asks
                    .Where(x => x != null && x.Price.HasValue && x.Size.HasValue)
                    .OrderBy(x => x.Price.Value)
                );

            foreach (var item in _Cummulative_Asks)
                _cummObjectPool.Return(item);
            _Cummulative_Asks.Clear();
            cumSize = 0;
            foreach (var o in _Asks)
            {
                cumSize += o.Size.Value;
                var _item = _cummObjectPool.Get();
                _item.Price = o.Price;
                _item.Size = cumSize;
                _item.IsBid = false;
                _Cummulative_Asks.Add(_item);
            }

            #endregion

            _bidTOP = _Bids.FirstOrDefault();
            _askTOP = _Asks.FirstOrDefault();
            if (_bidTOP != null && _bidTOP.Price.HasValue && _askTOP != null && _askTOP.Price.HasValue)
            {
                MidPrice = (_bidTOP.Price.Value + _askTOP.Price.Value) / 2;
                Spread = _askTOP.Price.Value - _bidTOP.Price.Value;
            }

            CalculateMetrics();
        }

        return ret;
    }

    public BookItem GetTOB(bool isBid)
    {
        lock (LOCK_OBJECT)
        {
            if (isBid)
                return _bidTOP;
            return _askTOP;
        }
    }

    public double GetMaxOrderSize()
    {
        double _maxOrderSize = 0;

        lock (LOCK_OBJECT)
        {
            if (_Bids != null)
                _maxOrderSize = _Bids.Where(x => x.Size.HasValue).DefaultIfEmpty(new BookItem()).Max(x => x.Size.Value);
            if (_Asks != null)
                _maxOrderSize = Math.Max(_maxOrderSize,
                    _Asks.Where(x => x.Size.HasValue).DefaultIfEmpty(new BookItem()).Max(x => x.Size.Value));
        }

        return _maxOrderSize;
    }

    public Tuple<double, double> GetMinMaxSizes()
    {
        var allOrders = new List<BookItem>();

        lock (LOCK_OBJECT)
        {
            if (_Bids != null)
                allOrders.AddRange(_Bids.Where(x => x.Size.HasValue).ToList());
            if (_Asks != null)
                allOrders.AddRange(_Asks.Where(x => x.Size.HasValue).ToList());
        }

        //AVOID OUTLIERS IN SIZES (when data is invalid)
        var firstQuantile = allOrders.Select(x => x.Size.Value).Quantile(0.25);
        var thirdQuantile = allOrders.Select(x => x.Size.Value).Quantile(0.75);
        var iqr = thirdQuantile - firstQuantile;
        var lowerBand = firstQuantile - 1.5 * iqr;
        var upperBound = thirdQuantile + 1.5 * iqr;

        var minOrderSize = allOrders.Where(x => x.Size >= lowerBand).Min(x => x.Size.Value);
        var maxOrderSize = allOrders.Where(x => x.Size <= upperBound).Max(x => x.Size.Value);

        return Tuple.Create(minOrderSize, maxOrderSize);
    }

    public void CopyTo(OrderBook target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));

        target.DecimalPlaces = DecimalPlaces;
        target.ProviderID = ProviderID;
        target.ProviderName = ProviderName;
        target.Symbol = Symbol;
        target.SymbolMultiplier = SymbolMultiplier;
        target.ImbalanceValue = ImbalanceValue;
        target.ProviderStatus = ProviderStatus;

        target.LoadData(Asks, Bids);
    }


    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _Cummulative_Asks?.Clear();
                _Cummulative_Bids?.Clear();
                _Bids?.Clear();
                _Asks?.Clear();


                _Cummulative_Asks = null;
                _Cummulative_Bids = null;
                _Bids = null;
                _Asks = null;

                _bidTOP = null;
                _askTOP = null;

                orderBookPool.Return(this); // Return to the pool
            }

            _disposed = true;
        }
    }
}