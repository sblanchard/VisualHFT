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
        if (!_queue.IsAddingCompleted)
        {
            _queue.Add(item);
            _resetEvent.Set();
        }
    }

    public void PauseConsumer() => _isPaused = true;

    public void ResumeConsumer()
    {
        _isPaused = false;
        _resetEvent.Set();
    }

    public void Stop()
    {
        _isRunning = false;
        _cts.Cancel();
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
            while (_isRunning)
            {
                _resetEvent.Wait(); // no token — no per-wait allocation
                _resetEvent.Reset();

                if (_isPaused)
                    continue;

                while (_queue.TryTake(out var item))
                {
                    sw.Restart();
                    _actionOnRead(item);
                    sw.Stop();
                    // keep your performance metrics logic here...
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _onError?.Invoke(ex);
        }
    }

    public void Dispose()
    {
        Stop();
        _tokenRegistration.Dispose();
        _resetEvent.Dispose();
        _queue.Dispose();
        _cts.Dispose();
    }
}
