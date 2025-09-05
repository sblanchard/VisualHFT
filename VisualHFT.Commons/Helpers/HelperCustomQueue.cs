using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

public class HelperCustomQueue<T> : IDisposable
{
    private readonly BlockingCollection<T> _queue;
    private readonly ManualResetEventSlim _resetEvent;
    private readonly string _queueName;
    private readonly Action<T> _actionOnRead;
    private readonly Action<Exception> _onError;
    private readonly CancellationTokenSource _cts;
    private readonly CancellationToken _token;
    private CancellationTokenRegistration _tokenRegistration;
    private Task _taskConsumer;
    private bool _isPaused;
    private bool _isRunning;
    private bool _disposed;
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    #region Ultra-Lean Performance Monitoring

    private readonly bool _monitorHealth = false; // Set to true to enable performance monitoring
    // Atomic counters - zero allocation, minimal overhead
    private long _totalMessagesAdded = 0;
    private long _totalMessagesProcessed = 0;
    private long _totalProcessingTimeTicks = 0;

    // Sliding window for rate calculation (lock-free)
    private long _lastReportTicks = DateTime.UtcNow.Ticks;
    private long _lastReportAdded = 0;
    private long _lastReportProcessed = 0;
    private static readonly long REPORT_INTERVAL_TICKS = TimeSpan.FromSeconds(5).Ticks; // Report every 5 seconds


    // Public read-only properties for monitoring
    public long TotalMessagesAdded => _totalMessagesAdded;
    public long TotalMessagesProcessed => _totalMessagesProcessed;
    public int CurrentQueueDepth => _queue?.Count ?? 0;
    public double AverageProcessingTimeMicroseconds
    {
        get
        {
            var processed = _totalMessagesProcessed;
            return processed > 0 ? (_totalProcessingTimeTicks / (double)processed) / 10.0 : 0; // Convert ticks to microseconds
        }
    }

    #endregion

    public HelperCustomQueue(string queueName, Action<T> actionOnRead, Action<Exception> onError = null, bool monitorHealth = false)
    {
        _queueName = queueName;
        _actionOnRead = actionOnRead;
        _onError = onError;
        _queue = new BlockingCollection<T>();
        _resetEvent = new ManualResetEventSlim(false);
        _cts = new CancellationTokenSource();
        _token = _cts.Token;
        _monitorHealth = monitorHealth;
        // Register once — avoids per-Wait() allocation
        _tokenRegistration = _token.Register(static s =>
        {
            var evt = (ManualResetEventSlim)s!;
            evt.Set(); // Wake up waiters when canceled
        }, _resetEvent);

        Start();
    }

    public void Add(T item)
    {
        if (_disposed || _queue.IsAddingCompleted)
            return;
        if (item == null)
            return;
        _queue.Add(item);
        if (_monitorHealth)
            Interlocked.Increment(ref _totalMessagesAdded); // Ultra-fast atomic increment
        _resetEvent.Set();

        // Periodic reporting (minimal overhead check)
        if (_monitorHealth)
            CheckAndReportPerformance();
    }

    public void PauseConsumer() => _isPaused = true;

    public void ResumeConsumer()
    {
        if (_disposed)
            return;

        _isPaused = false;
        _resetEvent.Set();
    }

    public void Stop()
    {
        if (_disposed)
            return;

        _isRunning = false;

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource already disposed, ignore
        }

        _queue.CompleteAdding();
        _resetEvent.Set();

        try
        {
            _taskConsumer?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignore
        }
    }

    public void Clear()
    {
        if (_disposed)
            return;

        while (_queue.TryTake(out _)) { }
    }

    private void Start()
    {
        _isRunning = true;
        _taskConsumer = Task.Run(RunConsumer);
    }

    private void RunConsumer()
    {
        var sw = new Stopwatch();

        try
        {
            while (_isRunning && !_disposed)
            {
                _resetEvent.Wait(); // no token — no per-wait allocation
                _resetEvent.Reset();

                if (_isPaused || _disposed)
                    continue;

                while (_queue.TryTake(out var item))
                {
                    if (_disposed)
                        break;

                    try
                    {
                        sw.Restart();
                        _actionOnRead(item);
                        sw.Stop();

                        // Ultra-fast performance tracking
                        if (_monitorHealth)
                        {
                            Interlocked.Increment(ref _totalMessagesProcessed);
                            Interlocked.Add(ref _totalProcessingTimeTicks, sw.ElapsedTicks);
                        }
                    }
                    catch (Exception ex)
                    {
                        _onError?.Invoke(ex);
                        // Continue processing other items even if one fails
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // This should only catch exceptions from the consumer loop itself, not from item processing
            _onError?.Invoke(ex);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckAndReportPerformance()
    {
        var currentTicks = DateTime.UtcNow.Ticks;
        var lastReport = _lastReportTicks;

        // Only check every 5 seconds to minimize overhead
        if (currentTicks - lastReport > REPORT_INTERVAL_TICKS)
        {
            // Try to atomically update the last report time
            if (Interlocked.CompareExchange(ref _lastReportTicks, currentTicks, lastReport) == lastReport)
            {
                ReportPerformanceMetrics(currentTicks, lastReport);
            }
        }
    }
    private void ReportPerformanceMetrics(long currentTicks, long lastReportTicks)
    {
        var currentAdded = _totalMessagesAdded;
        var currentProcessed = _totalMessagesProcessed;
        var intervalSeconds = (currentTicks - lastReportTicks) / (double)TimeSpan.TicksPerSecond;

        var addedInInterval = currentAdded - _lastReportAdded;
        var processedInInterval = currentProcessed - _lastReportProcessed;

        var addRate = addedInInterval / intervalSeconds;
        var processRate = processedInInterval / intervalSeconds;
        var queueDepth = CurrentQueueDepth;
        var avgProcessingTime = AverageProcessingTimeMicroseconds;

        // Ultra-compact performance report
        log.WarnFormat("[{0}] Add: {1:F0}/s | Process: {2:F0}/s | Queue: {3:N0} | Avg: {4:F1}μs", _queueName, addRate, processRate, queueDepth, avgProcessingTime);

        // Update for next interval
        _lastReportAdded = currentAdded;
        _lastReportProcessed = currentProcessed;
    }
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
        _tokenRegistration.Dispose();
        _resetEvent.Dispose();
        _queue.Dispose();
        _cts.Dispose();
    }
}
