﻿using System;
using System.Collections.Generic;
using System.Linq;
using ExchangeSharp;
using ExchangeOrderPrice = demoTradingCore.Models.Extension.ExchangeOrderPrice;

namespace demoTradingCore.Models
{
    public class Exchange
    {
        private readonly eEXCHANGE _exchange;
        private readonly object _orderBooksLCK = new object();

        public Exchange(eEXCHANGE exchange, int depth)
        {
            _exchange = exchange;
            _orderBooks = new Dictionary<string, OrderBook>();
        }

        private Dictionary<string, OrderBook> _orderBooks { get; }

        public string ExchangeName => _exchange.ToString();

        public DateTime? LastUpdated { get; private set; } = DateTime.MinValue;

        public void UpdateSnapshot(string symbol, ExchangeOrderBook ob, int depth)
        {
            lock (_orderBooksLCK)
            {
                if (!_orderBooks.ContainsKey(symbol))
                    _orderBooks.Add(symbol, new OrderBook(depth));
                _orderBooks[symbol].UpdateSnapshot(ob);
                LastUpdated = DateTime.Now;
            }
        }

        public IEnumerable<ExchangeOrderPrice> GetTopOfBook(string symbol)
        {
            lock (_orderBooksLCK)
            {
                if (_orderBooks.ContainsKey(symbol))
                    return _orderBooks[symbol].GetTopOfBook();
                return null;
            }
        }

        public jsonMarkets GetSnapshots()
        {
            var ret = new jsonMarkets();
            ret.dataObj = new List<jsonMarket>();
            lock (_orderBooksLCK)
            {
                foreach (var symbol in _orderBooks.Keys)
                    if (_orderBooks.ContainsKey(symbol))
                    {
                        var _asks = _orderBooks[symbol].GetAsks();
                        var _bids = _orderBooks[symbol].GetBids();
                        if (_asks.Any() || _bids.Any())
                        {
                            var m = new jsonMarket();
                            m.Symbol = symbol;
                            m.ProviderId = (int)_exchange;
                            m.ProviderName = _exchange.ToString();
                            m.ProviderStatus = 2; //conected
                            m.SymbolMultiplier = 1;
                            m.DecimalPlaces = CalculateDecimalPlaces(_asks);
                            m.Bids = _bids.Select(x => new jsonBookItem
                            {
                                DecimalPlaces = m.DecimalPlaces,
                                EntryID = (int)x.Price * 10 * m.DecimalPlaces,
                                IsBid = true,
                                LayerName = "",
                                LocalTimeStamp = x.LocalTimestamp,
                                Price = x.Price,
                                ProviderID = m.ProviderId,
                                ServerTimeStamp = x.ServerTimestamp,
                                Size = x.Amount,
                                Symbol = m.Symbol
                            }).ToList();
                            m.Asks = _asks.Select(x => new jsonBookItem
                            {
                                DecimalPlaces = m.DecimalPlaces,
                                EntryID = (int)x.Price * 10 * m.DecimalPlaces,
                                IsBid = false,
                                LayerName = "",
                                LocalTimeStamp = x.LocalTimestamp,
                                Price = x.Price,
                                ProviderID = m.ProviderId,
                                ServerTimeStamp = x.ServerTimestamp,
                                Size = x.Amount,
                                Symbol = m.Symbol
                            }).ToList();
                            ret.dataObj.Add(m);
                        }
                    }
            }

            return ret;
        }

        private int CalculateDecimalPlaces(IEnumerable<ExchangeOrderPrice> _prices)
        {
            if (_prices != null && _prices.Any())
            {
                var priceSample = _prices
                    .Where(x =>
                            x.Price.ToString().IndexOf(".") > -1 //has decimal
                            && Convert.ToInt32(x.Price.ToString().Split('.')[1]) > 0 // The decimal part must be > 0
                    )
                    .FirstOrDefault();
                if (priceSample != null)
                {
                    var strFirst = priceSample.Price.ToString();
                    var decimalPlaces = strFirst.Length - strFirst.IndexOf('.') - 1;

                    return Math.Max(decimalPlaces, 2);
                }
            }

            return 2; //default
        }
    }
}