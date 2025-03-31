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
        protected override eMarketBias? CalculateMRBias()
        {
            if (!InitialHitHappenedAtBid.HasValue || !_bidAtHit.HasValue || !_askAtHit.HasValue)
                return null;

            if (!_lastBidPrice.HasValue || !_lastAskPrice.HasValue)
                return null;

            decimal lastBidVsHitDiff = Math.Abs(_lastBidPrice.Value - _bidAtHit.Value);
            decimal lastAskVsHitDiff = Math.Abs(_lastAskPrice.Value - _askAtHit.Value);

            bool hasBidRecovered = lastBidVsHitDiff < lastAskVsHitDiff; //bid has partially or fully recovered
            bool hasAskRecovered = lastAskVsHitDiff < lastBidVsHitDiff; //ask has partially or fully recovered


            if (InitialHitHappenedAtBid.Value == false
                && !hasAskRecovered
                && _lastBidPrice > _bidAtHit.Value)
                return eMarketBias.Bullish; // Ask hit, didn't recover, bid is higher than hit
            else if (InitialHitHappenedAtBid.Value == true
                     && hasBidRecovered
                     && _lastBidPrice > _bidAtHit)
                return eMarketBias.Bullish; // Bid hit, recovered, bid is higher than hit
            else if (InitialHitHappenedAtBid.Value == true
                && !hasBidRecovered
                && _lastAskPrice < _askAtHit.Value)
                return eMarketBias.Bearish; // Bid hit, didn't recover, ask is lower than hit
            else if (InitialHitHappenedAtBid.Value == false
                     && hasAskRecovered
                     && _lastAskPrice < _askAtHit)
                return eMarketBias.Bearish; // Ask hit, recovered, ask is lower than hit
            else if (InitialHitHappenedAtBid.Value == false && hasAskRecovered)
                return eMarketBias.Neutral;
            else if (InitialHitHappenedAtBid.Value == true && hasBidRecovered)
                return eMarketBias.Neutral;
            else
                return null;
        }
    }
}
