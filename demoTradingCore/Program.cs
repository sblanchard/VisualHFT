using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using demoTradingCore.Models;
using ExchangeSharp;
using Newtonsoft.Json;
using WatsonWebsocket;

namespace demoTradingCore
{
    internal class Program
    {
        private static readonly Dictionary<eEXCHANGE, Exchange> _EXCHANGES = new Dictionary<eEXCHANGE, Exchange>();
        private static readonly List<string> _SYMBOLS = new List<string> { "BTC-USDT" };

        private static readonly Dictionary<eEXCHANGE, Dictionary<string, string>> _SYMBOLS_EXCH_TO_NORMALIZED =
            new Dictionary<eEXCHANGE, Dictionary<string, string>>();

        private static WatsonWsServer _SERVER_WS;
        private static IEnumerable<ClientMetadata> allWSClients;
        private static Strategy _STRATEGY;
        private static Timer heartBeat_Timer;
        private static readonly int _TIMESPAN_HEARTBEAT_IN_MS = 5000;
        private static readonly int _DEPTH_REQUEST = 10;

        private static async Task Main(string[] args)
        {
            await InitializeWS();
            await InitializeBinance();
            await InitializeCoinbase();
            await InitializeOKEx();
            await InitializeStrategy();

            heartBeat_Timer = new Timer(_TIMESPAN_HEARTBEAT_IN_MS);
            heartBeat_Timer.Elapsed += HeartBeat_Timer_Elapsed;
            heartBeat_Timer.AutoReset = true; // Makes the timer repeat
            heartBeat_Timer.Start(); // Starts the timer


            Console.WriteLine("\n\nPress ENTER to shutdown.");
            Console.ReadLine();
        }

        private static void HeartBeat_Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Send_ExchangesHeartBeat();
        }

        private static async Task InitializeWS()
        {
            Console.Write("Initializing Websocket server...");
            _SERVER_WS = new WatsonWsServer("localhost", 6900);
            _SERVER_WS.ClientConnected += ClientConnected;
            _SERVER_WS.ClientDisconnected += ClientDisconnected;
            _SERVER_WS.ServerStopped += _SERVER_WS_ServerStopped;
            _SERVER_WS.MessageReceived += MessageReceived;
            await _SERVER_WS.StartAsync();
            Console.WriteLine("OK");
        }


