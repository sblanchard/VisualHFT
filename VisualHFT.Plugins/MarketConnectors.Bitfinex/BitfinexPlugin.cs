using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bitfinex.Net;
using Bitfinex.Net.Clients;
using Bitfinex.Net.Enums;
using Bitfinex.Net.Objects.Models;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using log4net;
using MarketConnectors.Bitfinex.Model;
using MarketConnectors.Bitfinex.UserControls;
using MarketConnectors.Bitfinex.ViewModel;
using VisualHFT.Commons.PluginManager;
using VisualHFT.Commons.Pools;
using VisualHFT.DataRetriever;
using VisualHFT.Model;
using VisualHFT.PluginManager;
using VisualHFT.UserSettings;

namespace MarketConnectors.Bitfinex;

public class BitfinexPlugin : BasePluginDataRetriever
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private readonly ObjectPool<Trade> tradePool = new(); //pool of Trade objects


    private readonly Dictionary<string, CancellationTokenSource> _ctDeltas = new();
    private readonly Dictionary<string, CancellationTokenSource> _ctTrades = new();
    private bool _disposed; // to track whether the object has been disposed

    private readonly Dictionary<string, BlockingCollection<Tuple<DateTime, BitfinexOrderBookEntry>>> _eventBuffers =
        new();


    private readonly Timer _heartbeatTimer;
    private readonly Dictionary<string, OrderBook> _localOrderBooks = new();
    private readonly BitfinexRestClient _restClient;

    private PlugInSettings _settings;
    private readonly BitfinexSocketClient _socketClient;
    private readonly Dictionary<string, BlockingCollection<BitfinexTradeSimple>> _tradesBuffers = new();
    private CallResult<UpdateSubscription> deltaSubscription;

    private readonly DataEventArgs
        heartbeatDataEvent = new() { DataType = "HeartBeats" }; //reusable object. So we avoid allocations

    private readonly DataEventArgs
        marketDataEvent = new() { DataType = "Market" }; //reusable object. So we avoid allocations

    private readonly DataEventArgs
        tradeDataEvent = new() { DataType = "Trades" }; //reusable object. So we avoid allocations

    private CallResult<UpdateSubscription> tradesSubscription;

    public BitfinexPlugin()
    {
        _socketClient = new BitfinexSocketClient(options =>
        {
            if (_settings.ApiKey != "" && _settings.ApiSecret != "")
                options.ApiCredentials = new ApiCredentials(_settings.ApiKey, _settings.ApiSecret);
            options.Environment = BitfinexEnvironment.Live;
            options.AutoReconnect = true;
        });

        _restClient = new BitfinexRestClient(options =>
        {
            if (_settings.ApiKey != "" && _settings.ApiSecret != "")
                options.ApiCredentials = new ApiCredentials(_settings.ApiKey, _settings.ApiSecret);
            options.Environment = BitfinexEnvironment.Live;
        });
        // Initialize the timer
        _heartbeatTimer =
            new Timer(CheckConnectionStatus, null, TimeSpan.Zero, TimeSpan.FromSeconds(5)); // Check every 5 seconds
    }


    public override string Name { get; set; } = "Bitfinex Plugin";
    public override string Version { get; set; } = "1.0.0";
    public override string Description { get; set; } = "Connects to Bitfinex websockets.";
    public override string Author { get; set; } = "VisualHFT";

    public override ISetting Settings
    {
        get => _settings;
        set => _settings = (PlugInSettings)value;
    }

    public override Action CloseSettingWindow { get; set; }

    ~BitfinexPlugin()
    {
        Dispose(false);
    }

    public override async Task StartAsync()
    {
        try
        {
            foreach (var sym in GetAllNonNormalizedSymbols())
            {
                var symbol = GetNormalizedSymbol(sym);
                // Initialize event buffer for each symbol
                _eventBuffers.Add(symbol, new BlockingCollection<Tuple<DateTime, BitfinexOrderBookEntry>>());
                _tradesBuffers.Add(symbol, new BlockingCollection<BitfinexTradeSimple>());
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

        //reset models
        RaiseOnDataReceived(
            new DataEventArgs { DataType = "Market", ParsedModel = new List<OrderBook>(), RawData = "" });
        RaiseOnDataReceived(new DataEventArgs
            { DataType = "HeartBeats", ParsedModel = new List<Provider> { ToHeartBeatModel(false) }, RawData = "" });

        _eventBuffers.Clear();
        _tradesBuffers.Clear();

        await base.StopAsync();
    }

    private async Task InitializeTradesAsync()
    {
        foreach (var symbol in GetAllNonNormalizedSymbols())
        {
            tradesSubscription = await _socketClient.SpotApi.SubscribeToTradeUpdatesAsync(
                symbol,
                trade =>
                {
                    // Buffer the trades
                    if (trade.Data != null)
                        try
                        {
                            foreach (var item in trade.Data)
                            {
                                var _symbol = GetNormalizedSymbol(symbol);
                                if (!_ctTrades.ContainsKey(_symbol) || _ctTrades[_symbol].IsCancellationRequested)
                                    return;

                                _tradesBuffers[_symbol].Add(item);
                            }
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
                throw new Exception($"{Name} trades subscription for {symbol} error: {tradesSubscription.Error}");
            }
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
            Task.Run(async () => { ProcessBufferedTrades(symbol); });
        }
    }

    private async Task InitializeDeltasAsync()
    {
        foreach (var symbol in GetAllNonNormalizedSymbols())
        {
            deltaSubscription = await _socketClient.SpotApi.SubscribeToOrderBookUpdatesAsync(
                symbol,
                Precision.PrecisionLevel0,
                Frequency.Realtime,
                _settings.DepthLevels,
                data =>
                {
                    // Buffer the events
                    if (data.Data != null)
                    {
                        var normalizedSymbol = GetNormalizedSymbol(symbol);
                        if (!_ctDeltas.ContainsKey(normalizedSymbol) ||
                            _ctDeltas[normalizedSymbol].IsCancellationRequested)
                            return;
                        if (_eventBuffers.ContainsKey(normalizedSymbol))
                            foreach (var item in data.Data)
                                _eventBuffers[normalizedSymbol]
                                    .Add(new Tuple<DateTime, BitfinexOrderBookEntry>(data.Timestamp.ToLocalTime(),
                                        item));
                    }
                });
            if (deltaSubscription.Success)
                AttachEventHandlers(deltaSubscription.Data);
            else
                throw new Exception($"{Name} deltas subscription for {symbol} error: {deltaSubscription.Error}");
        }
    }

    private async Task InitializeSnapshotsAsync()
    {
        foreach (var symbol in GetAllNonNormalizedSymbols())
        {
            var normalizedSymbol = GetNormalizedSymbol(symbol);

            // Fetch initial depth snapshot
            var depthSnapshot = _restClient.SpotApi.ExchangeData
                .GetOrderBookAsync(symbol, Precision.PrecisionLevel0, _settings.DepthLevels).Result;
            if (!_localOrderBooks.ContainsKey(normalizedSymbol))
                _localOrderBooks.Add(normalizedSymbol, null);

            if (!_ctDeltas.ContainsKey(normalizedSymbol))
                _ctDeltas.Add(normalizedSymbol, new CancellationTokenSource());
            else
                _ctDeltas[normalizedSymbol] = new CancellationTokenSource();

            if (depthSnapshot.Success)
            {
                _localOrderBooks[normalizedSymbol] = ToOrderBookModel(depthSnapshot.Data, normalizedSymbol);

                //launch Task. in a new thread with _ct as cancellation
                _ = Task.Run(async () =>
                {
                    foreach (var eventData in _eventBuffers[normalizedSymbol]
                                 .GetConsumingEnumerable(_ctDeltas[normalizedSymbol].Token))
                        UpdateOrderBook(eventData.Item2, normalizedSymbol, eventData.Item1);
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

    private void ProcessBufferedTrades(string symbol)
    {
        foreach (var eventData in _tradesBuffers[symbol].GetConsumingEnumerable(_ctTrades[symbol].Token))
        {
            var typeEvent = eventData.UpdateType;
            var trade = tradePool.Get();
            trade.Price = eventData.Price;
            trade.Size = Math.Abs(eventData.Quantity);
            trade.Symbol = symbol;
            trade.Timestamp = eventData.Timestamp.ToLocalTime();
            trade.ProviderId = _settings.Provider.ProviderID;
            trade.ProviderName = _settings.Provider.ProviderName;
            trade.IsBuy = eventData.Quantity > 0;

            tradeDataEvent.ParsedModel = new List<Trade> { trade };
            RaiseOnDataReceived(tradeDataEvent);

            tradePool.Return(trade);
        }
    }

    private void UpdateOrderBook(BitfinexOrderBookEntry lob_update, string symbol, DateTime ts)
    {
        if (!_localOrderBooks.ContainsKey(symbol))
            return;
        if (lob_update == null)
            return;

        var local_lob = _localOrderBooks[symbol];
        var _bids = local_lob.Bids.ToList();
        var _asks = local_lob.Asks.ToList();


        if (lob_update.Count > 0) //add or update level
        {
            var isBid = lob_update.Quantity > 0;
            if (isBid)
            {
                var itemToUpdate = _bids.FirstOrDefault(x => x.Price == (double)lob_update.Price);
                if (itemToUpdate != null)
                {
                    itemToUpdate.Size = (double)Math.Abs(lob_update.Quantity);
                    itemToUpdate.LocalTimeStamp = DateTime.Now;
                    itemToUpdate.ServerTimeStamp = ts;
                }
                else
                {
                    _bids.Add(new BookItem
                    {
                        Price = (double)lob_update.Price,
                        Size = (double)Math.Abs(lob_update.Quantity),
                        LocalTimeStamp = DateTime.Now,
                        ServerTimeStamp = ts,
                        DecimalPlaces = local_lob.DecimalPlaces,
                        IsBid = isBid,
                        ProviderID = _settings.Provider.ProviderID,
                        Symbol = local_lob.Symbol
                    });
                }
            }
            else
            {
                var itemToUpdate = _asks.FirstOrDefault(x => x.Price == (double)lob_update.Price);
                if (itemToUpdate != null)
                {
                    itemToUpdate.Size = (double)Math.Abs(lob_update.Quantity);
                    itemToUpdate.LocalTimeStamp = DateTime.Now;
                    itemToUpdate.ServerTimeStamp = ts;
                }
                else
                {
                    _asks.Add(new BookItem
                    {
                        Price = (double)lob_update.Price,
                        Size = (double)Math.Abs(lob_update.Quantity),
                        LocalTimeStamp = DateTime.Now,
                        ServerTimeStamp = ts,
                        DecimalPlaces = local_lob.DecimalPlaces,
                        IsBid = isBid,
                        ProviderID = _settings.Provider.ProviderID,
                        Symbol = local_lob.Symbol
                    });
                }
            }
        }
        else
        {
            if (lob_update.Quantity == 1) //remove from bids
                _bids.RemoveAll(x => x.Price == (double)lob_update.Price);
            else if (lob_update.Quantity == -1) //remove from asks
                _asks.RemoveAll(x => x.Price == (double)lob_update.Price);
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

    private OrderBook ToOrderBookModel(BitfinexOrderBook data, string symbol)
    {
        var lob = new OrderBook();
        lob.Symbol = symbol;
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

                UnattachEventHandlers(tradesSubscription.Data);
                UnattachEventHandlers(deltaSubscription.Data);

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
            _settings.Provider = new Provider { ProviderID = 2, ProviderName = "Bitfinex" };
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
            DepthLevels = 25,
            Provider = new Provider { ProviderID = 2, ProviderName = "Bitfinex" },
            Symbols = new List<string> { "tBTCUSD(BTC/USD)", "tETHUSD(ETH/USD)" } // Add more symbols as needed
        };
        SaveToUserSettings(_settings);
    }

    public override object GetUISettings()
    {
        var view = new PluginSettingsView();
        var viewModel = new PluginSettingsViewModel(CloseSettingWindow);
        viewModel.ApiSecret = _settings.ApiSecret;
        viewModel.ApiKey = _settings.ApiKey;
        viewModel.DepthLevels = _settings.DepthLevels;
        viewModel.ProviderId = _settings.Provider.ProviderID;
        viewModel.ProviderName = _settings.Provider.ProviderName;
        viewModel.Symbols = _settings.Symbols;
        viewModel.UpdateSettingsFromUI = () =>
        {
            _settings.ApiSecret = viewModel.ApiSecret;
            _settings.ApiKey = viewModel.ApiKey;
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
        //throw new NotImplementedException();
    }

    private void deltaSubscription_ActivityPaused()
    {
        //throw new NotImplementedException();
    }

    private void deltaSubscription_ConnectionRestored(TimeSpan obj)
    {
        //throw new NotImplementedException();
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