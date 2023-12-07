using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VisualHFT.Model;

public partial class STRATEGY_PARAMETERS_BBOOK
{
    public void CopyTo(StrategyParametersBBookVM item)
    {
        item.PositionSize = PositionSize;
        item.PipsArb = PipsArb;
        item.MillisecondsToWaitBeofreClosing = MillisecondsToWaitBeofreClosing;
        item.PNLoverallPositionToClose = PNLoverallPositionToClose;
        item.ClosingWaitingBBook = ClosingWaitingBBook;
        item.ClosingWaitingTime = ClosingWaitingTime;
        item.AfterCloseWaitForMillisec = AfterCloseWaitForMillisec;

        item.PipsHedgeStopLoss = PipsHedgeStopLoss;
        item.PipsHedgeTakeProf = PipsHedgeTakeProf;
        item.PipsHedgeTrailing = PipsHedgeTrailing;
    }
}

public class StrategyParametersBBookVM : STRATEGY_PARAMETERS_BBOOK, INotifyPropertyChanged, IStrategyParameters
{
    private int _DecimalPlaces;


    private bool _IsStrategyOn;

    public StrategyParametersBBookVM()
    {
    }

    public StrategyParametersBBookVM(STRATEGY_PARAMETERS_BBOOK item)
    {
        Symbol = item.Symbol;
        LayerName = item.LayerName;
        PositionSize = item.PositionSize;
        PipsArb = item.PipsArb;
        MillisecondsToWaitBeofreClosing = item.MillisecondsToWaitBeofreClosing;
        PNLoverallPositionToClose = item.PNLoverallPositionToClose;
        ClosingWaitingBBook = item.ClosingWaitingBBook;
        ClosingWaitingTime = item.ClosingWaitingTime;
        AfterCloseWaitForMillisec = item.AfterCloseWaitForMillisec;

        PipsHedgeStopLoss = item.PipsHedgeStopLoss;
        PipsHedgeTakeProf = item.PipsHedgeTakeProf;
        PipsHedgeTrailing = item.PipsHedgeTrailing;
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

    public new decimal PipsArb
    {
        get => base.PipsArb;
        set
        {
            if (base.PipsArb != value)
            {
                base.PipsArb = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal MillisecondsToWaitBeofreClosing
    {
        get => base.MillisecondsToWaitBeofreClosing;
        set
        {
            if (base.MillisecondsToWaitBeofreClosing != value)
            {
                base.MillisecondsToWaitBeofreClosing = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal PNLoverallPositionToClose
    {
        get => base.PNLoverallPositionToClose;
        set
        {
            if (base.PNLoverallPositionToClose != value)
            {
                base.PNLoverallPositionToClose = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new bool ClosingWaitingBBook
    {
        get => base.ClosingWaitingBBook;
        set
        {
            if (base.ClosingWaitingBBook != value)
            {
                base.ClosingWaitingBBook = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new bool ClosingWaitingTime
    {
        get => base.ClosingWaitingTime;
        set
        {
            if (base.ClosingWaitingTime != value)
            {
                base.ClosingWaitingTime = value;
                NotifyPropertyChanged();
            }
        }
    }

    public new decimal AfterCloseWaitForMillisec
    {
        get => base.AfterCloseWaitForMillisec;
        set
        {
            if (base.AfterCloseWaitForMillisec != value)
            {
                base.AfterCloseWaitForMillisec = value;
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

    public STRATEGY_PARAMETERS_BBOOK ThisToDBObject()
    {
        var item = new STRATEGY_PARAMETERS_BBOOK();
        item.Symbol = Symbol;
        item.LayerName = LayerName;
        item.PositionSize = PositionSize;
        item.PipsArb = PipsArb;
        item.MillisecondsToWaitBeofreClosing = MillisecondsToWaitBeofreClosing;
        item.PNLoverallPositionToClose = PNLoverallPositionToClose;
        item.ClosingWaitingBBook = ClosingWaitingBBook;
        item.ClosingWaitingTime = ClosingWaitingTime;
        item.AfterCloseWaitForMillisec = AfterCloseWaitForMillisec;

        item.PipsHedgeStopLoss = PipsHedgeStopLoss;
        item.PipsHedgeTakeProf = PipsHedgeTakeProf;
        item.PipsHedgeTrailing = PipsHedgeTrailing;
        return item;
    }
}