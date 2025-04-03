using System;
using VisualHFT.Commons.Model;
using VisualHFT.Commons.Pools;
using VisualHFT.Model;
using VisualHFT.Studies.MarketResilience.Model;

namespace Studies.MarketResilience.Model
{
    public enum eMarketBias
    {
        Neutral,
        Bullish,
        Bearish
    }
    public class MarketResilienceCalculator : IDisposable
    {
        private double MIN_SHOCK_TIME_DIFFERENCE = 800.0; // Min time diff in ms to consider two shock events (trade and spread widening) as related.
        private decimal SPREAD_SHOCK_THRESHOLD_MULTIPLIER = 3m; // Multiplier to identify significant spread widening events as shocks.
        private decimal TRADE_SIZE_SHOCK_THRESHOLD_MULTIPLIER = 3m; // Multiplier to identify significant trade size events as shocks.

        private bool disposed = false;
        private decimal? _lastMidPrice = 0;
        protected decimal? _lastBidPrice;
        protected decimal? _lastAskPrice;
        protected decimal? _bidAtHit;
        protected decimal? _askAtHit;
        protected readonly object _syncLock = new object();

        protected RollingWindow<decimal> recentSpreads = new RollingWindow<decimal>(500);
        private RollingWindow<decimal> recentTradeSizes = new RollingWindow<decimal>(500);
        private RollingWindow<double> spreadRecoveryTimes = new RollingWindow<double>(100);
        private PlugInSettings settings;

        public MarketResilienceCalculator(PlugInSettings settings)
        {
            this.settings = settings;
            
            MIN_SHOCK_TIME_DIFFERENCE = settings.MinShockTimeDifference ?? MIN_SHOCK_TIME_DIFFERENCE;
            SPREAD_SHOCK_THRESHOLD_MULTIPLIER = settings.SpreadShockThresholdMultiplier ?? SPREAD_SHOCK_THRESHOLD_MULTIPLIER;
            TRADE_SIZE_SHOCK_THRESHOLD_MULTIPLIER = settings.TradeSizeShockThresholdMultiplier ?? TRADE_SIZE_SHOCK_THRESHOLD_MULTIPLIER;
        }

        private class TimestampedValue
        {
            public DateTime Timestamp { get; set; }
            public decimal Value { get; set; }
        }



        private TimestampedValue? ShockTrade { get; set; }      // holds the shock trade
        private TimestampedValue? ShockSpread { get; set; }     // holds the shock spread
        private TimestampedValue? ReturnedSpread { get; set; }  // holds the last spread value, which will be used to calculate the MR score when gets back to normal values.
        protected bool? InitialHitHappenedAtBid { get; set; }      // holds the information about the shock trade happened at bid or ask

        public decimal CurrentMRScore { get; private set; } = 1m; // stable MR value by default
        public eMarketBias CurrentMarketBias { get; private set; } = eMarketBias.Neutral;
        public decimal MidMarketPrice => _lastMidPrice ?? 0;

        public void OnTrade(Trade trade)
        {
            lock (_syncLock)
            {
                if (ShockTrade == null
                    && IsLargeTrade(trade.Size))
                {
                    ShockTrade = new TimestampedValue
                    {
                        Timestamp = trade.Timestamp,
                        Value = (decimal)trade.Size
                    };
                    //find out if the shock trade happened closer to bid or ask
                    if (_lastBidPrice.HasValue &&
                        _lastAskPrice.HasValue) //if we have latest bid/ask price we can infer where the trade happened
                    {
                        decimal midPrice = (_lastBidPrice.Value + _lastAskPrice.Value) / 2;
                        InitialHitHappenedAtBid = trade.Price <= midPrice;
                    }
                    else
                        InitialHitHappenedAtBid = false;
                }
                else
                {
                    recentTradeSizes.Add(trade.Size);
                }
                CheckAndCalculateIfShock();
            }
        }
        public void OnOrderBookUpdate(OrderBookSnapshot orderBook)
        {
            lock (_syncLock)
            {
                var currentSpread = (decimal)orderBook.Spread;
                if (ShockSpread == null
                    && IsLargeWideningSpread(currentSpread))
                {
                    ShockSpread = new TimestampedValue()
                    {
                        Timestamp = DateTime.Now,
                        Value = currentSpread
                    };
                    _bidAtHit = _lastBidPrice;
                    _askAtHit = _lastAskPrice;
                }
                else if (HasSpreadReturnedToMean(currentSpread))
                {
                    //keep track of the last spread
                    ReturnedSpread ??= new TimestampedValue();
                    ReturnedSpread.Value = currentSpread;
                    ReturnedSpread.Timestamp = DateTime.Now;
                }
                recentSpreads.Add(currentSpread);
                _lastMidPrice = (decimal?)orderBook.MidPrice;
                _lastBidPrice = (decimal?)orderBook.Bids[0]?.Price;
                _lastAskPrice = (decimal?)orderBook.Asks[0]?.Price;
                CheckAndCalculateIfShock();
            }
        }
        private void CheckAndCalculateIfShock()
        {
            if (ShockTrade != null
                && ShockSpread != null
                && Math.Abs(ShockSpread.Timestamp.Subtract(ShockTrade.Timestamp).TotalMilliseconds) >
                MIN_SHOCK_TIME_DIFFERENCE) // if the shocks are too far apart (timeout), reset
            {
                Reset();
            }
            else if (ShockTrade != null
                     && ShockSpread != null
                     && ReturnedSpread != null
                     && Math.Abs(ShockSpread.Timestamp.Subtract(ShockTrade.Timestamp).TotalMilliseconds) <
                     MIN_SHOCK_TIME_DIFFERENCE) // if the shocks are close enough, calculate the MR score
            {
                TriggerMRCalculation();
                Reset();
            }
        }


