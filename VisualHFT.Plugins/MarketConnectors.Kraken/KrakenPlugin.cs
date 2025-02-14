using Kraken.Net;
using Kraken.Net.Clients;
using Kraken.Net.Objects.Models;
using Kraken.Net.Enums;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using MarketConnectors.Kraken.Model;
using MarketConnectors.Kraken.UserControls;
using MarketConnectors.Kraken.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VisualHFT.Commons.PluginManager;
using VisualHFT.UserSettings;
using VisualHFT.Commons.Pools;
using VisualHFT.Commons.Model;
using VisualHFT.Commons.Helpers;
using CryptoExchange.Net.Objects.Sockets;
using VisualHFT.Enums;
using VisualHFT.PluginManager;
using Kraken.Net.Objects.Models.Socket;
using VisualHFT.Commons.Interfaces;
using Newtonsoft.Json.Linq;
using System.IO;

namespace MarketConnectors.Kraken
{
    public class KrakenPlugin : BasePluginDataRetriever, IDataRetrieverTestable
    {
        private bool _disposed = false; // to track whether the object has been disposed

        private PlugInSettings _settings;
        private KrakenSocketClient _socketClient;
        private KrakenRestClient _restClient;
        private Dictionary<string, VisualHFT.Model.OrderBook> _localOrderBooks = new Dictionary<string, VisualHFT.Model.OrderBook>();
        private Dictionary<string, HelperCustomQueue<Tuple<DateTime, string, KrakenBookUpdate>>> _eventBuffers =
            new Dictionary<string, HelperCustomQueue<Tuple<DateTime, string, KrakenBookUpdate>>>();

        private Dictionary<string, HelperCustomQueue<Tuple<string, KrakenTradeUpdate>>> _tradesBuffers =
            new Dictionary<string, HelperCustomQueue<Tuple<string, KrakenTradeUpdate>>>();

        private int pingFailedAttempts = 0;
        private System.Timers.Timer _timerPing;
        private CallResult<UpdateSubscription> deltaSubscription;
        private CallResult<UpdateSubscription> tradesSubscription;

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<string, VisualHFT.Model.Order> _localUserOrders = new Dictionary<string, VisualHFT.Model.Order>();

        private readonly CustomObjectPool<VisualHFT.Model.Trade> tradePool = new CustomObjectPool<VisualHFT.Model.Trade>();//pool of Trade objects


        public override string Name { get; set; } = "Kraken Plugin";
        public override string Version { get; set; } = "1.0.0";
        public override string Description { get; set; } = "Connects to Kraken websockets.";
        public override string Author { get; set; } = "VisualHFT";
        public override ISetting Settings { get => _settings; set => _settings = (PlugInSettings)value; }
        public override Action CloseSettingWindow { get; set; }

        public KrakenPlugin()
        {
            SetReconnectionAction(InternalStartAsync);
            log.Info($"{this.Name} has been loaded.");
        }
        ~KrakenPlugin()
        {
            Dispose(false);
        }

        public override async Task StartAsync()
        {

            await base.StartAsync();//call the base first
            _socketClient = new KrakenSocketClient(options =>
            {

                if (_settings.ApiKey != "" && _settings.ApiSecret != "")
                    options.ApiCredentials = new ApiCredentials(_settings.ApiKey, _settings.ApiSecret);
                options.Environment = KrakenEnvironment.Live;
            });

            _restClient = new KrakenRestClient(options =>
            {
                if (_settings.ApiKey != "" && _settings.ApiSecret != "")
                    options.ApiCredentials = new ApiCredentials(_settings.ApiKey, _settings.ApiSecret);
                options.Environment = KrakenEnvironment.Live;
            });

            var balance = await _restClient.SpotApi.Account.GetBalancesAsync();

            try
            {
                await InternalStartAsync();
                if (Status == ePluginStatus.STOPPED_FAILED) //check again here for failure
                    return;
                log.Info($"Plugin has successfully started.");
                RaiseOnDataReceived(GetProviderModel(eSESSIONSTATUS.CONNECTED));
                Status = ePluginStatus.STARTED;


            }
            catch (Exception ex)
            {
                var _error = ex.Message;
                log.Error(_error, ex);
                await HandleConnectionLost(_error, ex);
            }
        }
        private async Task InternalStartAsync()
        {
            await ClearAsync();

            // Initialize event buffer for each symbol
            foreach (var symbol in GetAllNormalizedSymbols())
            {
                _eventBuffers.Add(symbol, new HelperCustomQueue<Tuple<DateTime, string, KrakenBookUpdate>>($"<Tuple<DateTime, string, KrakenOrderBookEntry>>_{this.Name.Replace(" Plugin", "")}", eventBuffers_onReadAction, eventBuffers_onErrorAction));
                _tradesBuffers.Add(symbol, new HelperCustomQueue<Tuple<string, KrakenTradeUpdate>>($"<Tuple<DateTime, string, KrakenOrderBookEntry>>_{this.Name.Replace(" Plugin", "")}", tradesBuffers_onReadAction, tradesBuffers_onErrorAction));
            }

            await InitializeSnapshotsAsync();
            await InitializeTradesAsync();
            await InitializeDeltasAsync();
            await InitializePingTimerAsync();
            await InitializeUserPrivateOrders();
        }
        public override async Task StopAsync()
        {
            Status = ePluginStatus.STOPPING;
            log.Info($"{this.Name} is stopping.");

            await ClearAsync();
            RaiseOnDataReceived(new List<VisualHFT.Model.OrderBook>());
            RaiseOnDataReceived(GetProviderModel(eSESSIONSTATUS.DISCONNECTED));

            await base.StopAsync();
        }
        public async Task ClearAsync()
        {

            UnattachEventHandlers(deltaSubscription?.Data);
            UnattachEventHandlers(tradesSubscription?.Data);
            if (_socketClient != null)
                await _socketClient.UnsubscribeAllAsync();
            if (deltaSubscription != null && deltaSubscription.Data != null)
                await deltaSubscription.Data.CloseAsync();
            if (tradesSubscription != null && tradesSubscription.Data != null)
                await tradesSubscription.Data.CloseAsync();
            _timerPing?.Stop();
            _timerPing?.Dispose();

            foreach (var q in _eventBuffers)
                q.Value.Clear();
            _eventBuffers.Clear();

            foreach (var q in _tradesBuffers)
                q.Value.Clear();
            _tradesBuffers.Clear();


            //CLEAR LOB
            if (_localOrderBooks != null)
            {
                foreach (var lob in _localOrderBooks)
                {
                    lob.Value?.Dispose();
                }
                _localOrderBooks.Clear();
            }
        }

