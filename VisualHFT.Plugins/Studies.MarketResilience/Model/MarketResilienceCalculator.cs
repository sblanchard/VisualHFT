using System;
using System.Linq;
using VisualHFT.Commons.Pools;
using VisualHFT.Model;

namespace Studies.MarketResilience.Model
{
    public class MarketResilienceCalculator : IDisposable
    {
        protected readonly object _syncLock = new object();

        private DateTime? spreadShockTime = null;
        private DateTime? depthShockTime = null;
        private DateTime? tradeShockTime = null;
        protected RollingWindow<decimal> recentSpreads = new RollingWindow<decimal>(500);
        private RollingWindow<decimal> recentDepths = new RollingWindow<decimal>(500);
        private RollingWindow<decimal> recentTradeSizes = new RollingWindow<decimal>(500);
        private RollingWindow<double> recentPriceImpacts = new RollingWindow<double>(500);
        private RollingWindow<double> spreadRecoveryTimes = new RollingWindow<double>(100);
        private RollingWindow<double> depthRecoveryTimes = new RollingWindow<double>(100);
        private RollingWindow<double> tradeRecoveryTimes = new RollingWindow<double>(100);
        protected decimal spreadShockThresholdMultiplier = 3m;
        private decimal depthShockThresholdMultiplier = 3m;
        private decimal tradeSizeShockThresholdMultiplier = 3m;

        private PendingTradeImpactCheck pendingTradeCheck = null;
        private OrderBook lastOrderBookSnapshot = null;
        private bool pendingMRCalculation = false;


        private bool disposed = false;

        private class PendingTradeImpactCheck
        {
            public bool IsLargeTrade;
            public double MidPriceBeforeTrade;
            public DateTime Timestamp;
        }

        public decimal CurrentMRScore { get; private set; } = 1m; // stable MR value by default

        public double  MidMarketPrice => lastOrderBookSnapshot?.MidPrice ?? 0;
        public void OnTrade(Trade trade)
        {
            lock (_syncLock)
            {
                recentTradeSizes.Add(trade.Size);

                if (lastOrderBookSnapshot == null) return;
                bool isLargeTrade = IsLargeTrade(trade.Size, lastOrderBookSnapshot);
                pendingTradeCheck = new PendingTradeImpactCheck
                {
                    IsLargeTrade = isLargeTrade,
                    MidPriceBeforeTrade = (double)lastOrderBookSnapshot.MidPrice,
                    Timestamp = trade.Timestamp
                };

                if (isLargeTrade)
                {
                    tradeShockTime = DateTime.UtcNow;
                    pendingMRCalculation = true;
                }
            }
        }

        public virtual void OnOrderBookUpdate(OrderBook orderBook)
        {
            lock (_syncLock)
            {
                lastOrderBookSnapshot = orderBook;

                decimal currentSpread = (decimal)orderBook.Spread;
                decimal currentDepth = orderBook.Bids.Take(5).Sum(b => (decimal)b.Size) +
                                       orderBook.Asks.Take(5).Sum(a => (decimal)a.Size);

                recentSpreads.Add(currentSpread);
                recentDepths.Add(currentDepth);

                bool spreadShock = IsShock(currentSpread, recentSpreads, spreadShockThresholdMultiplier, true);
                bool depthShock = IsShock(currentDepth, recentDepths, depthShockThresholdMultiplier, false);

                // Handle spread shock
                if (spreadShock && spreadShockTime == null)
                {
                    spreadShockTime = DateTime.UtcNow;
                    pendingMRCalculation = true;
                }
                else if (!spreadShock && spreadShockTime != null)
                {
                    var recoveryDuration = DateTime.UtcNow - spreadShockTime.Value;
                    spreadRecoveryTimes.Add(recoveryDuration.TotalMilliseconds);
                    spreadShockTime = null;

                    // Trigger MR after recovery
                    if (pendingMRCalculation && depthShockTime == null && tradeShockTime == null)
                    {
                        TriggerMRCalculation();
                        pendingMRCalculation = false;
                    }
                }

                // Handle depth shock
                if (depthShock && depthShockTime == null)
                {
                    depthShockTime = DateTime.UtcNow;
                    pendingMRCalculation = true;
                }
                else if (!depthShock && depthShockTime != null)
                {
                    var recoveryDuration = DateTime.UtcNow - depthShockTime.Value;
                    depthRecoveryTimes.Add(recoveryDuration.TotalMilliseconds);
                    depthShockTime = null;

                    // Trigger MR after recovery
                    if (pendingMRCalculation && spreadShockTime == null && tradeShockTime == null)
                    {
                        TriggerMRCalculation();
                        pendingMRCalculation = false;
                    }
                }

                if (pendingTradeCheck != null)
                {
                    double midAfterTrade = orderBook.MidPrice;
                    double impact = Math.Abs(midAfterTrade - pendingTradeCheck.MidPriceBeforeTrade);
                    recentPriceImpacts.Add(impact);

                    bool tradeImpactMinimal = IsMinimalImpact(impact);

                    if (tradeImpactMinimal && tradeShockTime != null)
                    {
                        var recoveryDuration = DateTime.UtcNow - tradeShockTime.Value;
                        tradeRecoveryTimes.Add(recoveryDuration.TotalMilliseconds);
                        tradeShockTime = null;

                        // Trigger MR after recovery
                        if (pendingMRCalculation && spreadShockTime == null && depthShockTime == null)
                        {
                            TriggerMRCalculation();
                            pendingMRCalculation = false;
                        }
                    }

                    pendingTradeCheck = null;
                }

            }
        }

