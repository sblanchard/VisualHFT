using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using Prism.Mvvm;
using VisualHFT.Helpers;
using VisualHFT.Model;

namespace VisualHFT.ViewModel;

public class vmOrderBookFlowAnalysis : BindableBase, IDisposable
{
    private Dictionary<string, Func<string, string, bool>> _dialogs;
    private string _layerName;
    private OrderBook _orderBook;


    private List<PlotInfoPriceChart> _realTimeData;
    private Provider _selectedProvider;
    private string _selectedSymbol;
    protected object MTX_ORDERBOOK = new();

    private readonly DispatcherTimer timerUI = new();
    private List<PlotInfoPriceChart> TRACK_RealTimeData = new();

    public vmOrderBookFlowAnalysis(Dictionary<string, Func<string, string, bool>> dialogs)
    {
        _dialogs = dialogs;
        HelperOrderBook.Instance.Subscribe(LIMITORDERBOOK_OnDataReceived);

        timerUI.Interval = TimeSpan.FromMilliseconds(1);
        timerUI.Tick += TimerUI_Tick;
        timerUI.Start();
    }

    public vmOrderBookFlowAnalysis(vmOrderBook vm)
    {
        //this._providers = vm._providers;
        //this._dialogs = vm._dialogs;
        _orderBook = vm.OrderBook;
        _selectedSymbol = vm.SelectedSymbol;
        _selectedProvider = vm.SelectedProvider;
        _layerName = vm.SelectedLayer;
    }


    public List<PlotInfoPriceChart> RealTimeData
    {
        get
        {
            lock (MTX_ORDERBOOK)
            {
                if (_realTimeData == null)
                    return null;
                return _realTimeData.ToList();
            }
        }
    }

    public OrderBook OrderBook
    {
        get => _orderBook;
        set => SetProperty(ref _orderBook, value);
    }

    public string SelectedSymbol
    {
        get => _selectedSymbol;
        set => SetProperty(ref _selectedSymbol, value, () => Clear());
    }

    public Provider SelectedProvider
    {
        get => _selectedProvider;
        set => SetProperty(ref _selectedProvider, value, () => OrderBook = null);
    }

    public string SelectedLayer
    {
        get => _layerName;
        set => SetProperty(ref _layerName, value, () => OrderBook = null);
    }

    public ObservableCollection<Provider> Providers { get; }

    public void Dispose()
    {
        timerUI.Stop(); //stop timer
        HelperOrderBook.Instance.Unsubscribe(LIMITORDERBOOK_OnDataReceived);
    }

    private void TimerUI_Tick(object sender, EventArgs e)
    {
        var localRealTime = RealTimeData?.ToList();

        if (localRealTime != null && !TRACK_RealTimeData.SequenceEqual(localRealTime))
        {
            RaisePropertyChanged(nameof(RealTimeData));
            TRACK_RealTimeData = localRealTime;
        }
    }

    private void LIMITORDERBOOK_OnDataReceived(OrderBook e)
    {
        if (e == null)
            return;
        if (_selectedProvider == null || string.IsNullOrEmpty(_selectedSymbol) ||
            _selectedProvider.ProviderCode != e.ProviderID)
            return;
        if (string.IsNullOrEmpty(_selectedSymbol) || _selectedSymbol == "-- All symbols --" ||
            _selectedSymbol != e.Symbol)
            return;
        lock (MTX_ORDERBOOK)
        {
            if (_orderBook == null || _orderBook.ProviderID != e.ProviderID || _orderBook.Symbol != e.Symbol)
            {
                _orderBook = e;
                _realTimeData = new List<PlotInfoPriceChart>();
            }

            if (!_orderBook.LoadData(e.Asks, e.Bids))
                return; //if nothing to update, then exit

            #region REAL TIME DATA

            if (_realTimeData != null && _orderBook != null)
            {
                //Imbalance
                var objWithHigherDate = _realTimeData.OrderBy(x => x.Date).LastOrDefault();
                var objToAdd = new PlotInfoPriceChart
                    { Date = DateTime.Now, Volume = _orderBook.ImbalanceValue, MidPrice = _orderBook.MidPrice };
                if (objWithHigherDate == null || objToAdd.Date.Subtract(objWithHigherDate.Date).TotalMilliseconds > 10)
                    _realTimeData.Add(objToAdd);
                if (_realTimeData.Count > 300) //max chart points = 300
                    _realTimeData.RemoveAt(0);
            }

            #endregion
        }
    }

    private void Clear()
    {
        _realTimeData = null;
        _orderBook = null;
        RaisePropertyChanged(nameof(RealTimeData));
    }
}