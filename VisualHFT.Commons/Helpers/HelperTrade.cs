using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Timers;
using log4net;
using VisualHFT.Commons.Pools;
using VisualHFT.Commons.SubscriberBuffers;
using VisualHFT.Model;
using Timer = System.Timers.Timer;

namespace VisualHFT.Helpers;

public class HelperTrade
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly object _lockObj = new();

    // This timer will be used for performance monitoring
    private readonly Timer _monitoringTimer;
    private readonly Task _processingTask;
    private readonly ObjectPool<Trade> orderBookPool = new();

    private readonly ObjectPool<Trade> tradePool = new(); //pool of Trade objects
    protected BlockingCollection<Trade> _DataQueue = new(new ConcurrentQueue<Trade>());
    private readonly List<TradeSubscriberBuffer> _subscribers = new();


    public HelperTrade()
    {
        _processingTask = Task.Run(async () => await ProcessQueueAsync(), _cancellationTokenSource.Token);

        // Set up the performance monitoring timer
        _monitoringTimer = new Timer(5000); // Check every 5 seconds
        _monitoringTimer.Elapsed += MonitorSubscriberBuffers;
        _monitoringTimer.Start();
    }

    public static HelperTrade Instance { get; } = new();

    ~HelperTrade()
    {
        _cancellationTokenSource.Cancel();
        _processingTask.Wait();
    }

    public void Subscribe(Action<Trade> processor)
    {
        lock (_lockObj)
        {
            _subscribers.Add(new TradeSubscriberBuffer(processor));
        }
    }

    public void Unsubscribe(Action<Trade> processor)
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
                log.Warn($"Trade Subscriber buffer is growing large: {subscriber.Count}");
        // Additional actions as needed: Pause, Alert, Disconnect
    }

    private async Task ProcessQueueAsync()
    {
        Thread.CurrentThread.IsBackground = true;

        var ci_clone = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
        ci_clone.NumberFormat = CultureInfo.GetCultureInfo("en-US").NumberFormat;

        Thread.CurrentThread.CurrentCulture = ci_clone;
        Thread.CurrentThread.CurrentUICulture = ci_clone;
        var data = new List<OrderBook>();

        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (_DataQueue.Count > 500) log.Warn($"HelperTrade QUEUE is way behind: {_DataQueue.Count}");

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

    private void DispatchToSubscribers(Trade trade)
    {
        foreach (var subscriber in _subscribers) subscriber.Add(trade);
    }


    public void UpdateData(IEnumerable<Trade> trades)
    {
        foreach (var e in trades)
        {
            var pooledOrderBook = orderBookPool.Get();
            e.CopyTo(pooledOrderBook);
            _DataQueue.Add(pooledOrderBook);
        }
    }
}