        protected bool IsShock(decimal currentValue, RollingWindow<decimal> window, decimal thresholdMultiplier, bool higherIsShock)
        {
            decimal avg = window.Average();
            decimal std = window.StandardDeviation();

            if (std == 0) return false;

            decimal threshold = higherIsShock ? avg + thresholdMultiplier * std : avg - thresholdMultiplier * std;
            return higherIsShock ? currentValue > threshold : currentValue < threshold;
        }

        private bool IsLargeTrade(decimal tradeSize, OrderBook book)
        {
            decimal avgSize = recentTradeSizes.Average();
            decimal stdSize = recentTradeSizes.StandardDeviation();

            if (stdSize == 0) return false;

            return tradeSize > avgSize + tradeSizeShockThresholdMultiplier * stdSize;
        }

        private bool IsMinimalImpact(double impact)
        {
            double avgImpact = recentPriceImpacts.Average();
            double stdImpact = recentPriceImpacts.StandardDeviation();

            if (stdImpact == 0) return impact == 0;

            return impact < avgImpact + stdImpact;
        }


        private void TriggerMRCalculation()
        {
            double spreadScore = spreadRecoveryTimes.Count > 0
                ? NormalizeRecoveryTime(spreadRecoveryTimes.Last(), spreadRecoveryTimes) : 1.0;

            double depthScore = depthRecoveryTimes.Count > 0
                ? NormalizeRecoveryTime(depthRecoveryTimes.Last(), depthRecoveryTimes) : 1.0;

            double tradeImpactScore = tradeRecoveryTimes.Count > 0
                ? NormalizeRecoveryTime(tradeRecoveryTimes.Last(), tradeRecoveryTimes) : 1.0;

            CurrentMRScore = (decimal)(
                0.3 * spreadScore +
                0.3 * depthScore +
                0.2 * tradeImpactScore +
                0.2 * ((spreadScore + depthScore + tradeImpactScore) / 3.0));
        }

        private double NormalizeRecoveryTime(double recoveryMs, RollingWindow<double> historicalRecoveryTimes)
        {
            if (historicalRecoveryTimes.Count < 10)
                return 1.0;

            double avgRecovery = historicalRecoveryTimes.Average();
            double stdRecovery = historicalRecoveryTimes.StandardDeviation();

            if (stdRecovery == 0)
                return recoveryMs <= avgRecovery ? 1.0 : 0.0;

            double zScore = (recoveryMs - avgRecovery) / stdRecovery;
            double resilienceScore = 1 - Math.Min(1, Math.Max(0, (zScore / 3.0)));

            return resilienceScore;
        }
        public virtual void Reset()
        {
            lock (_syncLock)
            {
                recentSpreads.Clear();
                recentDepths.Clear();
                recentTradeSizes.Clear();
                recentPriceImpacts.Clear();
                spreadRecoveryTimes.Clear();
                depthRecoveryTimes.Clear();
                tradeRecoveryTimes.Clear();

                spreadShockTime = null;
                depthShockTime = null;
                tradeShockTime = null;
                pendingMRCalculation = false;

                CurrentMRScore = 1m;
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
                // Dispose managed resources.
                if (lastOrderBookSnapshot != null)
                {
                    lastOrderBookSnapshot.Dispose();
                    lastOrderBookSnapshot = null;
                }

            }
            disposed = true;
        }

        ~MarketResilienceCalculator()
        {
            Dispose(false);
        }
    }
}