        private static async Task InitializeBinance()
        {
            try
            {
                Console.Write("Initializing Binance...");
                var _currEXCH = eEXCHANGE.BINANCE;

                _EXCHANGES.Add(_currEXCH, new Exchange(_currEXCH, _DEPTH_REQUEST));
                _SYMBOLS_EXCH_TO_NORMALIZED.Add(_currEXCH,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

                var exchangeAPI = await ExchangeAPI.GetExchangeAPIAsync<ExchangeBinanceUSAPI>();
                var lstExchangeSymbols = new List<string>();
                foreach (var symbol in _SYMBOLS)
                {
                    var exchangeSym = await exchangeAPI.GlobalMarketSymbolToExchangeMarketSymbolAsync(symbol);
                    if (!_SYMBOLS_EXCH_TO_NORMALIZED[_currEXCH].ContainsKey(exchangeSym))
                        _SYMBOLS_EXCH_TO_NORMALIZED[_currEXCH].Add(exchangeSym, symbol);
                    lstExchangeSymbols.Add(exchangeSym);
                }

                await exchangeAPI.GetFullOrderBookWebSocketAsync(book =>
                {
                    var localSym = _SYMBOLS_EXCH_TO_NORMALIZED[_currEXCH][book.MarketSymbol];
                    SnapshotUpdates(localSym, _currEXCH, book, _DEPTH_REQUEST);
                }, _DEPTH_REQUEST, lstExchangeSymbols.ToArray());

                Console.WriteLine("OK");

                await exchangeAPI.GetTradesWebSocketAsync(trade =>
                {
                    var localSym = _SYMBOLS_EXCH_TO_NORMALIZED[_currEXCH][trade.Key];
                    Send_Trades(_currEXCH, new jsonTrade
                    {
                        Timestamp = trade.Value.Timestamp,
                        Symbol = localSym,
                        Size = trade.Value.Amount,
                        Price = trade.Value.Price,
                        IsBuy = trade.Value.IsBuy,
                        ProviderId = (int)_currEXCH,
                        ProviderName = _EXCHANGES[_currEXCH].ExchangeName
                        //Flags = trade.Value.Flags == ExchangeTradeFlags.
                    });
                    //Console.WriteLine($"Trade: {trade.Key}, Price: {trade.Value.Price}, Amount: {trade.Value.Amount}");                
                    return Task.CompletedTask;
                }, lstExchangeSymbols.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static async Task InitializeCoinbase()
        {
            Console.Write("Initializing Coinbase...");
            var _currEXCH = eEXCHANGE.COINBASE;

            _EXCHANGES.Add(_currEXCH, new Exchange(_currEXCH, _DEPTH_REQUEST));
            _SYMBOLS_EXCH_TO_NORMALIZED.Add(_currEXCH,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            var exchangeAPI = await ExchangeAPI.GetExchangeAPIAsync<ExchangeCoinbaseAPI>();
            var lstExchangeSymbols = new List<string>();
            foreach (var symbol in _SYMBOLS)
            {
                var exchangeSym = await exchangeAPI.GlobalMarketSymbolToExchangeMarketSymbolAsync(symbol);
                if (!_SYMBOLS_EXCH_TO_NORMALIZED[_currEXCH].ContainsKey(exchangeSym))
                    _SYMBOLS_EXCH_TO_NORMALIZED[_currEXCH].Add(exchangeSym, symbol);
                lstExchangeSymbols.Add(exchangeSym);
            }

            await exchangeAPI.GetFullOrderBookWebSocketAsync(book =>
            {
                var localSym = _SYMBOLS_EXCH_TO_NORMALIZED[_currEXCH][book.MarketSymbol];
                SnapshotUpdates(localSym, _currEXCH, book, _DEPTH_REQUEST);
            }, _DEPTH_REQUEST, lstExchangeSymbols.ToArray());

            Console.WriteLine("OK");
            /*await exchangeAPI.GetTradesWebSocketAsync((trade) =>
            {
                Send_Trades(_currEXCH, new Models.jsonTrade()
                {
                    Timestamp =trade.Value.Timestamp,
                    Symbol = trade.Key,
                    Size = trade.Value.Amount,
                    Price = trade.Value.Price,
                    IsBuy = trade.Value.IsBuy,
                    ProviderId = (int)_currEXCH,
                    ProviderName = _EXCHANGES[_currEXCH].ExchangeName,
                    //Flags = trade.Value.Flags == ExchangeTradeFlags.
                });
                //Console.WriteLine($"Trade: {trade.Key}, Price: {trade.Value.Price}, Amount: {trade.Value.Amount}");                
                return Task.CompletedTask;

            }, lstNormalized.ToArray());*/
        }

        private static async Task InitializeOKEx()
        {
            Console.Write("Initializing OKEx...");
            var _currEXCH = eEXCHANGE.OKEX;

            _EXCHANGES.Add(_currEXCH, new Exchange(_currEXCH, _DEPTH_REQUEST));
            _SYMBOLS_EXCH_TO_NORMALIZED.Add(_currEXCH,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            var exchangeAPI = await ExchangeAPI.GetExchangeAPIAsync<ExchangeOKExAPI>();
            var lstExchangeSymbols = new List<string>();
            foreach (var symbol in _SYMBOLS)
            {
                var exchangeSym = await exchangeAPI.GlobalMarketSymbolToExchangeMarketSymbolAsync(symbol);
                if (!_SYMBOLS_EXCH_TO_NORMALIZED[_currEXCH].ContainsKey(exchangeSym))
                    _SYMBOLS_EXCH_TO_NORMALIZED[_currEXCH].Add(exchangeSym, symbol);
                lstExchangeSymbols.Add(exchangeSym);
            }


            await exchangeAPI.GetFullOrderBookWebSocketAsync(book =>
            {
                if (_SYMBOLS_EXCH_TO_NORMALIZED[_currEXCH].ContainsKey(book.MarketSymbol))
                {
                    var localSym = _SYMBOLS_EXCH_TO_NORMALIZED[_currEXCH][book.MarketSymbol];
                    SnapshotUpdates(localSym, _currEXCH, book, _DEPTH_REQUEST);
                }
            }, _DEPTH_REQUEST, lstExchangeSymbols.ToArray());

            Console.WriteLine("OK");
            await exchangeAPI.GetTradesWebSocketAsync(trade =>
            {
                var localSym = _SYMBOLS_EXCH_TO_NORMALIZED[_currEXCH][trade.Key];
                Send_Trades(_currEXCH, new jsonTrade
                {
                    Timestamp = trade.Value.Timestamp.ToLocalTime(),
                    Symbol = localSym,
                    Size = trade.Value.Amount,
                    Price = trade.Value.Price,
                    IsBuy = trade.Value.IsBuy,
                    ProviderId = (int)_currEXCH,
                    ProviderName = _EXCHANGES[_currEXCH].ExchangeName
                    //Flags = trade.Value.Flags == ExchangeTradeFlags.
                });
                //Console.WriteLine($"Trade: {trade.Key}, Price: {trade.Value.Price}, Amount: {trade.Value.Amount}");                
                return Task.CompletedTask;
            }, lstExchangeSymbols.ToArray());
        }

        private static async Task InitializeStrategy()
        {
            Console.Write("Initializing Strategy...");
            _STRATEGY = new Strategy(_EXCHANGES.Select(x => x.Value).ToList(), _SYMBOLS.First());
            _STRATEGY.OnStrategyExposure += _STRATEGY_OnStrategyExposure;
            ;
            Console.WriteLine("OK");
        }

        private static void _STRATEGY_OnStrategyExposure(object sender, StrategyExposureEventArgs e)
        {
            //send heart beat with all strategies:
            // in this demo we only have one strategy running
            var json_Exp = new Json_Exposure
            {
                StrategyName = _STRATEGY.GetStrategyName(), SizeExposed = e.SizeExposed, Symbol = e.Symbol,
                UnrealizedPL = e.UnrealizedPL
            };
            var toSendExposure = new JsonExposures();
            toSendExposure.dataObj = new List<Json_Exposure> { json_Exp };

            Send_toWS(toSendExposure);
        }

        private static void SnapshotUpdates(string symbol, eEXCHANGE exchange, ExchangeOrderBook ob, int depth)
        {
            if (!_EXCHANGES.ContainsKey(exchange))
                _EXCHANGES.Add(exchange, new Exchange(exchange, depth));
            _EXCHANGES[exchange].UpdateSnapshot(symbol, ob, depth);
            _STRATEGY.UpdateSnapshot(ob);

            var toSend = new jsonMarkets();
            toSend.type = "Market";
            toSend.dataObj = _EXCHANGES[exchange].GetSnapshots().dataObj;
            SendMarketData_toWS(toSend);

            //send heart beat with all strategies:
            // in this demo we only have one strategy running
            var json_Strategy = new Json_Strategy { StrategyCode = _STRATEGY.GetStrategyName() };
            var toSendStrategy = new jsonStrategies();
            toSendStrategy.dataObj = new List<Json_Strategy> { json_Strategy };
            Send_toWS(toSendStrategy);
        }

        private static void SendMarketData_toWS(jsonMarkets toSend)
        {
            if (allWSClients == null || !allWSClients.Any())
                return;
            var msg = JsonConvert.SerializeObject(toSend);
            foreach (var cli in allWSClients)
            {
                var result = _SERVER_WS.SendAsync(cli.Guid, msg).Result;
            }
        }

        private static void Send_toWS(Json_BaseData toSend)
        {
            if (allWSClients == null || !allWSClients.Any())
                return;
            var msg = JsonConvert.SerializeObject(toSend);
            foreach (var cli in allWSClients)
            {
                var result = _SERVER_WS.SendAsync(cli.Guid, msg).Result;
            }
        }

        private static void Send_ExchangesHeartBeat()
        {
            var toSend = new Json_HeartBeats();
            toSend.dataObj = new List<Json_HeartBeat>();
            var _data = new List<Json_HeartBeat>();
            foreach (var exchange in _EXCHANGES)
            {
                var json_HeartBeat = new Json_HeartBeat();
                json_HeartBeat.ProviderID = (int)exchange.Key;
                json_HeartBeat.ProviderName = exchange.Value.ExchangeName;
                if (!exchange.Value.LastUpdated.HasValue ||
                    exchange.Value.LastUpdated.Value.AddMilliseconds(_TIMESPAN_HEARTBEAT_IN_MS) < DateTime.Now)
                    json_HeartBeat.Status = (int)eSESSIONSTATUS.BOTH_DISCONNECTED;
                else
                    json_HeartBeat.Status = (int)eSESSIONSTATUS.BOTH_CONNECTED;
                if (json_HeartBeat.Status == 3)
                    Console.WriteLine("++ ATENTION: " + exchange.Value.ExchangeName + " DISCONNECTED or NO DATA...");
                _data.Add(json_HeartBeat);
            }

            toSend.dataObj = _data;

            Send_toWS(toSend);
        }

        private static void Send_Trades(eEXCHANGE exchange, jsonTrade trade)
        {
            var toSend = new jsonTrades();
            toSend.dataObj = new List<jsonTrade> { trade };
            Send_toWS(toSend);
        }

        #region webserver callbacks

        private static void ClientConnected(object sender, ConnectionEventArgs args)
        {
            Console.WriteLine("Client connected: " + args.Client.IpPort);
            allWSClients = _SERVER_WS.ListClients();
        }

        private static void ClientDisconnected(object sender, DisconnectionEventArgs args)
        {
            Console.WriteLine("Client disconnected: " + args.Client.IpPort);
        }

        private static void MessageReceived(object sender, MessageReceivedEventArgs args)
        {
            Console.WriteLine("Message received from " + args.Client.IpPort + ": " +
                              Encoding.UTF8.GetString(args.Data.ToArray()));
        }

        private static void _SERVER_WS_ServerStopped(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}