using VisualHFT.Commons.Pools;
using VisualHFT.Helpers;
using VisualHFT.Model;

namespace VisualHFT.Commons.Model
{
    public class OrderBookSnapshot : IDisposable
    {
        private CustomObjectPool<BookItem> _bookItemPool = new CustomObjectPool<BookItem>(maxPoolSize: 200);
        private List<BookItem> _asks;
        private List<BookItem> _bids;
        private string _symbol;
        private int _priceDecimalPlaces;
        private int _sizeDecimalPlaces;
        private int _providerId;
        private string _providerName;
        private int _maxDepth;
        private double _imbalanceValue;
        private DateTime _lastUpdated;

        public List<BookItem> Asks
        {
            get
            {
                return _asks;
            }
            private set { _asks = value; }
        }

        public List<BookItem> Bids
        {
            get { return _bids; }
            private set
            {
                _bids = value;
            }
        }

        public string Symbol
        {
            get => _symbol;
            set => _symbol = value;
        }

        public int PriceDecimalPlaces
        {
            get => _priceDecimalPlaces;
            set => _priceDecimalPlaces = value;
        }

        public int SizeDecimalPlaces
        {
            get => _sizeDecimalPlaces;
            set => _sizeDecimalPlaces = value;
        }

        public double SymbolMultiplier => Math.Pow(10, PriceDecimalPlaces);

        public int ProviderID
        {
            get => _providerId;
            set => _providerId = value;
        }

        public string ProviderName
        {
            get => _providerName;
            set => _providerName = value;
        }

        public int MaxDepth
        {
            get => _maxDepth;
            set => _maxDepth = value;
        }

        public double ImbalanceValue
        {
            get => _imbalanceValue;
            set => _imbalanceValue = value;
        }


        // Constructor creates new subcollections.
        public OrderBookSnapshot()
        {
            Bids = new List<BookItem>();
            Asks = new List<BookItem>();
        }

        // Update the snapshot from the master OrderBook.
        public void UpdateFrom(OrderBook master)
        {
            this.Symbol = master.Symbol;
            this.ProviderID = master.ProviderID;
            this.ProviderName = master.ProviderName;
            this.PriceDecimalPlaces = master.PriceDecimalPlaces;
            this.SizeDecimalPlaces = master.SizeDecimalPlaces;
            this.MaxDepth = master.MaxDepth;
            this.ImbalanceValue = master.ImbalanceValue;
            LastUpdated = HelperTimeProvider.Now;
            CopyBookItems(master.Asks, _asks);
            CopyBookItems(master.Bids, _bids);
        }

        private void CopyBookItems(CachedCollection<BookItem> from, List<BookItem> to)
        {
            ClearBookItems(to); //reset before copying
            foreach (var bookItem in from)
            {
                var _item = _bookItemPool.Get();
                _item.CopyFrom(bookItem);
                to.Add(_item);
            }
        }
        public BookItem GetTOB(bool isBid)
        {
            if (isBid)
            {
                return _bids?.FirstOrDefault();
            }
            else
            {
                return _asks?.FirstOrDefault();
            }
        }
        public double MidPrice
        {
            get
            {
                var _bidTOP = GetTOB(true);
                var _askTOP = GetTOB(false);
                if (_bidTOP != null && _bidTOP.Price.HasValue && _askTOP != null && _askTOP.Price.HasValue)
                {
                    return (_bidTOP.Price.Value + _askTOP.Price.Value) / 2.0;
                }
                return 0;
            }
        }
        public double Spread
        {
            get
            {
                var _bidTOP = GetTOB(true);
                var _askTOP = GetTOB(false);
                if (_bidTOP != null && _bidTOP.Price.HasValue && _askTOP != null && _askTOP.Price.HasValue)
                {
                    return _askTOP.Price.Value - _bidTOP.Price.Value;
                }
                return 0;
            }
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set => _lastUpdated = value;
        }

        public Tuple<double, double> GetMinMaxSizes()
        {
            List<BookItem> allOrders = new List<BookItem>();
            double minVal = 0;
            double maxVal = 0;
            if (Asks == null || Bids == null
                             || Asks.Count == 0
                             || Bids.Count == 0)
                return new Tuple<double, double>(0, 0);
            foreach (var o in _bids)
            {
                if (o.Size.HasValue)
                {
                    minVal = Math.Min(minVal, o.Size.Value);
                    maxVal = Math.Max(maxVal, o.Size.Value);
                }
            }
            foreach (var o in _asks)
            {
                if (o.Size.HasValue)
                {
                    minVal = Math.Min(minVal, o.Size.Value);
                    maxVal = Math.Max(maxVal, o.Size.Value);
                }
            }
            return Tuple.Create(minVal, maxVal);
        }


        private void ClearBookItems(List<BookItem> list)
        {
            _bookItemPool.Return(list);

            list.Clear();
        }
        // Reset the snapshot to a clean state.
        public void Reset()
        {
            ClearBookItems(_asks);
            ClearBookItems(_bids);
        }

        // When disposing, reset internal state and return the snapshot to the pool.
        public void Dispose()
        {
            // Reset the snapshot so that it doesn't hold stale data.
            Reset();
        }
    }

}
