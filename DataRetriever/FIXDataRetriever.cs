using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using QuickFix.Transport;
using VisualHFT.Model;
using Message = QuickFix.Message;
using Provider = VisualHFT.ViewModel.Model.Provider;

namespace VisualHFT.DataRetriever;

public class FIXDataRetriever : IDataRetriever, IApplication
{
    private bool _disposed; // to track whether the object has been disposed
    private readonly IInitiator _initiator;
    private readonly ILogFactory _logFactory;

    private readonly SessionSettings _settings;
    private readonly IMessageStoreFactory _storeFactory;

    public FIXDataRetriever()
    {
        // Configuration settings
        _settings = new SessionSettings();
        var sessionID = new SessionID("FIX.4.4", "YOUR_SENDER_COMP_ID", "YOUR_TARGET_COMP_ID");
        var dictionary = new Dictionary();
        dictionary.SetString("ConnectionType", "initiator");
        dictionary.SetString("ReconnectInterval", "2");
        dictionary.SetString("FileLogPath", "log");
        dictionary.SetString("StartTime", "00:00:00");
        dictionary.SetString("EndTime", "00:00:00");
        dictionary.SetString("HeartBtInt", "30");
        dictionary.SetString("SocketConnectHost", "YOUR_FIX_SERVER_HOST");
        dictionary.SetString("SocketConnectPort", "YOUR_FIX_SERVER_PORT");
        _settings.Set(sessionID, dictionary);

        _storeFactory = new FileStoreFactory(_settings);
        _logFactory = new FileLogFactory(_settings);
        _initiator = new SocketInitiator(this, _storeFactory, _settings, _logFactory);
    }

    // IApplication methods
    public void OnCreate(SessionID sessionId)
    {
    }

    public void OnLogout(SessionID sessionId)
    {
    }

    public void OnLogon(SessionID sessionId)
    {
        // Send market data request after logging in
        SendMarketDataRequest();
    }

    public void ToAdmin(Message message, SessionID sessionId)
    {
        // Handle messages sent to the FIX server (e.g., logon messages)
    }

    public void ToApp(Message message, SessionID sessionId)
    {
        // Handle application-level messages sent to the FIX server
    }

    public void FromAdmin(Message message, SessionID sessionId)
    {
        // Handle administrative messages received from the FIX server (e.g., heartbeats)
    }

    public void FromApp(Message message, SessionID sessionId)
    {
        if (message is MarketDataSnapshotFullRefresh snapshot)
        {
            HandleMarketDataSnapshot(snapshot);
        }
        else if (message is Heartbeat heartbeatMessage)
        {
            Console.WriteLine("Received Heartbeat from: " + sessionId);
            HandleHeartBeat();
        }
        else if (message is TestRequest testRequestMessage) // Check if the message is a Test Request
        {
            var testReqID = testRequestMessage.TestReqID.getValue();
            var responseHeartbeat = new Heartbeat();
            responseHeartbeat.SetField(new TestReqID(testReqID));
            Session.SendToTarget(responseHeartbeat, sessionId);
        }
    }

    public event EventHandler<DataEventArgs> OnDataReceived;

    public async Task StartAsync()
    {
        if (!_initiator.IsLoggedOn) _initiator.Start();
    }

    public async Task StopAsync()
    {
        if (_initiator.IsLoggedOn) _initiator.Stop();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~FIXDataRetriever()
    {
        Dispose(false);
    }


    private void SendMarketDataRequest()
    {
        var marketDataRequest = new MarketDataRequest();

        // Set unique ID for the request
        marketDataRequest.Set(new MDReqID(Guid.NewGuid().ToString()));

        // Request type: 0 = Snapshot
        marketDataRequest.Set(new SubscriptionRequestType('0'));

        // Market depth: 0 = Full book
        marketDataRequest.Set(new MarketDepth(0));

        // Add symbols or other criteria for which you want the snapshot
        var noRelatedSymGroup = new MarketDataRequest.NoRelatedSymGroup();
        noRelatedSymGroup.Set(new Symbol("EUR/USD")); // Replace with your desired symbol
        marketDataRequest.AddGroup(noRelatedSymGroup);

        // Send the message
        Session.SendToTarget(marketDataRequest);
    }

    private void HandleMarketDataSnapshot(MarketDataSnapshotFullRefresh snapshot)
    {
        int? decimalPlaces = null;

        // Extract data from the snapshot
        var symbol = snapshot.Get(new Symbol()).getValue();
        var _bids = new List<BookItem>();
        var _asks = new List<BookItem>();

        // Iterate through the repeating groups for market data entries
        var noMDEntries = snapshot.GetInt(Tags.NoMDEntries);
        for (var i = 1; i <= noMDEntries; i++)
        {
            var group = snapshot.GetGroup(i, Tags.NoMDEntries);
            var entryId = group.GetDecimal(Tags.MDEntryID);
            var price = group.GetDecimal(Tags.MDEntryPx);
            var size = group.GetDecimal(Tags.MDEntrySize);
            var type = group.GetChar(Tags.MDEntryType);
            if (decimalPlaces == null)
            {
                var priceString = group.GetString(Tags.MDEntryPx);
                if (priceString.IndexOf(".") > 0)
                    decimalPlaces = priceString.Split('.')[1].Length;
            }

            var bookItem = new BookItem
            {
                Price = price.ToDouble(),
                Size = size.ToDouble(),
                IsBid = type == '0',
                EntryID = entryId.ToString(),
                LocalTimeStamp = DateTime.Now,
                ServerTimeStamp = DateTime.Now,
                DecimalPlaces = decimalPlaces.Value,
                ProviderID = 12, //FXCM
                Symbol = symbol
            };

            switch (type)
            {
                case '0': // Bid
                    _bids.Add(bookItem);
                    break;
                case '1': // Ask
                    _asks.Add(bookItem);
                    break;
            }
        }

        var model = new OrderBook();
        model.LoadData(_asks, _bids);
        model.Symbol = symbol;
        model.DecimalPlaces = decimalPlaces.Value;
        model.SymbolMultiplier = Math.Pow(10, decimalPlaces.Value);
        model.ProviderID = 12; //FXCM
        model.ProviderName = "FXCM";

        // Raise an event or further process the data as needed
        OnDataReceived?.Invoke(this,
            new DataEventArgs { DataType = "Market", ParsedModel = model, RawData = snapshot.ToString() });
    }

    private void HandleHeartBeat()
    {
        var provider = new Provider
        {
            LastUpdated = DateTime.Now, ProviderCode = 12, ProviderID = 12, ProviderName = "FXCM",
            Status = eSESSIONSTATUS.BOTH_CONNECTED
        };
        var model = new List<Provider> { provider };
        // Raise an event or further process the data as needed
        OnDataReceived?.Invoke(this, new DataEventArgs { DataType = "HeartBeats", ParsedModel = model, RawData = "" });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) _initiator.Dispose();
            _disposed = true;
        }
    }
}