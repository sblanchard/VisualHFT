using System.Collections.Concurrent;
using System.Reflection;
using System.Timers;
using log4net;
using VisualHFT.Commons.Pools;
using VisualHFT.Commons.SubscriberBuffers;
using VisualHFT.Model;
using Timer = System.Timers.Timer;

namespace VisualHFT.Helpers;

public sealed class HelperOrderBook : IOrderBookHelper
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly object _lockObj = new();


    // This timer will be used for performance monitoring
    private readonly Timer _monitoringTimer;
    private readonly Task _processingTask;

    private readonly ObjectPool<OrderBook> orerBookPool = new(); //pool of Trade objects
    protected readonly BlockingCollection<OrderBook> _DataQueue = new(new ConcurrentQueue<OrderBook>());
    private readonly List<OrderBookSubscriberBuffer> _subscribers = new();


    private HelperOrderBook()
    {
        _processingTask = Task.Run(async () => await ProcessQueueAsync(), _cancellationTokenSource.Token);

        // Set up the performance monitoring timer
        _monitoringTimer = new Timer(5000); // Check every 5 seconds
        _monitoringTimer.Elapsed += MonitorSubscriberBuffers;
        _monitoringTimer.Start();
    }

    public static HelperOrderBook Instance { get; } = new();


    public void Subscribe(Action<OrderBook> processor)
    {
        lock (_lockObj)
        {
            _subscribers.Add(new OrderBookSubscriberBuffer(processor));
        }
    }

    public void UpdateData(IEnumerable<OrderBook> data)
    {
        foreach (var e in data) _DataQueue.Add(e);
    }

    ~HelperOrderBook()
    {
        _cancellationTokenSource.Cancel();
        _processingTask.Wait();
    }

    public void Unsubscribe(Action<OrderBook> processor)
    {
        lock (_lockObj)
        {
            var bufferToRemove = _subscribers.FirstOrDefault(buffer => buffer.Processor == processor);
            if (bufferToRemove != null)
            {
                _subscribers.Remove(bufferToRemove);
                bufferToRemove.Buffer.CompleteAdding();
            }
        }
    }


    private void MonitorSubscriberBuffers(object? sender, ElapsedEventArgs e)
    {
        foreach (var subscriber in _subscribers)
            if (subscriber.Count > 500) // or some threshold value
                log.Warn($"OrderBook Subscriber buffer is growing large: {subscriber.Count}");
        // Additional actions as needed: Pause, Alert, Disconnect
    }

    private async Task ProcessQueueAsync()
    {
        Thread.CurrentThread.IsBackground = true;

        var data = new List<OrderBook>();

        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (_DataQueue.Count > 500) log.Warn($"HelperOrderBook QUEUE is way behind: {_DataQueue.Count}");

                var ob = _DataQueue.Take();
                DispatchToSubscribers(ob);


                // Wait for the next iteration
                await Task.Delay(0);
            }
        }
        catch (Exception ex)
        {
            log.Fatal(ex);
        }
    }

    private void DispatchToSubscribers(OrderBook book)
    {
        foreach (var subscriber in _subscribers) subscriber.Add(book);
    }
}