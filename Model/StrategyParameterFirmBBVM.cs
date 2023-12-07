using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VisualHFT.Model;

public partial class
    STRATEGY_PARAMETERS_FIRMBB
{
    public void CopyTo(StrategyParametersFirmBBVM item)
    {
        item.PositionSize = PositionSize;
        item.MaximumExposure = MaximumExposure;
        item.LookUpBookForSize = LookUpBookForSize;
        item.PipsMarkupAsk = PipsMarkupAsk;
        item.PipsMarkupBid = PipsMarkupBid;
        item.MinPipsDiffToUpdatePrice = MinPipsDiffToUpdatePrice;
        item.MinSpread = MinSpread;
        item.PipsSlippage = PipsSlippage;
        item.AggressingToHedge = AggressingToHedge;
        item.PipsSlippageToHedge = PipsSlippageToHedge;
        item.PipsHedgeStopLoss = PipsHedgeStopLoss;
        item.PipsHedgeTakeProf = PipsHedgeTakeProf;
        item.PipsHedgeTrailing = PipsHedgeTrailing;
        item.TickSample = TickSample;
        item.BollingerPeriod = BollingerPeriod;
        item.BollingerStdDev = BollingerStdDev;
    }
}

public class StrategyParametersFirmBBVM : STRATEGY_PARAMETERS_FIRMBB, INotifyPropertyChanged, IStrategyParameters
{
    private int _DecimalPlaces;
    private int _HedgeCancelRejectedQty;
    private int _HedgeFilledQty;


    private bool _IsStrategyOn;
    private double _SentAskPrice;
    private int _SentAskSize;
    private double _SentBidPrice;
    private int _SentBidSize;

    public StrategyParametersFirmBBVM()
    {
    }

    public StrategyParametersFirmBBVM(STRATEGY_PARAMETERS_FIRMBB item)
    {
        Symbol = item.Symbol;
        LayerName = item.LayerName;
        PositionSize = item.PositionSize;
        MaximumExposure = item.MaximumExposure;
        LookUpBookForSize = item.LookUpBookForSize;
        PipsMarkupAsk = item.PipsMarkupAsk;
        PipsMarkupBid = item.PipsMarkupBid;
        MinPipsDiffToUpdatePrice = item.MinPipsDiffToUpdatePrice;
        MinSpread = item.MinSpread;
        PipsSlippage = item.PipsSlippage;
        AggressingToHedge = item.AggressingToHedge;
        PipsSlippageToHedge = item.PipsSlippageToHedge;
        PipsHedgeStopLoss = item.PipsHedgeStopLoss;
        PipsHedgeTakeProf = item.PipsHedgeTakeProf;
        PipsHedgeTrailing = item.PipsHedgeTrailing;
        TickSample = item.TickSample;
        BollingerPeriod = item.BollingerPeriod;
        BollingerStdDev = item.BollingerStdDev;
    }