        private async Task InitializeTradesAsync()
        {
            foreach (var symbol in GetAllNonNormalizedSymbols())
            {
                var _normalizedSymbol = GetNormalizedSymbol(symbol);
                var _traderQueueRef = _tradesBuffers[_normalizedSymbol];

                log.Info($"{this.Name}: sending WS Trades Subscription {_normalizedSymbol} ");
                tradesSubscription = await _socketClient.SpotApi.SubscribeToTradeUpdatesAsync(
                    symbol,
                    trade =>
                    {
                        // Buffer the trades
                        if (trade.Data != null)
                        {
                            try
                            {
                                foreach (var item in trade.Data)
                                {
                                    item.Timestamp = trade.ReceiveTime; 
                                    _traderQueueRef.Add(
                                        new Tuple<string, KrakenTradeUpdate>(_normalizedSymbol, item));
                                }
                            }
                            catch (Exception ex)
                            {
                                var _error = $"Will reconnect. Unhandled error while receiving trading data for {_normalizedSymbol}.";
                                log.Error(_error, ex);
                                Task.Run(async () => await HandleConnectionLost(_error, ex));
                            }
                        }
                    });
                if (tradesSubscription.Success)
                {
                    AttachEventHandlers(tradesSubscription.Data);
                }
                else
                {
                    var _error = $"Unsuccessful trades subscription for {_normalizedSymbol} error: {tradesSubscription.Error}";
                    throw new Exception(_error);
                }
            }
        }
        private async Task InitializeUserPrivateOrders()
        {
            if (!string.IsNullOrEmpty(this._settings.ApiKey) && !string.IsNullOrEmpty(this._settings.ApiSecret))
            {
                await _socketClient.SpotApi.SubscribeToOrderUpdatesAsync(async neworder =>
                {
                    log.Info(neworder.Data);
                    if (neworder.Data != null)
                    {
                        IEnumerable<KrakenOrderUpdate> item = neworder.Data;

                        foreach (var order in item)
                        {
                           await UpdateUserOrder(order);
                        }
                    }
                }, true, true);
            }
        }
        private async Task UpdateUserOrder(KrakenOrderUpdate item)
        {
            VisualHFT.Model.Order localuserOrder;
            if (!this._localUserOrders.ContainsKey(item.OrderId))
            {
                localuserOrder = new VisualHFT.Model.Order();
                localuserOrder.Currency = GetNormalizedSymbol(item.Symbol);
                localuserOrder.CreationTimeStamp = item.Timestamp;
                localuserOrder.OrderID = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                // localuserOrder.OrderID = long.Parse(item.OrderId);
                localuserOrder.ProviderId = _settings!.Provider.ProviderID;
                localuserOrder.ProviderName = _settings.Provider.ProviderName;
                localuserOrder.CreationTimeStamp = item.EffectiveTime.HasValue ? item.EffectiveTime.Value : item.Timestamp;
                localuserOrder.Quantity = (double)item.OrderQuantity;
                localuserOrder.PricePlaced = (double)item.LimitPrice;
                localuserOrder.Symbol = GetNormalizedSymbol(item.Symbol);
                localuserOrder.TimeInForce = eORDERTIMEINFORCE.GTC;


                if (item.TimeInForce == TimeInForce.IOC)
                {
                    localuserOrder.TimeInForce = eORDERTIMEINFORCE.IOC;
                }
                else if (item.TimeInForce == TimeInForce.GTC)
                {
                    localuserOrder.TimeInForce = eORDERTIMEINFORCE.GTC;
                }
                this._localUserOrders.Add(item.OrderId, localuserOrder);
            }
            else
            {
                localuserOrder = this._localUserOrders[item.OrderId];
            }


            if (item.OrderType == OrderType.Market)
            {
                localuserOrder.OrderType = eORDERTYPE.MARKET;
            }
            else if (item.OrderType == OrderType.Limit)
            {
                localuserOrder.OrderType = eORDERTYPE.LIMIT;
            }
            else
            {
                localuserOrder.OrderType = eORDERTYPE.PEGGED;
            }


            if (item.OrderSide == OrderSide.Buy)
            {
                localuserOrder.Side = eORDERSIDE.Buy;
            }
            if (item.OrderSide == OrderSide.Sell)
            {
                localuserOrder.Side = eORDERSIDE.Sell;
            }

            if (item.OrderEventType == OrderEventType.New || item.OrderEventType == OrderEventType.PendingNew)
            {
                if (item.OrderSide == OrderSide.Buy)
                {
                    localuserOrder.CreationTimeStamp = item.Timestamp;
                    localuserOrder.PricePlaced = (double)item.LimitPrice;
                    localuserOrder.BestBid = (double)item.LimitPrice;
                    localuserOrder.Side = eORDERSIDE.Buy;
                }
                if (item.OrderSide == OrderSide.Sell)
                {
                    localuserOrder.Side = eORDERSIDE.Sell;
                    localuserOrder.BestAsk = (double)item.LimitPrice;
                    localuserOrder.Quantity = (double)item.OrderQuantity;
                }
                localuserOrder.Status = eORDERSTATUS.NEW;
            }
            if (item.OrderEventType == OrderEventType.Expired)
            {
                localuserOrder.Status = eORDERSTATUS.CANCELED;
            }
            if (item.OrderEventType == OrderEventType.Canceled)
            {
                localuserOrder.Status = eORDERSTATUS.CANCELED;
            }
            if (item.OrderEventType == OrderEventType.Filled)
            {
                localuserOrder.Status = eORDERSTATUS.FILLED;
            }


            localuserOrder.LastUpdated = DateTime.Now;
            localuserOrder.FilledPercentage = Math.Round((100 / localuserOrder.Quantity) * localuserOrder.FilledQuantity, 2);
            RaiseOnDataReceived(localuserOrder);
        }
        private async Task InitializeDeltasAsync()
        {
            foreach (var symbol in GetAllNonNormalizedSymbols())
            {
                var normalizedSymbol = GetNormalizedSymbol(symbol);
                log.Info($"{this.Name}: sending WS Trades Subscription {normalizedSymbol} ");

                deltaSubscription = await _socketClient.SpotApi.SubscribeToAggregatedOrderBookUpdatesAsync(
                    symbol,
                    _settings.DepthLevels,
                    data =>
                    {
                        // Buffer the events
                        if (data.Data != null)
                        {
                            try
                            {
                                if (Math.Abs(DateTime.Now.Subtract(data.ReceiveTime.ToLocalTime()).TotalSeconds) > 1)
                                {
                                    var _msg = $"Rates are coming late at {Math.Abs(DateTime.Now.Subtract(data.ReceiveTime.ToLocalTime()).TotalSeconds)} seconds.";
                                    log.Warn(_msg);
                                    HelperNotificationManager.Instance.AddNotification(this.Name, _msg, HelprNorificationManagerTypes.WARNING, HelprNorificationManagerCategories.PLUGINS);
                                }
                                _eventBuffers[normalizedSymbol].Add(
                                       new Tuple<DateTime, string, KrakenBookUpdate>(
                                           data.ReceiveTime.ToLocalTime(), normalizedSymbol, data.Data));
                            }
                            catch (Exception ex)
                            {
                                string _normalizedSymbol = "(null)";
                                if (data != null && data.Data != null)
                                    _normalizedSymbol = GetNormalizedSymbol(data.Data.Symbol);


                                var _error = $"Will reconnect. Unhandled error while receiving delta market data for {_normalizedSymbol}.";
                                log.Error(_error, ex);
                                Task.Run(async () => await HandleConnectionLost(_error, ex));
                            }
                        }
                    }, null, new CancellationToken());
                if (deltaSubscription.Success)
                {
                    AttachEventHandlers(deltaSubscription.Data);
                }
                else
                {
                    var _error = $"Unsuccessful deltas subscription for {normalizedSymbol} error: {deltaSubscription.Error}";
                    throw new Exception(_error);
                }
            }
        }
        private async Task InitializeSnapshotsAsync()
        {
            foreach (var symbol in GetAllNonNormalizedSymbols())
            {
                var normalizedSymbol = GetNormalizedSymbol(symbol);
                if (!_localOrderBooks.ContainsKey(normalizedSymbol))
                {
                    _localOrderBooks.Add(normalizedSymbol, null);
                }
                log.Info($"{this.Name}: Getting snapshot {normalizedSymbol} level 2");

                // Fetch initial depth snapshot
                var depthSnapshot = await _restClient.SpotApi.ExchangeData.GetOrderBookAsync(symbol, _settings.DepthLevels);
                if (depthSnapshot.Success)
                {
                    _localOrderBooks[normalizedSymbol] = ToOrderBookModel(depthSnapshot.Data, normalizedSymbol);
                    log.Info($"{this.Name}: LOB {normalizedSymbol} level 2 Successfully loaded.");
                }
                else
                {
                    var _error = $"Unsuccessful snapshot request for {normalizedSymbol} error: {depthSnapshot.ResponseStatusCode} - {depthSnapshot.Error}";
                    throw new Exception(_error);
                }
            }
        }
        private async Task InitializePingTimerAsync()
        {
            _timerPing?.Stop();
            _timerPing?.Dispose();

            _timerPing = new System.Timers.Timer(3000); // Set the interval to 3000 milliseconds (3 seconds)
            _timerPing.Elapsed += async (sender, e) => await DoPingAsync();
            _timerPing.AutoReset = true;
            _timerPing.Enabled = true; // Start the timer
        }

