using System;
using System.Linq;
using VisualHFT.Model;

namespace Studies.MarketResilience.Model
{
    public enum eMarketBias
    {
        Neutral,
        Bullish,
        Bearish
    }
    public class MarketResilienceWithBias : MarketResilienceCalculator
    {
        private DateTime? bidDepletionTime = null;
        private DateTime? askDepletionTime = null;

        private bool isBidSideDepleted = false;
        private bool isAskSideDepleted = false;

        private decimal recentBestBidPrice = 0m;
        private decimal recentBestAskPrice = 0m;

        public eMarketBias CurrentMarketBias { get; private set; } = eMarketBias.Neutral;

        public override void Reset()
        {
            bidDepletionTime = null;
            askDepletionTime = null;

            isBidSideDepleted = false;
            isAskSideDepleted = false;

            recentBestBidPrice = 0m;
            recentBestAskPrice = 0m;

            base.Reset();
        }

        public override void OnOrderBookUpdate(OrderBook orderBook)
        {
            base.OnOrderBookUpdate(orderBook);

            lock (_syncLock)
            {
                var currentBestBid = (decimal?)orderBook.Bids.FirstOrDefault()?.Price ?? recentBestBidPrice;
                var currentBestAsk = (decimal?)orderBook.Asks.FirstOrDefault()?.Price ?? recentBestAskPrice;

                bool spreadShock = IsShock((decimal)orderBook.Spread, recentSpreads, spreadShockThresholdMultiplier, true);

                if (spreadShock && !isBidSideDepleted && !isAskSideDepleted)
                {
                    if (currentBestBid < recentBestBidPrice)
                    {
                        isBidSideDepleted = true;
                        bidDepletionTime = DateTime.UtcNow;
                    }
                    else if (currentBestAsk > recentBestAskPrice)
                    {
                        isAskSideDepleted = true;
                        askDepletionTime = DateTime.UtcNow;
                    }
                }

                TrackReplenishment(orderBook);

                recentBestBidPrice = currentBestBid;
                recentBestAskPrice = currentBestAsk;
            }
        }


        private void TrackReplenishment(OrderBook orderBook)
        {
            if (isBidSideDepleted)
            {
                decimal bidDepthRecovery = orderBook.Bids.Take(3).Sum(b => (decimal)b.Size);
                decimal askDepthStrength = orderBook.Asks.Take(3).Sum(a => (decimal)a.Size);

                if (bidDepthRecovery > askDepthStrength)
                {
                    isBidSideDepleted = false;
                    bidDepletionTime = null;
                }
                else if ((DateTime.UtcNow - bidDepletionTime.Value).TotalMilliseconds > 500)
                {
                    CurrentMarketBias = eMarketBias.Bearish;
                    return;
                }
            }
            else if (isAskSideDepleted)
            {
                decimal askDepthRecovery = orderBook.Asks.Take(3).Sum(a => (decimal)a.Size);
                decimal bidDepthStrength = orderBook.Bids.Take(3).Sum(b => (decimal)b.Size);

                if (askDepthRecovery > bidDepthStrength)
                {
                    isAskSideDepleted = false;
                    askDepletionTime = null;
                }
                else if ((DateTime.UtcNow - askDepletionTime.Value).TotalMilliseconds > 500)
                {
                    CurrentMarketBias = eMarketBias.Bullish;
                    return;
                }
            }
            else
            {
                CurrentMarketBias = eMarketBias.Bullish;
            }
        }
        
    }

}
