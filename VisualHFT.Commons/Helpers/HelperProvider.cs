using System.Collections.Concurrent;
using System.Reflection;
using System.Timers;
using log4net;
using VisualHFT.Model;
using Timer = System.Timers.Timer;

namespace VisualHFT.Helpers;

public class HelperProvider : ConcurrentDictionary<int, Provider>, IDisposable
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private readonly Timer _timer_check_heartbeat;
    private readonly int _MILLISECONDS_HEART_BEAT = 5000;

    public HelperProvider()
    {
        _timer_check_heartbeat = new Timer(_MILLISECONDS_HEART_BEAT);
        _timer_check_heartbeat.Elapsed += _timer_check_heartbeat_Elapsed;
        _timer_check_heartbeat.Start();
    }

    public static HelperProvider Instance { get; } = new();

    public void Dispose()
    {
        _timer_check_heartbeat?.Stop();
        _timer_check_heartbeat?.Dispose();
    }


    public event EventHandler<Provider> OnDataReceived;
    public event EventHandler<Provider> OnHeartBeatFail;

    private void _timer_check_heartbeat_Elapsed(object sender, ElapsedEventArgs e)
    {
        foreach (var x in this)
            if (DateTime.Now.Subtract(x.Value.LastUpdated).TotalMilliseconds > _MILLISECONDS_HEART_BEAT)
            {
                x.Value.Status = eSESSIONSTATUS.BOTH_DISCONNECTED;
                OnHeartBeatFail?.Invoke(this, x.Value);
            }
    }

    public List<Provider> ToList()
    {
        return Values.ToList();
    }

    protected virtual void RaiseOnDataReceived(Provider provider)
    {
        var _handler = OnDataReceived;
        if (_handler != null) _handler(this, provider);
    }

    public void HeartbeatFailed(Provider provider)
    {
        this[provider.ProviderID].Status = provider.Status;
        OnHeartBeatFail?.Invoke(this, provider);
    }

    public void UpdateData(IEnumerable<Provider> providers)
    {
        foreach (var provider in providers)
            if (UpdateData(provider))
                RaiseOnDataReceived(provider); //Raise all provs allways
    }

    private bool UpdateData(Provider provider)
    {
        if (provider != null)
        {
            //Check provider
            if (!ContainsKey(provider.ProviderCode))
            {
                provider.LastUpdated = DateTime.Now;
                return TryAdd(provider.ProviderCode, provider);
            }

            this[provider.ProviderCode].LastUpdated = DateTime.Now;
            this[provider.ProviderCode].Status = provider.Status;
            this[provider.ProviderCode].Plugin = provider.Plugin;
            if (provider.Status == eSESSIONSTATUS.BOTH_DISCONNECTED ||
                provider.Status == eSESSIONSTATUS.PRICE_DSICONNECTED_ORDER_CONNECTED)
                OnHeartBeatFail?.Invoke(this, provider);

            return true;
        }

        return false;
    }
}