        private void eventBuffers_onReadAction(Tuple<DateTime, string, KrakenBookUpdate> eventData)
        {
            UpdateOrderBook(eventData.Item3, eventData.Item2, eventData.Item1);
        }
        private void eventBuffers_onErrorAction(Exception ex)
        {
            var _error = $"Will reconnect. Unhandled error in the Market Data Queue: {ex.Message}";

            log.Error(_error, ex);
            Task.Run(async () => await HandleConnectionLost(_error, ex));
        }
        private void tradesBuffers_onReadAction(Tuple<string, KrakenTradeUpdate> item)
        {
            var trade = tradePool.Get();
            trade.Price = item.Item2.Price;
            trade.Size = Math.Abs(item.Item2.Quantity);
            trade.Symbol = item.Item1;
            trade.Timestamp = item.Item2.Timestamp.ToLocalTime();
            trade.ProviderId = _settings.Provider.ProviderID;
            trade.ProviderName = _settings.Provider.ProviderName;
            trade.IsBuy = item.Item2.Quantity > 0;
            trade.MarketMidPrice = _localOrderBooks[item.Item1].MidPrice;

            RaiseOnDataReceived(trade);
            tradePool.Return(trade);
        }
        private void tradesBuffers_onErrorAction(Exception ex)
        {
            var _error = $"Will reconnect. Unhandled error in the Trades Queue: {ex.Message}";

            log.Error(_error, ex);
            Task.Run(async () => await HandleConnectionLost(_error, ex));
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
            if (Status != ePluginStatus.STOPPING && Status != ePluginStatus.STOPPED) //avoid executing this if we are actually trying to disconnect.
                Task.Run(async () => await HandleConnectionLost("Websocket has been closed from the server (no informed reason)."));
        }
        private void deltaSubscription_ConnectionLost()
        {
            Task.Run(async () => await HandleConnectionLost("Websocket connection has been lost (no informed reason)."));
        }
        private void deltaSubscription_Exception(Exception obj)
        {
            string _error = $"Websocket error: {obj.Message}";
            log.Error(_error, obj);
            HelperNotificationManager.Instance.AddNotification(this.Name, _error, HelprNorificationManagerTypes.ERROR, HelprNorificationManagerCategories.PLUGINS);

            Task.Run(StopAsync);

            Status = ePluginStatus.STOPPED_FAILED;
            RaiseOnDataReceived(GetProviderModel(eSESSIONSTATUS.DISCONNECTED_FAILED));
        }
        #endregion