    public new decimal PositionSize
    {
        get => base.PositionSize;
        set
        {
            if (base.PositionSize != value)
            {
                base.PositionSize = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal MaximumExposure
    {
        get => base.MaximumExposure;
        set
        {
            if (base.MaximumExposure != value)
            {
                base.MaximumExposure = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal LookUpBookForSize
    {
        get => base.LookUpBookForSize;
        set
        {
            if (base.LookUpBookForSize != value)
            {
                base.LookUpBookForSize = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal PipsMarkupAsk
    {
        get => base.PipsMarkupAsk;
        set
        {
            if (base.PipsMarkupAsk != value)
            {
                base.PipsMarkupAsk = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal PipsMarkupBid
    {
        get => base.PipsMarkupBid;
        set
        {
            if (base.PipsMarkupBid != value)
            {
                base.PipsMarkupBid = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal MinPipsDiffToUpdatePrice
    {
        get => base.MinPipsDiffToUpdatePrice;
        set
        {
            if (base.MinPipsDiffToUpdatePrice != value)
            {
                base.MinPipsDiffToUpdatePrice = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal MinSpread
    {
        get => base.MinSpread;
        set
        {
            if (base.MinSpread != value)
            {
                base.MinSpread = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal PipsSlippage
    {
        get => base.PipsSlippage;
        set
        {
            if (base.PipsSlippage != value)
            {
                base.PipsSlippage = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new bool AggressingToHedge
    {
        get => base.AggressingToHedge;
        set
        {
            if (base.AggressingToHedge != value)
            {
                base.AggressingToHedge = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal PipsSlippageToHedge
    {
        get => base.PipsSlippageToHedge;
        set
        {
            if (base.PipsSlippageToHedge != value)
            {
                base.PipsSlippageToHedge = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal PipsHedgeStopLoss
    {
        get => base.PipsHedgeStopLoss;
        set
        {
            if (base.PipsHedgeStopLoss != value)
            {
                base.PipsHedgeStopLoss = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal PipsHedgeTakeProf
    {
        get => base.PipsHedgeTakeProf;
        set
        {
            if (base.PipsHedgeTakeProf != value)
            {
                base.PipsHedgeTakeProf = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal PipsHedgeTrailing
    {
        get => base.PipsHedgeTrailing;
        set
        {
            if (base.PipsHedgeTrailing != value)
            {
                base.PipsHedgeTrailing = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new int TickSample
    {
        get => base.TickSample;
        set
        {
            if (base.TickSample != value)
            {
                base.TickSample = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new int BollingerPeriod
    {
        get => base.BollingerPeriod;
        set
        {
            if (base.BollingerPeriod != value)
            {
                base.BollingerPeriod = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal BollingerStdDev
    {
        get => base.BollingerStdDev;
        set
        {
            if (base.BollingerStdDev != value)
            {
                base.BollingerStdDev = value;
                NotifyPropertyChanged();
            }
        }
    }

    public int DecimalPlaces
    {
        get => _DecimalPlaces;
        set
        {
            if (_DecimalPlaces != value)
            {
                _DecimalPlaces = value;
                NotifyPropertyChanged();
            }
        }
    }

    public double SentBidPrice
    {
        get => _SentBidPrice;
        set
        {
            if (_SentBidPrice != value)
            {
                _SentBidPrice = value;
                NotifyPropertyChanged();
            }
        }
    }

    public double SentAskPrice
    {
        get => _SentAskPrice;
        set
        {
            if (_SentAskPrice != value)
            {
                _SentAskPrice = value;
                NotifyPropertyChanged();
            }
        }
    }

    public int SentBidSize
    {
        get => _SentBidSize;
        set
        {
            if (_SentBidSize != value)
            {
                _SentBidSize = value;
                NotifyPropertyChanged();
            }
        }
    }

    public int SentAskSize
    {
        get => _SentAskSize;
        set
        {
            if (_SentAskSize != value)
            {
                _SentAskSize = value;
                NotifyPropertyChanged();
            }
        }
    }

    public int HedgeCancelRejectedQty
    {
        get => _HedgeCancelRejectedQty;
        set
        {
            if (_HedgeCancelRejectedQty != value)
            {
                _HedgeCancelRejectedQty = value;
                NotifyPropertyChanged();
            }
        }
    }

    public int HedgeFilledQty
    {
        get => _HedgeFilledQty;
        set
        {
            if (_HedgeFilledQty != value)
            {
                _HedgeFilledQty = value;
                NotifyPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public new string Symbol
    {
        get => base.Symbol;
        set
        {
            if (base.Symbol != value)
            {
                base.Symbol = value;
                NotifyPropertyChanged();
            }
        }
    }


    //INFORMATION
    public bool IsStrategyOn
    {
        get => _IsStrategyOn;
        set
        {
            if (_IsStrategyOn != value)
            {
                _IsStrategyOn = value;
                NotifyPropertyChanged();
            }
        }
    }

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }

    public STRATEGY_PARAMETERS_FIRMBB ThisToDBObject()
    {
        var item = new STRATEGY_PARAMETERS_FIRMBB();
        item.Symbol = Symbol;
        item.LayerName = LayerName;
        item.PositionSize = PositionSize;
        item.MaximumExposure = MaximumExposure;
        item.LookUpBookForSize = LookUpBookForSize;
        item.PipsMarkupAsk = PipsMarkupAsk;
        item.PipsMarkupBid = PipsMarkupBid;
        item.MinPipsDiffToUpdatePrice = MinPipsDiffToUpdatePrice;
        item.MinSpread = MinSpread;
        item.PipsSlippage = PipsSlippage;
        item.AggressingToHedge = AggressingToHedge;
        item.PipsSlippageToHedge = PipsSlippageToHedge;
        item.PipsHedgeStopLoss = PipsHedgeStopLoss;
        item.PipsHedgeTakeProf = PipsHedgeTakeProf;
        item.PipsHedgeTrailing = PipsHedgeTrailing;
        item.TickSample = TickSample;
        item.BollingerPeriod = BollingerPeriod;
        item.BollingerStdDev = BollingerStdDev;
        return item;
    }
}