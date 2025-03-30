using System;
using System.Linq;
using VisualHFT.Model;
using VisualHFT.Studies.MarketResilience.Model;

namespace Studies.MarketResilience.Model
{

    public class MarketResilienceWithBias : MarketResilienceCalculator
    {
        public MarketResilienceWithBias(PlugInSettings settings): base(settings)
        {

        }
        protected override eMarketBias CalculateMRBias()
        {
            if (!InitialHitHappenedAtBid.HasValue || !_bidAtHit.HasValue || !_askAtHit.HasValue)
                return eMarketBias.Neutral;

            if (!_lastBidPrice.HasValue || !_lastAskPrice.HasValue)
                return eMarketBias.Neutral;

            bool hasBidFullyRecovered = _lastBidPrice.Value >= _bidAtHit.Value;
            bool hasAskFullyRecovered = _lastAskPrice.Value <= _askAtHit.Value;

            if (InitialHitHappenedAtBid.Value)
            {
                if (!hasBidFullyRecovered)
                    return eMarketBias.Bearish; // Bid hit, didn't recover
                else
                    return eMarketBias.Neutral; // Bid hit, fully recovered
            }
            else // Initial hit was at ask
            {
                if (!hasAskFullyRecovered)
                    return eMarketBias.Bullish; // Ask hit, didn't recover
                else
                    return eMarketBias.Neutral; // Ask hit, fully recovered
            }
        }
    }
}