        private void UpdateOrderBook(KrakenBookUpdate lob_update, string symbol, DateTime ts)
        {
            if (!_localOrderBooks.ContainsKey(symbol))
                return;

            var local_lob = _localOrderBooks[symbol];

            if (local_lob != null)
            {
                foreach (var item in lob_update.Bids)
                {
                    if (item.Quantity != 0)
                    {
                        local_lob.AddOrUpdateLevel(new DeltaBookItem()
                        {
                            MDUpdateAction = eMDUpdateAction.None,
                            Price = (double)item.Price,
                            Size = (double)item.Quantity,
                            IsBid = true,
                            LocalTimeStamp = DateTime.Now,
                            ServerTimeStamp = ts,
                            Symbol = symbol
                        });
                    }
                    else
                        local_lob.DeleteLevel(new DeltaBookItem()
                        {
                            MDUpdateAction = eMDUpdateAction.Delete,
                            Price = (double)item.Price,
                            //Size = (double)item.Quantity,
                            IsBid = true,
                            LocalTimeStamp = DateTime.Now,
                            ServerTimeStamp = ts,
                            Symbol = symbol
                        });
                }
                foreach (var item in lob_update.Asks)
                {
                    if (item.Quantity != 0)
                    {
                        local_lob.AddOrUpdateLevel(new DeltaBookItem()
                        {
                            MDUpdateAction = eMDUpdateAction.None,
                            Price = (double)item.Price,
                            Size = (double)item.Quantity,
                            IsBid = false,
                            LocalTimeStamp = DateTime.Now,
                            ServerTimeStamp = ts,
                            Symbol = symbol
                        });
                    }
                    else
                        local_lob.DeleteLevel(new DeltaBookItem()
                        {
                            MDUpdateAction = eMDUpdateAction.Delete,
                            Price = (double)item.Price,
                            //Size = (double)item.Quantity,
                            IsBid = false,
                            LocalTimeStamp = DateTime.Now,
                            ServerTimeStamp = ts,
                            Symbol = symbol
                        });
                }

                RaiseOnDataReceived(local_lob);
            }
        }
        private async Task DoPingAsync()
        {
            try
            {
                if (Status == ePluginStatus.STOPPED || Status == ePluginStatus.STOPPING || Status == ePluginStatus.STOPPED_FAILED)
                    return; //do not ping if any of these statues

                bool isConnected = _socketClient.CurrentConnections > 0;
                if (!isConnected)
                {
                    throw new Exception("The socket seems to be disconnected.");
                }


                DateTime ini = DateTime.Now;
                var result = await _restClient.SpotApi.ExchangeData.GetSystemStatusAsync();
                if (result != null)
                {
                    var timeLapseInMicroseconds = DateTime.Now.Subtract(ini).TotalMicroseconds;


                    // Connection is healthy
                    pingFailedAttempts = 0; // Reset the failed attempts on a successful ping

                    RaiseOnDataReceived(GetProviderModel(eSESSIONSTATUS.CONNECTED));
                }
                else
                {
                    // Consider the ping failed
                    throw new Exception("Ping failed, result was null.");
                }
            }
            catch (Exception ex)
            {

                if (++pingFailedAttempts >= 5) //5 attempts
                {
                    var _error = $"Will reconnect. Unhandled error in DoPingAsync. Initiating reconnection. {ex.Message}";

                    log.Error(_error, ex);

                    Task.Run(async () => await HandleConnectionLost(_error, ex));
                }
            }

        }
        private VisualHFT.Model.OrderBook ToOrderBookModel(KrakenOrderBook data, string symbol)
        {
            var identifiedPriceDecimalPlaces = RecognizeDecimalPlacesAutomatically(data.Asks.Select(x => x.Price));

            var lob = new VisualHFT.Model.OrderBook(symbol, identifiedPriceDecimalPlaces, _settings.DepthLevels);
            lob.ProviderID = _settings.Provider.ProviderID;
            lob.ProviderName = _settings.Provider.ProviderName;
            lob.SizeDecimalPlaces = RecognizeDecimalPlacesAutomatically(data.Asks.Select(x => x.Quantity));

            var _asks = new List<VisualHFT.Model.BookItem>();
            var _bids = new List<VisualHFT.Model.BookItem>();
            data.Asks.ToList().ForEach(x =>
            {
                _asks.Add(new VisualHFT.Model.BookItem()
                {
                    IsBid = false,
                    Price = (double)x.Price,
                    Size = (double)x.Quantity,
                    LocalTimeStamp = DateTime.Now,
                    ServerTimeStamp = DateTime.Now,
                    Symbol = lob.Symbol,
                    PriceDecimalPlaces = lob.PriceDecimalPlaces,
                    SizeDecimalPlaces = lob.SizeDecimalPlaces,
                    ProviderID = lob.ProviderID,
                });
            });
            data.Bids.ToList().ForEach(x =>
            {
                _bids.Add(new VisualHFT.Model.BookItem()
                {
                    IsBid = true,
                    Price = (double)x.Price,
                    Size = (double)x.Quantity,
                    LocalTimeStamp = DateTime.Now,
                    ServerTimeStamp = DateTime.Now,
                    Symbol = lob.Symbol,
                    PriceDecimalPlaces = lob.PriceDecimalPlaces,
                    SizeDecimalPlaces = lob.SizeDecimalPlaces,
                    ProviderID = lob.ProviderID,
                });
            });

            lob.LoadData(
                _asks.OrderBy(x => x.Price).Take(_settings.DepthLevels),
                _bids.OrderByDescending(x => x.Price).Take(_settings.DepthLevels)
                );
            return lob;
        }


        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    UnattachEventHandlers(deltaSubscription?.Data);
                    UnattachEventHandlers(tradesSubscription?.Data);
                    _socketClient?.UnsubscribeAllAsync();
                    _socketClient?.Dispose();
                    _restClient?.Dispose();
                    _timerPing?.Dispose();