        private bool IsLargeTrade(decimal tradeSize)
        {
            decimal avgSize = recentTradeSizes.Average();
            decimal stdSize = recentTradeSizes.StandardDeviation();
            if (recentTradeSizes.Count < 3) return false; //not enough data
            return tradeSize > avgSize + TRADE_SIZE_SHOCK_THRESHOLD_MULTIPLIER * stdSize;
        }

        private bool IsLargeWideningSpread(decimal spreadValue)
        {
            decimal avgSize = recentSpreads.Average();
            decimal stdSize = recentSpreads.StandardDeviation();
            if (recentSpreads.Count < 3) return false; //not enough data
            return spreadValue > avgSize + SPREAD_SHOCK_THRESHOLD_MULTIPLIER * stdSize;
        }

        private bool HasSpreadReturnedToMean(decimal spreadValue)
        {
            decimal avgSize = recentSpreads.Average();
            return spreadValue < avgSize;
        }

        private void TriggerMRCalculation()
        {
            if (ShockSpread == null || ReturnedSpread == null)
                return;

            // 1. Calculate explicit recovery duration (milliseconds)
            double recoveryDurationMs = Math.Abs((ReturnedSpread.Timestamp - ShockSpread.Timestamp).TotalMilliseconds);

            // 2. Get average historical recovery time (explicitly check historical data)
            double avgHistoricalRecoveryMs = spreadRecoveryTimes.Any() ? spreadRecoveryTimes.Average() : recoveryDurationMs;

            // Explicitly normalize recovery duration:
            // If recovery is faster or equal than avg, MR score is closer to 1 (high resilience)
            // If recovery slower than avg, MR decreases proportionally
            double recoveryScore = avgHistoricalRecoveryMs / (avgHistoricalRecoveryMs + recoveryDurationMs);
            recoveryScore = Math.Max(0, Math.Min(1, recoveryScore)); // clearly keep within [0,1]

            // 3. Calculate explicit magnitude of shock:
            decimal avgHistoricalSpread = recentSpreads.Any() ? recentSpreads.Average() : ShockSpread.Value;
            double magnitudeRatio = (double)(ShockSpread.Value / avgHistoricalSpread);

            // Normalize shock magnitude explicitly:
            // Large shock = lower score; Small shock = higher score
            double magnitudeScore = 1 / magnitudeRatio;
            magnitudeScore = Math.Max(0, Math.Min(1, magnitudeScore)); // clearly keep within [0,1]

            // 4. Explicitly combine both scores clearly (weights can be adjusted):
            CurrentMRScore = (decimal)(0.5 * recoveryScore + 0.5 * magnitudeScore);
            // 5. Determine market bias based on the MR score and where the shock trade happened
            CurrentMarketBias = CalculateMRBias() ?? CurrentMarketBias;


            // Save the recovery duration explicitly for future historical comparison
            spreadRecoveryTimes.Add(recoveryDurationMs);
        }
        protected virtual eMarketBias? CalculateMRBias()
        {
            return null;
        }

        private void Reset()
        {
            lock (_syncLock)
            {
                ShockSpread = null;
                ReturnedSpread = null;
                ShockTrade = null;
                InitialHitHappenedAtBid = null;
                _lastMidPrice = null;
                _lastBidPrice = null;
                _lastAskPrice = null;
                _bidAtHit = null;
                _askAtHit = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {


            }
            disposed = true;
        }

        ~MarketResilienceCalculator()
        {
            Dispose(false);
        }
    }
}
