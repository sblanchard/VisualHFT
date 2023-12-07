using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using log4net;
using MarketConnectors.Binance.Model;
using MarketConnectors.Binance.UserControls;
using MarketConnectors.Binance.ViewModel;
using VisualHFT.Commons.PluginManager;
using VisualHFT.Commons.Pools;
using VisualHFT.DataRetriever;
using VisualHFT.Model;
using VisualHFT.PluginManager;
using VisualHFT.UserSettings;

namespace MarketConnectors.Binance;

public class BinancePlugin : BasePluginDataRetriever
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private readonly ObjectPool<Trade> tradePool = new(); //pool of Trade objects

    private readonly Dictionary<string, CancellationTokenSource> _ctDeltas = new();
    private readonly Dictionary<string, CancellationTokenSource> _ctTrades = new();
    private bool _disposed; // to track whether the object has been disposed
    private readonly Dictionary<string, BlockingCollection<IBinanceEventOrderBook>> _eventBuffers = new();
    private readonly Timer _heartbeatTimer;
    private readonly Dictionary<string, OrderBook> _localOrderBooks = new();

    private readonly Dictionary<string, long> _localOrderBooks_LastUpdate = new();
    private readonly BinanceRestClient _restClient;

    private PlugInSettings _settings;
    private readonly BinanceSocketClient _socketClient;
    private readonly Dictionary<string, BlockingCollection<IBinanceTrade>> _tradesBuffers = new();
    private CallResult<UpdateSubscription> deltaSubscription;

    private readonly DataEventArgs
        heartbeatDataEvent = new() { DataType = "HeartBeats" }; //reusable object. So we avoid allocations

    private readonly DataEventArgs
        marketDataEvent = new() { DataType = "Market" }; //reusable object. So we avoid allocations

    private readonly DataEventArgs
        tradeDataEvent = new() { DataType = "Trades" }; //reusable object. So we avoid allocations

    private CallResult<UpdateSubscription> tradesSubscription;

    public BinancePlugin()
    {
        _socketClient = new BinanceSocketClient(options =>
        {
            if (_settings.ApiKey != "" && _settings.ApiSecret != "")
                options.ApiCredentials = new ApiCredentials(_settings.ApiKey, _settings.ApiSecret);
            options.Environment = BinanceEnvironment.Us;
            options.AutoReconnect = true;
        });

        _restClient = new BinanceRestClient(options =>
        {
            if (_settings.ApiKey != "" && _settings.ApiSecret != "")
                options.ApiCredentials = new ApiCredentials(_settings.ApiKey, _settings.ApiSecret);
            options.Environment = BinanceEnvironment.Us;
        });
        // Initialize the timer
        _heartbeatTimer =
            new Timer(CheckConnectionStatus, null, TimeSpan.Zero, TimeSpan.FromSeconds(5)); // Check every 5 seconds
    }

    public override string Name { get; set; } = "Binance.US Plugin";
    public override string Version { get; set; } = "1.0.0";
    public override string Description { get; set; } = "Connects to Binance websockets.";
    public override string Author { get; set; } = "VisualHFT";

    public override ISetting Settings
    {
        get => _settings;
        set => _settings = (PlugInSettings)value;
    }

    public override Action CloseSettingWindow { get; set; }

    ~BinancePlugin()
    {
        Dispose(false);
    }

    public override async Task StartAsync()
    {
        try
        {
            foreach (var sym in GetAllNormalizedSymbols())
            {
                _tradesBuffers.Add(sym, new BlockingCollection<IBinanceTrade>());
                _eventBuffers.Add(sym, new BlockingCollection<IBinanceEventOrderBook>());
            }


            await InitializeTradesAsync();
            await InitializeDeltasAsync();
            await Task.Delay(1000); // allow deltas to come in
            await InitializeSnapshotsAsync();
            await base.StartAsync();
        }
        catch (Exception ex)
        {
            if (failedAttempts == 0)
                RaiseOnError(new ErrorEventArgs { IsCritical = true, PluginName = Name, Exception = ex });
            await HandleConnectionLost();
            throw;
        }
    }

    public override async Task StopAsync()
    {
        foreach (var token in _ctDeltas.Values)
            token.Cancel();
        foreach (var token in _ctTrades.Values)
            token.Cancel();
        _ctDeltas?.Clear();
        _ctTrades?.Clear();


        UnattachEventHandlers(tradesSubscription?.Data);
        UnattachEventHandlers(deltaSubscription?.Data);

        if (deltaSubscription != null && deltaSubscription.Data != null)
            await deltaSubscription.Data.CloseAsync();
        if (tradesSubscription != null && tradesSubscription.Data != null)
            await tradesSubscription.Data.CloseAsync();
        if (_socketClient != null)
            await _socketClient.UnsubscribeAllAsync();

        marketDataEvent.ParsedModel = new List<OrderBook>();
        heartbeatDataEvent.ParsedModel = new List<Provider>();
        //reset models
        RaiseOnDataReceived(marketDataEvent);
        RaiseOnDataReceived(heartbeatDataEvent);

        _eventBuffers.Clear();
        _tradesBuffers.Clear();

        await base.StopAsync();
    }

    private async Task InitializeTradesAsync()
    {
        tradesSubscription = await _socketClient.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(
            GetAllNonNormalizedSymbols(),
            trade =>
            {
                // Buffer the trades
                if (trade.Data != null)
                    try
                    {
                        _tradesBuffers[GetNormalizedSymbol(trade.Data.Symbol)].Add(trade.Data);
                    }
                    catch (Exception ex)
                    {
                        RaiseOnError(new ErrorEventArgs { IsCritical = false, PluginName = Name, Exception = ex });
                        // Start the HandleConnectionLost task without awaiting it
                        Task.Run(HandleConnectionLost);
                    }
            });
        if (tradesSubscription.Success)
        {
            AttachEventHandlers(tradesSubscription.Data);
            InitializeBufferProcessingTasks();
        }
        else
        {
            throw new Exception($"{Name} trades subscription error: {tradesSubscription.Error}");
        }
    }

    private void InitializeBufferProcessingTasks()
    {
        //Initialize processes to consume buffer
        _ctTrades.Clear();
        foreach (var sym in GetAllNonNormalizedSymbols())
        {
            var symbol = GetNormalizedSymbol(sym);

            _ctTrades.Add(symbol, new CancellationTokenSource());
            //launch Task. in a new thread with _ct as cancellation
            Task.Run(async () =>
            {
                foreach (var eventData in _tradesBuffers[symbol].GetConsumingEnumerable(_ctTrades[symbol].Token))
                {
                    // Get a Trade object from the pool.
                    var trade = tradePool.Get();
                    // Populate the Trade object with the necessary data.
                    trade.Price = eventData.Price;
                    trade.Size = eventData.Quantity;
                    trade.Symbol = symbol;
                    trade.Timestamp = eventData.TradeTime.ToLocalTime();
                    trade.ProviderId = _settings.Provider.ProviderID;
                    trade.ProviderName = _settings.Provider.ProviderName;
                    trade.IsBuy = eventData.BuyerIsMaker;

                    // Add the populated Trade object to the _trades list.
                    tradeDataEvent.ParsedModel = new List<Trade> { trade };
                    RaiseOnDataReceived(tradeDataEvent);

                    tradePool.Return(trade);
                }
            });
        }
    }

    private async Task InitializeDeltasAsync()
    {
        // *************** PARTIAL OR NOT???????
        // Subscribe to updates of a symbol
        //_socketClient.SpotApi.ExchangeData.SubscribeToOrderBookUpdatesAsync
        //_socketClient.SpotApi.ExchangeData.SubscribeToPartialOrderBookUpdatesAsync (looks like this is snapshots)
        deltaSubscription = await _socketClient.SpotApi.ExchangeData.SubscribeToOrderBookUpdatesAsync(
            GetAllNonNormalizedSymbols(),
            //DEPTH_LEVELS,
            _settings.UpdateIntervalMs,
            data =>
            {
                // Buffer the events
                if (data.Data != null)
                {
                    data.Data.EventTime = data.Timestamp;
                    if (Math.Abs(DateTime.Now.Subtract(data.Data.EventTime.ToLocalTime()).TotalSeconds) > 1)
                        log.Warn("Rates coming late?");
                    var normalizedSymbol = GetNormalizedSymbol(data.Data.Symbol);
                    if (!_ctDeltas.ContainsKey(normalizedSymbol) || _ctDeltas[normalizedSymbol].IsCancellationRequested)
                        return;
                    _eventBuffers[normalizedSymbol].Add(data.Data);
                }
            });
        if (deltaSubscription.Success)
            AttachEventHandlers(deltaSubscription.Data);
        else
            throw new Exception($"{Name} deltas subscription error: {deltaSubscription.Error}");
    }

    private async Task InitializeSnapshotsAsync()
    {
        foreach (var symbol in GetAllNonNormalizedSymbols())
        {
            var normalizedSymbol = GetNormalizedSymbol(symbol);
            // Fetch initial depth snapshot
            var depthSnapshot = _restClient.SpotApi.ExchangeData.GetOrderBookAsync(symbol, _settings.DepthLevels)
                .Result;
            if (!_localOrderBooks.ContainsKey(normalizedSymbol))
                _localOrderBooks.Add(normalizedSymbol, null);
            if (!_localOrderBooks_LastUpdate.ContainsKey(normalizedSymbol))
                _localOrderBooks_LastUpdate.Add(normalizedSymbol, -1);

            if (!_ctDeltas.ContainsKey(normalizedSymbol))
                _ctDeltas.Add(normalizedSymbol, new CancellationTokenSource());
            else
                _ctDeltas[normalizedSymbol] = new CancellationTokenSource();

            if (depthSnapshot.Success)
            {
                _localOrderBooks[normalizedSymbol] = ToOrderBookModel(depthSnapshot.Data);
                _localOrderBooks_LastUpdate[normalizedSymbol] = depthSnapshot.Data.LastUpdateId;

                //launch Task. in a new thread with _ct as cancellation
                _ = Task.Run(async () =>
                {
                    // Process buffered events
                    ProcessBufferedEvents(normalizedSymbol);
                });
            }
            else
            {
                var erroMsg =
                    $"{Name} getting Snapshot error for {symbol}: {depthSnapshot.ResponseStatusCode} - {depthSnapshot.Error}";
                throw new Exception(erroMsg);
            }
        }
    }

    private void ProcessBufferedEvents(string normalizedSymbol)
    {
        var lastUpdateId = _localOrderBooks_LastUpdate[normalizedSymbol];

        foreach (var eventData in _eventBuffers[normalizedSymbol]
                     .GetConsumingEnumerable(_ctDeltas[normalizedSymbol].Token))
        {
            if (eventData.LastUpdateId <= lastUpdateId) continue;
            UpdateOrderBook(eventData, normalizedSymbol);
            lastUpdateId = eventData.LastUpdateId;
        }

        _localOrderBooks_LastUpdate[normalizedSymbol] = lastUpdateId;
    }

    private void UpdateOrderBook(IBinanceEventOrderBook lob_update, string normalizedSymbol)
    {
        if (!_localOrderBooks.ContainsKey(normalizedSymbol))
            return;

        var local_lob = _localOrderBooks[normalizedSymbol];
        var _bids = local_lob.Bids.ToList();
        var _asks = local_lob.Asks.ToList();
        var ts = lob_update.EventTime.ToLocalTime();

        foreach (var item in lob_update.Bids)
        {
            var toUpdate = _bids.FirstOrDefault(x => x != null && x.Price == (double)item.Price);
            if (toUpdate == null && item.Quantity != 0)
            {
                _bids.Add(new BookItem
                {
                    Price = (double)item.Price,
                    Size = (double)item.Quantity,
                    LocalTimeStamp = DateTime.Now,
                    ServerTimeStamp = ts,
                    DecimalPlaces = local_lob.DecimalPlaces,
                    IsBid = true,
                    ProviderID = _settings.Provider.ProviderID,
                    Symbol = local_lob.Symbol
                });
            }
            else if (toUpdate != null && item.Quantity != 0)
            {
                toUpdate.Size = (double)item.Quantity;
                toUpdate.LocalTimeStamp = DateTime.Now;
                toUpdate.ServerTimeStamp = ts;
            }
            else if (toUpdate != null && item.Quantity == 0)
            {
                _bids.Remove(toUpdate);
            }
            else
            {
            }
        }

        foreach (var item in lob_update.Asks)
        {
            var toUpdate = _asks.FirstOrDefault(x => x != null && x.Price.Value == (double)item.Price);
            if (toUpdate == null && item.Quantity != 0)
            {
                _asks.Add(new BookItem
                {
                    Price = (double)item.Price,
                    Size = (double)item.Quantity,
                    LocalTimeStamp = DateTime.Now,
                    ServerTimeStamp = ts,
                    DecimalPlaces = local_lob.DecimalPlaces,
                    IsBid = false,
                    ProviderID = _settings.Provider.ProviderID,
                    Symbol = local_lob.Symbol
                });
            }
            else if (toUpdate != null && item.Quantity != 0)
            {
                toUpdate.Size = (double)item.Quantity;
                toUpdate.LocalTimeStamp = DateTime.Now;
                toUpdate.ServerTimeStamp = ts;
            }
            else if (toUpdate != null && item.Quantity == 0)
            {
                _asks.Remove(toUpdate);
            }
        }

        local_lob.LoadData(
            _asks.OrderBy(x => x.Price).Take(_settings.DepthLevels),
            _bids.OrderByDescending(x => x.Price).Take(_settings.DepthLevels)
        );
        marketDataEvent.ParsedModel = new List<OrderBook> { local_lob };
        RaiseOnDataReceived(marketDataEvent);
    }

    private void CheckConnectionStatus(object state)
    {
        var isConnected = _socketClient.CurrentConnections > 0;
        heartbeatDataEvent.ParsedModel = new List<Provider> { ToHeartBeatModel(isConnected) };
        RaiseOnDataReceived(heartbeatDataEvent);
    }

    private OrderBook ToOrderBookModel(BinanceOrderBook data)
    {
        var lob = new OrderBook();
        lob.Symbol = GetNormalizedSymbol(data.Symbol);
        lob.SymbolMultiplier = 2; //???????
        lob.DecimalPlaces = 2; //?????????
        lob.ProviderID = _settings.Provider.ProviderID;
        lob.ProviderName = _settings.Provider.ProviderName;

        var _asks = new List<BookItem>();
        var _bids = new List<BookItem>();
        data.Asks.ToList().ForEach(x =>
        {
            _asks.Add(new BookItem
            {
                IsBid = false,
                Price = (double)x.Price,
                Size = (double)x.Quantity,
                LocalTimeStamp = DateTime.Now,
                ServerTimeStamp = DateTime.Now,
                Symbol = lob.Symbol,
                DecimalPlaces = lob.DecimalPlaces,
                ProviderID = lob.ProviderID
            });
        });
        data.Bids.ToList().ForEach(x =>
        {
            _bids.Add(new BookItem
            {
                IsBid = true,
                Price = (double)x.Price,
                Size = (double)x.Quantity,
                LocalTimeStamp = DateTime.Now,
                ServerTimeStamp = DateTime.Now,
                Symbol = lob.Symbol,
                DecimalPlaces = lob.DecimalPlaces,
                ProviderID = lob.ProviderID
            });
        });
        lob.LoadData(_asks, _bids);
        return lob;
    }

    private Provider ToHeartBeatModel(bool isConnected = true)
    {
        return new Provider
        {
            ProviderCode = _settings.Provider.ProviderID,
            ProviderID = _settings.Provider.ProviderID,
            ProviderName = _settings.Provider.ProviderName,
            Status = isConnected ? eSESSIONSTATUS.BOTH_CONNECTED : eSESSIONSTATUS.BOTH_DISCONNECTED,
            Plugin = this
        };
    }


    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var token in _ctDeltas.Values)
                    token.Cancel();
                foreach (var token in _ctTrades.Values)
                    token.Cancel();

                UnattachEventHandlers(tradesSubscription?.Data);
                UnattachEventHandlers(deltaSubscription?.Data);

                _localOrderBooks?.Clear();
                _eventBuffers?.Clear();
                _tradesBuffers?.Clear();
                _ctDeltas?.Clear();
                _ctTrades?.Clear();

                _socketClient?.Dispose();
                _restClient?.Dispose();
                _heartbeatTimer?.Dispose();
            }

            _disposed = true;
        }
    }

    protected override void LoadSettings()
    {
        _settings = LoadFromUserSettings<PlugInSettings>();
        if (_settings == null) InitializeDefaultSettings();
        if (_settings.Provider == null) //To prevent back compability with older setting formats
            _settings.Provider = new Provider { ProviderID = 1, ProviderName = "Binance.US" };
        ParseSymbols(string.Join(',', _settings.Symbols.ToArray())); //Utilize normalization function
    }

    protected override void SaveSettings()
    {
        SaveToUserSettings(_settings);
    }

    protected override void InitializeDefaultSettings()
    {
        _settings = new PlugInSettings
        {
            ApiKey = "",
            ApiSecret = "",
            DepthLevels = 10,
            UpdateIntervalMs = 100,
            Provider = new Provider { ProviderID = 1, ProviderName = "Binance.US" },
            Symbols = new List<string> { "BTCUSDT(BTC/USD)", "ETHUSDT(ETH/USD)" } // Add more symbols as needed
        };
        SaveToUserSettings(_settings);
    }

    public override object GetUISettings()
    {
        var view = new PluginSettingsView();
        var viewModel = new PluginSettingsViewModel(CloseSettingWindow);
        viewModel.ApiSecret = _settings.ApiSecret;
        viewModel.ApiKey = _settings.ApiKey;
        viewModel.UpdateIntervalMs = _settings.UpdateIntervalMs;
        viewModel.DepthLevels = _settings.DepthLevels;
        viewModel.ProviderId = _settings.Provider.ProviderID;
        viewModel.ProviderName = _settings.Provider.ProviderName;
        viewModel.Symbols = _settings.Symbols;
        viewModel.UpdateSettingsFromUI = () =>
        {
            _settings.ApiSecret = viewModel.ApiSecret;
            _settings.ApiKey = viewModel.ApiKey;
            _settings.UpdateIntervalMs = viewModel.UpdateIntervalMs;
            _settings.DepthLevels = viewModel.DepthLevels;
            _settings.Provider = new Provider
                { ProviderID = viewModel.ProviderId, ProviderName = viewModel.ProviderName };
            _settings.Symbols = viewModel.Symbols;
            SaveSettings();
            ParseSymbols(string.Join(',', _settings.Symbols.ToArray()));

            // Start the HandleConnectionLost task without awaiting it
            //run this because it will allow to reconnect with the new values
            Task.Run(HandleConnectionLost);
        };
        // Display the view, perhaps in a dialog or a new window.
        view.DataContext = viewModel;
        return view;
    }


    #region Websocket Deltas Callbacks

    private void AttachEventHandlers(UpdateSubscription data)
    {
        if (data == null)
            return;
        data.Exception += deltaSubscription_Exception;
        data.ConnectionLost += deltaSubscription_ConnectionLost;
        data.ConnectionClosed += deltaSubscription_ConnectionClosed;
        data.ConnectionRestored += deltaSubscription_ConnectionRestored;
        data.ActivityPaused += deltaSubscription_ActivityPaused;
        data.ActivityUnpaused += deltaSubscription_ActivityUnpaused;
    }

    private void UnattachEventHandlers(UpdateSubscription data)
    {
        if (data == null)
            return;
        data.Exception -= deltaSubscription_Exception;
        data.ConnectionLost -= deltaSubscription_ConnectionLost;
        data.ConnectionClosed -= deltaSubscription_ConnectionClosed;
        data.ConnectionRestored -= deltaSubscription_ConnectionRestored;
        data.ActivityPaused -= deltaSubscription_ActivityPaused;
        data.ActivityUnpaused -= deltaSubscription_ActivityUnpaused;
    }

    private void deltaSubscription_ActivityUnpaused()
    {
        throw new NotImplementedException();
    }

    private void deltaSubscription_ActivityPaused()
    {
        throw new NotImplementedException();
    }

    private void deltaSubscription_ConnectionRestored(TimeSpan obj)
    {
    }

    private void deltaSubscription_ConnectionClosed()
    {
        if (log.IsWarnEnabled)
            log.Warn($"{Name} Reconnecting because Subscription channel has been closed from the server");

        // Start the HandleConnectionLost task without awaiting it
        Task.Run(HandleConnectionLost);
    }

    private void deltaSubscription_ConnectionLost()
    {
        RaiseOnError(new ErrorEventArgs
            { IsCritical = false, PluginName = Name, Exception = new Exception("Connection lost.") });
        // Start the HandleConnectionLost task without awaiting it
        Task.Run(HandleConnectionLost);
    }

    private void deltaSubscription_Exception(Exception obj)
    {
        RaiseOnError(new ErrorEventArgs { IsCritical = false, PluginName = Name, Exception = obj });
        // Start the HandleConnectionLost task without awaiting it
        Task.Run(HandleConnectionLost);
    }

    #endregion
}