                    foreach (var q in _eventBuffers)
                        q.Value?.Dispose();
                    _eventBuffers.Clear();

                    foreach (var q in _tradesBuffers)
                        q.Value?.Dispose();
                    _tradesBuffers.Clear();


                    if (_localOrderBooks != null)
                    {
                        foreach (var lob in _localOrderBooks)
                        {
                            lob.Value?.Dispose();
                        }
                        _localOrderBooks.Clear();
                    }

                    base.Dispose();
                }
            }
        }

        protected override void LoadSettings()
        {
            _settings = LoadFromUserSettings<PlugInSettings>();
            if (_settings == null)
            {
                InitializeDefaultSettings();
            }
            if (_settings.Provider == null) //To prevent back compability with older setting formats
            {
                _settings.Provider = new VisualHFT.Model.Provider() { ProviderID = 3, ProviderName = "Kraken" };
            }
            ParseSymbols(string.Join(',', _settings.Symbols.ToArray())); //Utilize normalization function
        }

        protected override void SaveSettings()
        {
            SaveToUserSettings(_settings);
        }

        protected override void InitializeDefaultSettings()
        {
            _settings = new PlugInSettings()
            {
                ApiKey = "",
                ApiSecret = "",
                DepthLevels = 25,
                Provider = new VisualHFT.Model.Provider() { ProviderID = 3, ProviderName = "Kraken" },
                Symbols = new List<string>() { "BTC/USD", "ETH/USD" } // Add more symbols as needed
            };
            SaveToUserSettings(_settings);
        }
        public override object GetUISettings()
        {
            PluginSettingsView view = new PluginSettingsView();
            PluginSettingsViewModel viewModel = new PluginSettingsViewModel(CloseSettingWindow);
            viewModel.ApiSecret = _settings.ApiSecret;
            viewModel.ApiKey = _settings.ApiKey;
            viewModel.APIPassPhrase = _settings.APIPassPhrase;

            viewModel.DepthLevels = _settings.DepthLevels;
            viewModel.ProviderId = _settings.Provider.ProviderID;
            viewModel.ProviderName = _settings.Provider.ProviderName;
            viewModel.Symbols = _settings.Symbols;
            viewModel.UpdateSettingsFromUI = () =>
            {
                _settings.ApiSecret = viewModel.ApiSecret;
                _settings.ApiKey = viewModel.ApiKey;
                _settings.APIPassPhrase = viewModel.APIPassPhrase;
                _settings.DepthLevels = viewModel.DepthLevels;
                _settings.Provider = new VisualHFT.Model.Provider() { ProviderID = viewModel.ProviderId, ProviderName = viewModel.ProviderName };
                _settings.Symbols = viewModel.Symbols;
                SaveSettings();
                ParseSymbols(string.Join(',', _settings.Symbols.ToArray()));

                //run this because it will allow to reconnect with the new values
                RaiseOnDataReceived(GetProviderModel(eSESSIONSTATUS.CONNECTING));
                Status = ePluginStatus.STARTING;
                Task.Run(async () => await HandleConnectionLost($"{this.Name} is starting (from reloading settings).", null, true));


            };
            // Display the view, perhaps in a dialog or a new window.
            view.DataContext = viewModel;
            return view;
        }


        //FOR UNIT TESTING PURPOSES
        public void InjectSnapshot(VisualHFT.Model.OrderBook snapshotModel, long sequence)
        {
            var localModel = new KrakenOrderBook();
            localModel.Bids = snapshotModel.Bids.Select(x => new KrakenOrderBookEntry() { Price = x.Price.ToDecimal(), Quantity = x.Size.ToDecimal(), Timestamp = x.LocalTimeStamp}).ToList();
            localModel.Asks = snapshotModel.Asks.Select(x => new KrakenOrderBookEntry() { Price = x.Price.ToDecimal(), Quantity = x.Size.ToDecimal(), Timestamp = x.LocalTimeStamp}).ToList();
            _settings.DepthLevels = snapshotModel.MaxDepth; //force depth received

            var symbol = snapshotModel.Symbol;

            if (!_localOrderBooks.ContainsKey(symbol))
            {
                _localOrderBooks.Add(symbol, ToOrderBookModel(localModel, symbol));
            }
            else
                _localOrderBooks[symbol] = ToOrderBookModel(localModel, symbol);

            _localOrderBooks[symbol].Sequence = sequence;// KRAKEN does not provide sequence numbers

            RaiseOnDataReceived(_localOrderBooks[symbol]);

        }

        public void InjectDeltaModel(List<DeltaBookItem> bidDeltaModel, List<DeltaBookItem> askDeltaModel)
        {
            var symbol = bidDeltaModel?.FirstOrDefault()?.Symbol;
            if (symbol == null)
                symbol = askDeltaModel?.FirstOrDefault()?.Symbol;
            if (string.IsNullOrEmpty(symbol))
                throw new Exception("Couldn't find the symbol for this model.");
            var ts = DateTime.Now;

            var localModel = new KrakenBookUpdate();
            localModel.Bids = bidDeltaModel?.Select(x => new KrakenBookUpdateEntry() { Price = x.Price.ToDecimal(), Quantity = x.Size.ToDecimal()}).ToList();
            localModel.Asks = askDeltaModel?.Select(x => new KrakenBookUpdateEntry() { Price = x.Price.ToDecimal(), Quantity = x.Size.ToDecimal() }).ToList();

            //************************************************************************************************************************
            //sequence is not provided by KRAKEN (then make adjustments to this method, so Unit tests don't fail)
            //************************************************************************************************************************
            long maxSequence = Math.Max(bidDeltaModel.Max(x => x.Sequence), askDeltaModel.Max(x => x.Sequence));
            long minSequence = Math.Min(bidDeltaModel.Min(x => x.Sequence), askDeltaModel.Min(x => x.Sequence));
            if (_localOrderBooks.ContainsKey(symbol))
            {
                if (minSequence < _localOrderBooks[symbol].Sequence)
                {
                    bidDeltaModel.RemoveAll(x => x.Sequence <= _localOrderBooks[symbol].Sequence);
                    askDeltaModel.RemoveAll(x => x.Sequence <= _localOrderBooks[symbol].Sequence);
                    localModel.Bids = bidDeltaModel?.Select(x => new KrakenBookUpdateEntry()
                        { Price = x.Price.ToDecimal(), Quantity = x.Size.ToDecimal() }).ToList();
                    localModel.Asks = askDeltaModel?.Select(x => new KrakenBookUpdateEntry()
                        { Price = x.Price.ToDecimal(), Quantity = x.Size.ToDecimal() }).ToList();
                }
                else if (minSequence != _localOrderBooks[symbol].Sequence + 1)
                {
                    throw new Exception("Sequence numbers are not in order.");
                }
                else
                    _localOrderBooks[symbol].Sequence = maxSequence;
            }
            //************************************************************************************************************************
            //************************************************************************************************************************

            UpdateOrderBook(localModel, symbol, ts);
        }

        public List<VisualHFT.Model.Order> ExecutePrivateMessageScenario(eTestingPrivateMessageScenario scenario)
        {
            //depending on the scenario, load its message(s)
            string _file = "";
            if (scenario == eTestingPrivateMessageScenario.SCENARIO_1)
                _file = "PrivateMessages_Scenario1.json";
            else if (scenario == eTestingPrivateMessageScenario.SCENARIO_2)
                _file = "PrivateMessages_Scenario2.json";
            else if (scenario == eTestingPrivateMessageScenario.SCENARIO_3)
                _file = "PrivateMessages_Scenario3.json";
            else if (scenario == eTestingPrivateMessageScenario.SCENARIO_4)
                _file = "PrivateMessages_Scenario4.json";
            else if (scenario == eTestingPrivateMessageScenario.SCENARIO_5)
                _file = "PrivateMessages_Scenario5.json";
            else if (scenario == eTestingPrivateMessageScenario.SCENARIO_6)
                _file = "PrivateMessages_Scenario6.json";
            else if (scenario == eTestingPrivateMessageScenario.SCENARIO_7)
                _file = "PrivateMessages_Scenario7.json";
            else if (scenario == eTestingPrivateMessageScenario.SCENARIO_8)
                _file = "PrivateMessages_Scenario8.json";
            else if (scenario == eTestingPrivateMessageScenario.SCENARIO_9)
            {
                _file = "PrivateMessages_Scenario9.json";
                throw new Exception("Messages collected for this scenario don't look good.");
            }
            else if (scenario == eTestingPrivateMessageScenario.SCENARIO_10)
            {
                _file = "PrivateMessages_Scenario10.json";
                throw new Exception("Messages were not collected for this scenario.");
            }

            string jsonString = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, $"jsonMessages/{_file}"));

            //DESERIALIZE EXCHANGES MODEL
            List<KrakenOrderUpdate> modelList = new List<KrakenOrderUpdate>();
            var dataEvents = new List<KrakenOrderUpdate>();
            var jsonArray = JArray.Parse(jsonString);
            foreach (var jsonObject in jsonArray)
            {
                JToken dataToken = jsonObject["data"];
                string dataJsonString = dataToken.ToString();
                var jsonArrayData = JArray.Parse(dataJsonString);
                foreach (var jsonObjectData in jsonArrayData)
                {
                    string jsonObjectDataString = jsonObjectData.ToString(); // Convert JObject to string for debugging
                    KrakenOrderUpdate _data = JsonParser.Parse(jsonObjectDataString);
                    if (_data != null)
                        modelList.Add(_data);
                }
            }
            //END DESERIALIZE EXCHANGES MODEL



            //UPDATE VISUALHFT CORE & CREATE MODEL TO RETURN
            if (!modelList.Any())
                throw new Exception("No data was found in the json file.");
            foreach (var item in modelList)
            {
                UpdateUserOrder(item);
            }
            //END UPDATE VISUALHFT CORE



            //CREATE MODEL TO RETURN (First, identify the order that was sent, then use that one with the updated values)
            var dicOrders = new Dictionary<string, VisualHFT.Model.Order>(); //we need to use dictionary to identify orders (because exchanges orderId is string)

            foreach (var item in modelList)
            {
                if (item.OrderStatus == OrderStatusUpdate.Pending) //identify the order sent
                {
                    var order = new VisualHFT.Model.Order()
                    {
                        CreationTimeStamp = item.Timestamp,
                        PricePlaced = item.LimitPrice.ToDouble(),
                        ProviderId = _settings.Provider.ProviderID,
                        ProviderName = _settings.Provider.ProviderName,
                        Symbol = item.Symbol,
                        Side = item.OrderSide == OrderSide.Buy ? eORDERSIDE.Buy : eORDERSIDE.Sell,
                    };
                    if (item.OrderType != OrderType.Market)
                        order.Quantity = item.OrderQuantity.ToDouble();

                    //Since OrderID needs to match (the one we are creating for this scenario vs the one created by this class) use the localUserOrders to get it
                    order.OrderID = this._localUserOrders[item.OrderId].OrderID; //DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (item.TimeInForce == TimeInForce.IOC)
                        order.TimeInForce = eORDERTIMEINFORCE.IOC;
                    else if (item.TimeInForce == TimeInForce.GTC || item.TimeInForce == TimeInForce.GTD)
                        order.TimeInForce = eORDERTIMEINFORCE.GTC;

                    if (item.OrderType == OrderType.Limit)
                        order.OrderType = eORDERTYPE.LIMIT;
                    else if (item.OrderType == OrderType.Market)
                        order.OrderType = eORDERTYPE.MARKET;
                    else
                        order.OrderType = eORDERTYPE.LIMIT;
                    order.Status = eORDERSTATUS.SENT;

                    dicOrders.Add(item.OrderId, order);
                }
                else if (item.OrderStatus == OrderStatusUpdate.New)
                {
                    var orderToUpdate = dicOrders[item.OrderId];
                    orderToUpdate.Status = eORDERSTATUS.NEW;
                    if (item.OrderEventType == OrderEventType.Amended) //the order was amended
                    {
                        orderToUpdate.PricePlaced = item.LimitPrice.ToDouble();
                        if (item.OrderQuantity.HasValue)
                            orderToUpdate.Quantity = item.OrderQuantity.ToDouble();
                        orderToUpdate.LastUpdated = item.Timestamp;
                        if (item.QuantityFilled.HasValue)
                            orderToUpdate.FilledQuantity = item.QuantityFilled.ToDouble();
                        if (item.AveragePrice.HasValue)
                            orderToUpdate.FilledPrice = item.AveragePrice.ToDouble();
                    }
                }
                else if (item.OrderStatus == OrderStatusUpdate.Canceled || item.OrderStatus == OrderStatusUpdate.Expired)
                {
                    var orderToUpdate = dicOrders[item.OrderId];

                    //update needed fields only
                    orderToUpdate.LastUpdated = item.Timestamp;
                    if (item.QuantityFilled.HasValue)
                        orderToUpdate.FilledQuantity = item.QuantityFilled.ToDouble();
                    if (item.AveragePrice.HasValue)
                        orderToUpdate.FilledPrice = item.AveragePrice.ToDouble();

                    orderToUpdate.Status = eORDERSTATUS.CANCELED;
                }
                else if (item.OrderStatus == OrderStatusUpdate.PartiallyFilled || item.OrderStatus == OrderStatusUpdate.Filled)
                {
                    var orderToUpdate = dicOrders[item.OrderId];

                    //update needed fields only
                    orderToUpdate.LastUpdated = item.Timestamp;
                    if (item.QuantityFilled.HasValue)
                        orderToUpdate.FilledQuantity = item.QuantityFilled.ToDouble();
                    if (item.AveragePrice.HasValue)
                        orderToUpdate.FilledPrice = item.AveragePrice.ToDouble();
                    orderToUpdate.Status = (item.OrderStatus == OrderStatusUpdate.PartiallyFilled ? eORDERSTATUS.PARTIALFILLED: eORDERSTATUS.FILLED);
                    if (orderToUpdate.OrderType == eORDERTYPE.MARKET) //update original order and price placed
                    {
                        orderToUpdate.PricePlaced = orderToUpdate.FilledPrice;
                        orderToUpdate.Quantity = orderToUpdate.FilledQuantity;
                    }
                }

            }
            //END CREATE MODEL TO RETURN


            return dicOrders.Values.ToList();

        }


    }





}

