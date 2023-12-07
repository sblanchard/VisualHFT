using System;
using System.Collections.Generic;
using VisualHFT.Commons.Model;

namespace VisualHFT.Model;

public class PlotInfoPriceChart : IResettable
{
    public PlotInfoPriceChart()
    {
        AskLevelOrders = new List<OrderBookLevel>();
        BidLevelOrders = new List<OrderBookLevel>();
    }

    public DateTime Date { get; set; }
    public double Volume { get; set; }

    public double MidPrice { get; set; }

    public double BidPrice { get; set; }

    public double AskPrice { get; set; }

    public double? BuyActiveOrder { get; set; }

    public double? SellActiveOrder { get; set; }

    public List<OrderBookLevel> AskLevelOrders { get; set; }
    public List<OrderBookLevel> BidLevelOrders { get; set; }

    public void Reset()
    {
        Date = DateTime.MinValue;
        Volume = 0;
        MidPrice = 0;
        BidPrice = 0;
        AskPrice = 0;
        BuyActiveOrder = 0;
        SellActiveOrder = 0;

        AskLevelOrders?.Clear();
        BidLevelOrders?.Clear();
    }
}