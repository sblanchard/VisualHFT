using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using VisualHFT.Model;
using Timer = System.Timers.Timer;

namespace VisualHFT.Helpers;

public enum ePOSITION_LOADING_TYPE
{
    WEBSOCKETS,
    DATABASE
}

public class HelperPosition : IDisposable
{
    private const int POLLING_INTERVAL = 5000; // Interval for polling the database
    private readonly CancellationTokenSource _cancellationTokenSource = new(); // Added cancellation token source
    private readonly HFTEntities _DB;
    private readonly object _lock = new();
    private readonly Timer _timer;
    private long? _LAST_POSITION_ID;
    private DateTime? _sessionDate;


    public HelperPosition(ePOSITION_LOADING_TYPE loadingType)
    {
        Positions = new List<Position>();
        LoadingType = loadingType;
        if (loadingType == ePOSITION_LOADING_TYPE.DATABASE)
        {
            _timer = new Timer(POLLING_INTERVAL);
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();
            _timer_Elapsed(null, null);

            _DB = new HFTEntities();
            _DB.Database.CommandTimeout = 6000;
            _DB.Configuration.ValidateOnSaveEnabled = false;
            _DB.Configuration.AutoDetectChangesEnabled = false;
            _DB.Configuration.LazyLoadingEnabled = false;
        }
    }

    public List<Position> Positions { get; private set; }

    public ePOSITION_LOADING_TYPE LoadingType { get; set; }

    public DateTime? SessionDate
    {
        get => _sessionDate;
        set
        {
            if (value != _sessionDate)
            {
                _sessionDate = value;
                Positions.Clear();
                RaiseOnInitialLoad(Positions);
                _LAST_POSITION_ID = null;
                _timer_Elapsed(null, null);
            }
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _cancellationTokenSource?.Cancel(); // Cancel any ongoing operations
        _cancellationTokenSource?.Dispose();
        _DB.Dispose();
    }

    public event EventHandler<IEnumerable<Position>> OnInitialLoad;
    public event EventHandler<IEnumerable<Position>> OnDataReceived;

    protected virtual void RaiseOnInitialLoad(IEnumerable<Position> pos)
    {
        OnInitialLoad?.Invoke(this, pos);
    }

    protected virtual void RaiseOnDataReceived(IEnumerable<Position> pos)
    {
        OnDataReceived?.Invoke(this, pos);
    }

    private async void _timer_Elapsed(object sender, ElapsedEventArgs e)
    {
        _timer.Stop(); // Stop the timer while the operation is running
        if (_cancellationTokenSource.IsCancellationRequested) return; // Check for cancellation
        var res = await GetPositionsAsync();
        if (res != null && res.Any())
        {
            foreach (var p in res)
            {
                if (!p.PipsPnLInCurrency.HasValue || p.PipsPnLInCurrency == 0)
                {
                    p.PipsPnLInCurrency =
                        p.GetCloseQuantity * p.GetCloseAvgPrice - p.GetOpenQuantity * p.GetOpenAvgPrice;
                    if (p.Side == ePOSITIONSIDE.Sell) p.PipsPnLInCurrency *= -1;
                }

                HelperSymbol.Instance.UpdateData(p.Symbol);
            }

            if (Positions == null || !Positions.Any())
            {
                Positions = new List<Position>(res);
                RaiseOnInitialLoad(Positions);
            }
            else
            {
                foreach (var p in res)
                    Positions.Insert(0, p);
                RaiseOnDataReceived(res);
            }
        }

        _timer.Start(); // Restart the timer once the operation is complete
    }

    private async Task<IEnumerable<Position>> GetPositionsAsync()
    {
        if (!SessionDate.HasValue || _cancellationTokenSource.IsCancellationRequested) return null;

        if (!SessionDate.HasValue)
            return null;
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    var targetDate = SessionDate.Value.Date;
                    var simpleCheckOfNewRecords = _DB.Positions
                        .Where(x => DbFunctions.TruncateTime(x.CreationTimeStamp) == targetDate &&
                                    (!_LAST_POSITION_ID.HasValue || x.ID > _LAST_POSITION_ID.Value))
                        .ToList();

                    if (!simpleCheckOfNewRecords.Any()) return null;

                    var allProviders = _DB.Providers.ToList();
                    var result = _DB.Positions.Include("OpenExecutions").Include("CloseExecutions").Where(x =>
                        DbFunctions.TruncateTime(x.CreationTimeStamp) == targetDate &&
                        (!_LAST_POSITION_ID.HasValue || x.ID > _LAST_POSITION_ID.Value)).ToList();
                    if (result.Any())
                    {
                        _LAST_POSITION_ID = result.Max(x => x.ID);


                        //DEPRECIATED
                        /*var ret = result.Select(x => new VisualHFT.Model.Position(x)).ToList(); //convert to our model
                                                                                  //find provider's name
                        ret.ForEach(x =>
                        {
                            x.CloseProviderName = allProviders.Where(p => p.ProviderCode == x.CloseProviderId).DefaultIfEmpty(new Provider()).FirstOrDefault().ProviderName;
                            x.OpenProviderName = allProviders.Where(p => p.ProviderCode == x.OpenProviderId).DefaultIfEmpty(new Provider()).FirstOrDefault().ProviderName;

                            x.CloseExecutions.ForEach(ex => ex.ProviderName = x.CloseProviderName);
                            x.OpenExecutions.ForEach(ex => ex.ProviderName = x.OpenProviderName);
                        });
                        return ret;*/
                        return new List<Position>();
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    return null;
                }
            }
        });

        //return null;
    }

    public void LoadNewPositions(IEnumerable<Position> positions)
    {
        if (positions == null || !positions.Any() || _cancellationTokenSource.IsCancellationRequested) return;

        foreach (var p in positions)
        {
            var posToUpdate = Positions.Where(x => x.PositionID == p.PositionID).FirstOrDefault();
            if (posToUpdate == null)
            {
                /*foreach (var ex in p.AllExecutions)
                    ex.Symbol = p.Symbol;
                this.Positions.Add(p);*/
            }
        }
    }
}