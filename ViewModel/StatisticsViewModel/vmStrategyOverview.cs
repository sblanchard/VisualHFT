using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Prism.Mvvm;
using VisualHFT.Helpers;
using VisualHFT.Model;

namespace VisualHFT.ViewModel.StatisticsViewModel;

public class vmStrategyOverview : BindableBase
{
    private Dictionary<string, Func<string, string, bool>> _dialogs;

    public vmStrategyOverview()
    {
        Positions = new ObservableCollection<Position>();
        BlankFields();
    }

    public vmStrategyOverview(Dictionary<string, Func<string, string, bool>> dialogs)
    {
        Positions = new ObservableCollection<Position>();
        _dialogs = dialogs;
        BlankFields();
    }

    private void BlankFields()
    {
        WinnersPnL = "";
        WinnersAttempts = "";
        WinnersSpan = "";
        LosersPnL = "";
        LosersAttempts = "";
        LosersSpan = "";
        AllPnL = "";
        AllAttempts = "";
        AllSpan = "";
        PnLAmount = 0;
        WinningRate = 0;
        WinningCount = 0;
        LoserCount = 0;
        WinningRateChartPoints = new ObservableCollection<PlotInfo>
        {
            new() { Value = 0 },
            new() { Value = 0 }
        };
        EquityChartPoints = new List<ChartDateCategoryDataPoint>();
    }

