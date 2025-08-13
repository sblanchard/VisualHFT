using System.Collections.Concurrent;
using System.Diagnostics;

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

    public HelperCustomQueue(string queueName, Action<T> actionOnRead, Action<Exception> onError = null)
    {
        _queueName = queueName;
        _actionOnRead = actionOnRead;
        _onError = onError;
        _queue = new BlockingCollection<T>();
        _resetEvent = new ManualResetEventSlim(false);
        _cts = new CancellationTokenSource();
        _token = _cts.Token;

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
        _resetEvent.Set();
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
                        // keep your performance metrics logic here...
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
