using System;
using System.Collections.Generic;
using System.Linq;
using ExchangeSharp;
using ExchangeOrderPrice = demoTradingCore.Models.Extension.ExchangeOrderPrice;

namespace demoTradingCore.Models
{
    public class OrderBook
    {
        private Dictionary<decimal, ExchangeOrderPrice> _asks;
        private Dictionary<decimal, ExchangeOrderPrice> _bids;
        private int _depthOfBook;
        private long _lastSequenceId;

        public OrderBook(int depth)
        {
            _depthOfBook = depth;
            _asks = new Dictionary<decimal, ExchangeOrderPrice>();
            _bids = new Dictionary<decimal, ExchangeOrderPrice>();
        }

        public void UpdateSnapshot(ExchangeOrderBook ob)
        {
            _lastSequenceId = ob.SequenceId;
            lock (_asks)
            {
                _asks = ob.Asks.ToArray().Select(x => new KeyValuePair<decimal, ExchangeOrderPrice>(x.Key,
                    new ExchangeOrderPrice
                    {
                        Amount = x.Value.Amount,
                        LocalTimestamp = DateTime.Now,
                        Price = x.Value.Price,
                        ServerTimestamp = ob.LastUpdatedUtc.ToLocalTime()
                    })).ToDictionary(x => x.Key, x => x.Value);
            }

            lock (_bids)
            {
                _bids = ob.Bids.ToArray().Select(x => new KeyValuePair<decimal, ExchangeOrderPrice>(x.Key,
                    new ExchangeOrderPrice
                    {
                        Amount = x.Value.Amount,
                        LocalTimestamp = DateTime.Now,
                        Price = x.Value.Price,
                        ServerTimestamp = ob.LastUpdatedUtc.ToLocalTime()
                    })).ToDictionary(x => x.Key, x => x.Value);
            }
        }

        public IEnumerable<ExchangeOrderPrice> GetAsks()
        {
            lock (_asks)
            {
                return _asks.Values.ToList();
            }
        }

        public IEnumerable<ExchangeOrderPrice> GetBids()
        {
            lock (_bids)
            {
                return _bids.Values.ToList();
            }
        }

        public IEnumerable<ExchangeOrderPrice> GetTopOfBook()
        {
            lock (_asks)
            {
                lock (_bids)
                {
                    var b = _bids.OrderBy(x => x.Value.Price).LastOrDefault().Value;
                    var a = _asks.OrderBy(x => x.Value.Price).FirstOrDefault().Value;
                    if (a == null || b == null)
                        return null;
                    return new List<ExchangeOrderPrice> { b, a };
                }
            }
        }
    }
}