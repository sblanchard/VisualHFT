﻿using VisualHFT.Model;

namespace VisualHFT.Studies;

public class OrderFlowAnalysis
{
    private IEnumerable<BookItem> asks;
    private IEnumerable<BookItem> bids;

    public void LoadData(IEnumerable<BookItem> pAsks, IEnumerable<BookItem> pBids)
    {
        asks = pAsks;
        bids = pBids;
    }

    private void Calculate_TradeImbalance()
    {
        /*
         Similar to order imbalance, trade imbalance measures the difference between the number of executed buy and sell trades. It can provide insights into the actual trading activity in the market.
         */
    }

    private void Calculate_OrderFlowToxicity()
    {
        /*
         This metric measures the likelihood that incoming orders are informed trades (i.e., trades based on private information). High order flow toxicity can indicate a higher likelihood of price movements, as informed traders are likely to trade in the direction of future price changes.
         */
    }

    private double Calculate_VWAP()
    {
        /*
         VWAP is the average price a security has traded at throughout the day, based on both volume and price. It is important because it provides traders with insight into both the trend and value of a security.
         */
        var totalAskValue = asks.Sum(a => a.Price.Value * a.Size.Value);
        var totalBidValue = bids.Sum(b => b.Price.Value * b.Size.Value);
        var totalValue = totalAskValue + totalBidValue;
        var totalSize = asks.Sum(a => a.Size.Value) + bids.Sum(b => b.Size.Value);
        return totalValue / totalSize;
    }

    private double Calculate_OrderBookDepth()
    {
        /*
         This metric measures the number of open buy and sell orders at different price levels. It can provide insights into the liquidity and depth of the market.
         */
        return asks.Count() + bids.Count();
    }

    private void Calculate_OrderSizeAndFrequency()
    {
        /*
         These metrics measure the average size and frequency of incoming orders. They can provide insights into the trading activity and liquidity in the market.
         */
    }

    private void Calculate_PriceImpactOfTrades()
    {
        /*
         This metric measures the impact of trades on the price of a security. It can provide insights into the liquidity and resilience of the market.
         */
    }

    private void Calculate_CancelationRate()
    {
        /*             
         This metric measures the rate at which orders are canceled relative to the rate at which they are placed. A high cancellation rate can indicate a more volatile and less predictable market.
         */
    }

    private double Calculate_OrderBookSlope()
    {
        /*
         The slope of the order book (i.e., the relationship between price and cumulative order size) can provide insights into market participants' expectations about future price movements. A steeper slope on the bid side might indicate bullish sentiment, while a steeper slope on the ask side might indicate bearish sentiment.
         */

        // Calculate the cumulative size for bids and asks
        var cumulativeBidSize = bids.Sum(b => b.Size.Value);
        var cumulativeAskSize = asks.Sum(a => a.Size.Value);

        // Calculate the price range for bids and asks
        var bidPriceRange = bids.Max(b => b.Size.Value) - bids.Min(b => b.Size.Value);
        var askPriceRange = asks.Max(a => a.Size.Value) - asks.Min(a => a.Size.Value);

        // Calculate the slope for bids and asks
        var bidSlope = bidPriceRange == 0 ? 0 : cumulativeBidSize / bidPriceRange;
        var askSlope = askPriceRange == 0 ? 0 : cumulativeAskSize / askPriceRange;

        // Return the average slope
        return (bidSlope + askSlope) / 2;
    }

    public double Calculate_OrderImbalance()
    {
        /*
        This metric measures the difference between the number of buy and sell orders in the market.
        It can provide insights into the supply and demand dynamics in the market. 
        A positive order imbalance (more buy orders than sell orders) can indicate upward pressure on prices, while a negative order imbalance (more sell orders than buy orders) can indicate downward pressure on prices.
        */
        if (asks != null && bids != null && asks.Any() && bids.Any())
        {
            var totalAskSize = asks.Sum(a => a.Size.Value);
            var totalBidSize = bids.Sum(b => b.Size.Value);
            return (totalBidSize - totalAskSize) / (totalBidSize + totalAskSize);
        }

        return 0;
    }

    public double CalculateOrderBookKurtosis()
    {
        // Calculate Order Book Kurtosis
        // This metric measures the "tailedness" of the order book. High kurtosis (a lot of volume far from the mid-price) might indicate the presence of large limit orders or stop orders, which could impact price movements.

        var midPrice = (asks.Min(a => a.Price.Value) + bids.Max(b => b.Price.Value)) / 2;
        var fourthMoment = asks.Average(a => Math.Pow(a.Price.Value - midPrice, 4)) +
                           bids.Average(b => Math.Pow(b.Price.Value - midPrice, 4));
        var variance = asks.Average(a => Math.Pow(a.Price.Value - midPrice, 2)) +
                       bids.Average(b => Math.Pow(b.Price.Value - midPrice, 2));
        return fourthMoment / Math.Pow(variance, 2);
    }

    public double CalculatePriceLevelClustering()
    {
        // Calculate Price Level Clustering
        // This metric measures the extent to which orders are clustered at certain price levels. Clustering can indicate the presence of significant support or resistance levels.
        var priceLevels = asks.Select(a => a.Price.Value).Concat(bids.Select(b => b.Price.Value)).Distinct().Count();
        return (double)priceLevels / (asks.Count() + bids.Count());
    }

    public double CalculateLiquidityConsumption()
    {
        // Calculate Liquidity Consumption
        // This is a measure of how much the order book "moves" for each unit of volume traded. It can provide insights into market liquidity and efficiency.
        var volumeWeightedPrice =
            asks.Sum(a => a.Price.Value * a.Size.Value) + bids.Sum(b => b.Price.Value * b.Size.Value);
        var totalVolume = asks.Sum(a => a.Size.Value) + bids.Sum(b => b.Size.Value);
        var averagePrice = volumeWeightedPrice / totalVolume;
        var midPrice = (asks.Min(a => a.Price.Value) + bids.Max(b => b.Price.Value)) / 2;
        return Math.Abs(averagePrice - midPrice) / totalVolume;
    }
}