    private void CalculatePositionStats()
    {
        if (Positions == null || !Positions.Any())
        {
            BlankFields();
            return;
        }

        try
        {
            var losers = Positions.Where(x => x.GetPipsPnL < 0).DefaultIfEmpty(new Position()).ToList();
            var winners = Positions.Where(x => x.GetPipsPnL >= 0).DefaultIfEmpty(new Position()).ToList();
            foreach (var item in losers)
                if (!item.PipsPnLInCurrency.HasValue)
                    item.PipsPnLInCurrency = item.GetPipsPnL;
            foreach (var item in winners)
                if (!item.PipsPnLInCurrency.HasValue)
                    item.PipsPnLInCurrency = item.GetPipsPnL;


            //ALL

            AllPnL = "Avg PnL: " + Positions.Where(x => x.PipsPnLInCurrency.HasValue)
                .DefaultIfEmpty(new Position { PipsPnLInCurrency = 0m }).Average(x => x.PipsPnLInCurrency.Value)
                .ToString("C0");
            AllAttempts = "Avg Attempts: " + Positions.Average(x => (double)x.AttemptsToClose).ToString("N1");
            AllSpan = "Avg Span: " +
                      HelperCommon.GetKiloFormatterTime(Positions.Average(x =>
                          (x.CloseTimeStamp - x.CreationTimeStamp).TotalMilliseconds));


            if (losers != null && losers.Count() > 0)
            {
                LoserCount = losers.Count();
                LosersPnL = "Avg PnL: " + losers.Where(x => x.PipsPnLInCurrency.HasValue)
                    .DefaultIfEmpty(new Position { PipsPnLInCurrency = 0m }).Average(x => x.PipsPnLInCurrency.Value)
                    .ToString("C2");
                LosersAttempts = "Avg Attempts: " + losers.Average(x => (double)x.AttemptsToClose).ToString("N1");
                LosersSpan = "Avg Span: " +
                             HelperCommon.GetKiloFormatterTime(losers.Average(x =>
                                 (x.CloseTimeStamp - x.CreationTimeStamp).TotalMilliseconds));
            }

            if (winners != null && winners.Count() > 0)
            {
                WinningCount = winners.Count();
                WinnersPnL = "Avg PnL: " + winners.Where(x => x.PipsPnLInCurrency.HasValue)
                    .DefaultIfEmpty(new Position { PipsPnLInCurrency = 0m }).Average(x => x.PipsPnLInCurrency.Value)
                    .ToString("C2");
                WinnersAttempts = "Avg Attempts: " + winners.Average(x => (double)x.AttemptsToClose).ToString("N1");
                WinnersSpan = "Avg Span: " +
                              HelperCommon.GetKiloFormatterTime(winners.Average(x =>
                                  (x.CloseTimeStamp - x.CreationTimeStamp).TotalMilliseconds));
            }

            WinningRateChartPoints[0].Value = _winningCount;
            WinningRateChartPoints[1].Value = _loserCount;
            //RaisePropertyChanged("WinningRateChartPoints");

            if (losers != null && losers.Count() > 0 && winners != null && winners.Count() > 0)
                WinningRate = winners.Count() / (double)Positions.Count;
            else if (winners != null && winners.Count() > 0 && (losers == null || losers.Count() == 0))
                WinningRate = 1;
            else if (losers != null && losers.Count() > 0 && (winners == null || winners.Count() == 0))
                WinningRate = 0;


            //GET Equity
            var max = Positions.Max(x => x.CloseTimeStamp);
            var min = Positions.Min(x => x.CreationTimeStamp);
            List<cEquity> equity = null;
            if (max.Subtract(min).TotalHours <= 1)
                equity = HelperPositionAnalysis.GetEquityCurve(Positions.ToList());
            else
                equity = HelperPositionAnalysis.GetEquityCurveByHour(Positions.ToList());
            if (equity != null && equity.Count > 0)
            {
                EquityChartPoints = equity.OrderBy(x => x.Date).Select(x => new ChartDateCategoryDataPoint
                    { Date = x.Date, Value = x.Equity.ToDouble() }).ToList();
                var iniEquity = equity.First().Equity.ToDouble();
                var endEquity = equity.Last().Equity.ToDouble();

                PnLAmount = endEquity;
                RaisePropertyChanged("EquityChartPoints");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    #region Fields

    //WINNERS
    private string _winnersPnL;
    private string _winnersAttempts;

    private string _winnersSpan;

    //LOSERS
    private string _losersPnL;
    private string _losersAttempts;

    private string _losersSpan;

    //ALL
    private string _allPnL;
    private string _allAttempts;

    private string _allSpan;

    //PNL
    private double _pnLAmount;
    private double _winningRate;
    private int _winningCount;
    private int _loserCount;

    private ObservableCollection<PlotInfo> _winningRateChartPoints;
    private List<ChartDateCategoryDataPoint> _equityChartPoints;

    public ObservableCollection<PlotInfo> WinningRateChartPoints
    {
        get => _winningRateChartPoints;
        set => SetProperty(ref _winningRateChartPoints, value);
    }

    public List<ChartDateCategoryDataPoint> EquityChartPoints
    {
        get => _equityChartPoints;
        set => SetProperty(ref _equityChartPoints, value);
    }

    public string WinnersPnL
    {
        get => _winnersPnL;
        set => SetProperty(ref _winnersPnL, value);
    }

    public string WinnersAttempts
    {
        get => _winnersAttempts;
        set => SetProperty(ref _winnersAttempts, value);
    }

    public string WinnersSpan
    {
        get => _winnersSpan;
        set => SetProperty(ref _winnersSpan, value);
    }

    public string LosersPnL
    {
        get => _losersPnL;
        set => SetProperty(ref _losersPnL, value);
    }

    public string LosersAttempts
    {
        get => _losersAttempts;
        set => SetProperty(ref _losersAttempts, value);
    }

    public string LosersSpan
    {
        get => _losersSpan;
        set => SetProperty(ref _losersSpan, value);
    }

    public string AllPnL
    {
        get => _allPnL;
        set => SetProperty(ref _allPnL, value);
    }

    public string AllAttempts
    {
        get => _allAttempts;
        set => SetProperty(ref _allAttempts, value);
    }

    public string AllSpan
    {
        get => _allSpan;
        set => SetProperty(ref _allSpan, value);
    }

    public double PnLAmount
    {
        get => _pnLAmount;
        set => SetProperty(ref _pnLAmount, value);
    }

    public double WinningRate
    {
        get => _winningRate;
        set => SetProperty(ref _winningRate, value);
    }

    public int WinningCount
    {
        get => _winningCount;
        set => SetProperty(ref _winningCount, value);
    }

    public int LoserCount
    {
        get => _loserCount;
        set => SetProperty(ref _loserCount, value);
    }

    public ObservableCollection<Position> Positions { get; }

    public void AddNewPositions(IEnumerable<Position> pos)
    {
        foreach (var position in pos)
            Positions.Add(position);
        CalculatePositionStats();
    }

    public void AddNewPosition(Position p)
    {
        Positions.Add(p);
        CalculatePositionStats();
    }

    public void ClearPositions()
    {
        Positions.Clear();
        CalculatePositionStats();
    }

    #endregion
}