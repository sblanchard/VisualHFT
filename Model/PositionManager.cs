using System;
using System.Collections.Generic;
using System.Linq;
using Prism.Mvvm;
using VisualHFT.Helpers;

namespace VisualHFT.Model;

public class PositionManager : BindableBase
{
    private List<Order> _buys;
    private double _currentMidPrice;
    private DateTime _lastUpdated;
    private PositionManagerCalculationMethod _method;
    private double _plOpen;
    private double _plRealized;
    private double _plTot;
    private List<Order> _sells;
    private string _symbol;
    private double _totBuy;
    private double _totSell;
    private double _wrkBuy;
    private double _wrkSell;

    public PositionManager(List<Order> orders, PositionManagerCalculationMethod method)
    {
        _method = method;
        //make sure orders are of the same symbol
        if (orders.Select(x => x.Symbol).Distinct().Count() > 1)
            throw new Exception("This class is not able to handle orders with multiple symbols.");

        _buys = orders.Where(x => x.Side == eORDERSIDE.Buy).DefaultIfEmpty(new Order()).ToList();
        _sells = orders.Where(x => x.Side == eORDERSIDE.Sell).DefaultIfEmpty(new Order()).ToList();

        Symbol = orders.First().Symbol;
        TotBuy = _buys.Sum(x => x.FilledQuantity);
        TotSell = _sells.Sum(x => x.FilledQuantity);
        WrkBuy = orders.Where(x => x.Side == eORDERSIDE.Buy).DefaultIfEmpty(new Order()).Sum(x => x.PendingQuantity);
        WrkSell = orders.Where(x => x.Side == eORDERSIDE.Sell).DefaultIfEmpty(new Order()).Sum(x => x.PendingQuantity);

        PLRealized = CalculateRealizedPnL();
        PLTot = PLRealized + PLOpen;
        LastUpdated = DateTime.Now;
    }

    private List<Order> Buys
    {
        get => _buys;
        set => SetProperty(ref _buys, value);
    }

    private List<Order> Sells
    {
        get => _sells;
        set => SetProperty(ref _sells, value);
    }

    private PositionManagerCalculationMethod Method
    {
        get => _method;
        set => SetProperty(ref _method, value);
    }

    public string Symbol
    {
        get => _symbol;
        set => SetProperty(ref _symbol, value);
    }

    public double TotBuy
    {
        get => _totBuy;
        set => SetProperty(ref _totBuy, value);
    }

    public double TotSell
    {
        get => _totSell;
        set => SetProperty(ref _totSell, value);
    }

    public double WrkBuy
    {
        get => _wrkBuy;
        set => SetProperty(ref _wrkBuy, value);
    }

    public double WrkSell
    {
        get => _wrkSell;
        set => SetProperty(ref _wrkSell, value);
    }

    public double PLTot
    {
        get => _plTot;
        set => SetProperty(ref _plTot, value);
    }

    public double PLRealized
    {
        get => _plRealized;
        set => SetProperty(ref _plRealized, value);
    }

    public double PLOpen
    {
        get => _plOpen;
        set => SetProperty(ref _plOpen, value);
    }

    public DateTime LastUpdated
    {
        get => _lastUpdated;
        set => SetProperty(ref _lastUpdated, value);
    }

    public double NetPosition => _totBuy - _totSell;

    public double Exposure => NetPosition * _currentMidPrice;

    public double CurrentMidPrice
    {
        get => _currentMidPrice;
        set => SetProperty(ref _currentMidPrice, value);
    }

    private double CalculateRealizedPnL()
    {
        return HelperPnLCalculator.CalculateRealizedPnL(_buys, _sells, _method);
    }

    private double CalculateOpenPnl()
    {
        return HelperPnLCalculator.CalculateOpenPnL(_buys, _sells, _method, _currentMidPrice);
    }

    private void UpdateUI()
    {
        RaisePropertyChanged("NetPosition");
        RaisePropertyChanged("Exposure");
    }

    public void AddOrder(Order newOrder)
    {
        if (newOrder.Side == eORDERSIDE.Buy)
        {
            Buys.Add(newOrder);
            TotBuy += newOrder.FilledQuantity;
            WrkBuy += newOrder.PendingQuantity;
        }
        else
        {
            Sells.Add(newOrder);
            TotSell += newOrder.FilledQuantity;
            WrkSell += newOrder.PendingQuantity;
        }

        PLRealized = CalculateRealizedPnL();
        PLOpen = CalculateOpenPnl();
        PLTot = _plRealized + _plOpen;
        LastUpdated = DateTime.Now;
        UpdateUI();
    }

    public void UpdateLastMidPrice(double newMidPrice)
    {
        _currentMidPrice = newMidPrice;
        PLOpen = CalculateOpenPnl();
        PLTot = _plRealized + _plOpen;
        UpdateUI();
        LastUpdated = DateTime.Now;
    }
}