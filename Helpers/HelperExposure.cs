using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using VisualHFT.Model;

namespace VisualHFT.Helpers;

public class HelperExposure : ConcurrentDictionary<string, Exposure>
{
    public event EventHandler<Exposure> OnDataReceived;

    ~HelperExposure()
    {
    }

    protected virtual void RaiseOnDataReceived(List<Exposure> exposures)
    {
        var _handler = OnDataReceived;
        if (_handler != null && Application.Current != null)
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                foreach (var exposure in exposures)
                    _handler(this, exposure);
            }));
    }

    public void UpdateData(IEnumerable<Exposure> exposures)
    {
        var toUpdate = new List<Exposure>();
        foreach (var e in exposures)
            if (UpdateData(e))
                toUpdate.Add(e);
        if (toUpdate.Any())
            RaiseOnDataReceived(toUpdate);
    }

    private bool UpdateData(Exposure exposure)
    {
        if (exposure != null)
        {
            var _key = exposure.StrategyName + exposure.Symbol;
            //Check provider
            if (!ContainsKey(_key))
            {
                return TryAdd(_key, exposure);
            }

            var hasChanged = false;
            //UPPDATE
            if (this[_key].SizeExposed != exposure.SizeExposed)
            {
                this[_key].SizeExposed = exposure.SizeExposed;
                hasChanged = true;
            }

            if (this[_key].UnrealizedPL != exposure.UnrealizedPL)
            {
                this[_key].UnrealizedPL = exposure.UnrealizedPL;
                hasChanged = true;
            }

            return hasChanged;
        }

        return false;
    }
}