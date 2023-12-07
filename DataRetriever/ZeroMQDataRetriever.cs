using System;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using VisualHFT.Model;
using Provider = VisualHFT.ViewModel.Model.Provider;

namespace VisualHFT.DataRetriever;

public class ZeroMQDataRetriever : IDataRetriever
{
    private readonly string _connectionString;
    private bool _disposed; // to track whether the object has been disposed
    private SubscriberSocket _subscriber;

    public ZeroMQDataRetriever(string connectionString)
    {
        _connectionString = connectionString;
    }

    public event EventHandler<DataEventArgs> OnDataReceived;

    public async Task StartAsync()
    {
        _subscriber = new SubscriberSocket();
        _subscriber.Connect(_connectionString);
        _subscriber.Subscribe(""); // Subscribe to all messages

        // Start listening for messages in a separate thread
        _subscriber.ReceiveReady += (s, e) =>
        {
            var message = e.Socket.ReceiveFrameString();
            HandleMessage(message);
        };

        // You can use NetMQ's built-in Poller to continuously listen for messages
        using (var poller = new NetMQPoller { _subscriber })
        {
            poller.Run();
        }
    }

    public async Task StopAsync()
    {
        _subscriber.Close();
        _subscriber.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~ZeroMQDataRetriever()
    {
        Dispose(false);
    }

    private void HandleMessage(string message)
    {
        // Process the received message
        var model = new OrderBook();
        // parse message and populate 'model'


        // Raise the OnDataReceived event
        OnDataReceived?.Invoke(this, new DataEventArgs { DataType = "Market", RawData = message, ParsedModel = model });


        var provider = new Provider
        {
            LastUpdated = DateTime.Now, ProviderID = 1, ProviderName = "ZeroMQ", Status = eSESSIONSTATUS.BOTH_CONNECTED
        };
        // Raise the OnDataReceived event
        OnDataReceived?.Invoke(this,
            new DataEventArgs { DataType = "HeartBeats", RawData = message, ParsedModel = model });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) _subscriber.Dispose();
            _disposed = true;
        }
    }
}