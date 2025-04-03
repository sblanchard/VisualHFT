using log4net;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Principal;

namespace VisualHFT.Commons.Helpers
{
    public class HelperCustomQueue<T> : IDisposable
    {
        private const int ALERT_WHEN_QUEUE_SIZE = 1000;
        private BlockingCollection<T> _queue;
        private ManualResetEventSlim _resetEvent;
        private CancellationTokenSource _ctx;
        private bool _disposed = false;
        private Task _taskConsumer;
        private Action<T> _actionOnRead;
        private Action<Exception> _actionOnError;
        private readonly object _lock = new object();
        private bool _isConsumerPaused;
        private DateTime _lastUpdateLog = DateTime.MinValue;

        private readonly HelperPerformanceCounter _performanceCounter;
        private readonly string _queueName;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public HelperCustomQueue(string queueName, Action<T> actionOnRead, Action<Exception> actionOnError = null)
        {
            _queue = new BlockingCollection<T>();
            _ctx = new CancellationTokenSource();
            _resetEvent = new ManualResetEventSlim(false);
            _actionOnRead = actionOnRead;
            _actionOnError = actionOnError;
            _queueName = queueName;


            //TODO: this is disabled until we can improve its execution and usage
            /*
            if (IsUserAdministrator())
            {
                _performanceCounter = new HelperPerformanceCounter("QUEUE", queueName);
            }*/


            _taskConsumer = Task.Run(RunConsumer, _ctx.Token);
        }
        private bool IsUserAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        public void Add(T item)
        {
            bool informOverUtilization = false;
            lock (_lock)
            {
                if (_ctx.IsCancellationRequested || _queue.IsAddingCompleted)
                    return;
                _queue.Add(item, _ctx.Token);
                _resetEvent.Set();

                informOverUtilization = _queue.Count > ALERT_WHEN_QUEUE_SIZE;
                _performanceCounter?.QueueItemAdded();
            }
            if (informOverUtilization)
            {
                InformOverUtilization();
            }
        }

        private async Task RunConsumer()
        {
            try
            {
                while (!_ctx.Token.IsCancellationRequested)
                {
                    _resetEvent.Wait(_ctx.Token); // Wait for signal or cancellation
                    if (_isConsumerPaused)
                    {
                        await Task.Delay(300);
                        continue;
                    }
                    while (!_isConsumerPaused && _queue.TryTake(out var item, 0, _ctx.Token))
                    {
                        var stopwatch = Stopwatch.StartNew();
                        _actionOnRead(item);
                        stopwatch.Stop();

                        _performanceCounter?.QueueItemRemoved();
                        _performanceCounter?.UpdateLatency(stopwatch.ElapsedTicks);
                    }
                    _resetEvent.Reset();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected exception when cancellation is requested
            }
            catch (Exception ex)
            {
                _ctx.Cancel(); // Cancel on any other exception
                _actionOnError?.Invoke(ex);
            }
        }

        public int Count()
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                ClearAndResetTask();
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                ClearAndResetTask(false);
            }
        }
        public void PauseConsumer()
        {
            _resetEvent.Reset();
            _isConsumerPaused = true;
        }
        public void ResumeConsumer()
        {
            _resetEvent.Set();
            _isConsumerPaused = false;
        }
        private void ClearAndResetTask(bool restart = true)
        {
            _queue.CompleteAdding();
            // Dispose all remaining disposable items in queue
            foreach (var item in _queue.GetConsumingEnumerable().OfType<IDisposable>())
            {
                item.Dispose();
            }

            _ctx.Cancel(); // Signal cancellation to the consumer task
            _resetEvent.Set(); // Release the consumer task from waiting 

            try
            {
                // Wait for the task to complete within the timeout period.
                _taskConsumer?.Wait(TimeSpan.FromSeconds(3));
            }
            catch (AggregateException ex)
            {
                foreach (var innerException in ex.InnerExceptions)
                {
                    _actionOnError?.Invoke(innerException);
                }
            }
            finally
            {
                // Only dispose if the task has reached a final state.
                if (_taskConsumer != null && _taskConsumer.IsCompleted)
                {
                    _taskConsumer.Dispose();
                }
                // Dispose of old resources
                _ctx.Dispose();
                if (restart)
                {
                    _ctx = new CancellationTokenSource();
                    _resetEvent.Reset(); // Ensure it's reset if cancellation was interrupted
                    _queue = new BlockingCollection<T>();
                    _taskConsumer = Task.Run(RunConsumer, _ctx.Token);
                }
            }
        }


        private void InformOverUtilization()
        {
            if (DateTime.Now.Subtract(_lastUpdateLog).TotalSeconds > 5)
            {
                var typeName = typeof(T).Name;
                var stackTrace = new StackTrace();

                var callingMethod = stackTrace.GetFrame(2)?.GetMethod();
                var callingClass = callingMethod?.ReflectedType?.Namespace + "." + callingMethod?.ReflectedType?.Name;
                /*var caller = callingMethod.ReflectedType != null
                    ? callingMethod.ReflectedType.Name
                    : "Unknown" + "." + callingMethod.Name;*/
                log.Warn($"HelperCustomQueue<{typeName}> -> {callingClass}::{callingMethod?.ToString()} - utilization: {_queue.Count}/{ALERT_WHEN_QUEUE_SIZE}");
                _lastUpdateLog = DateTime.Now;
            }
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    ClearAndResetTask(false);
                    _queue?.Dispose();
                    _ctx?.Dispose();
                    _performanceCounter?.Dispose();
                }
                _disposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
