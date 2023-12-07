using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Prism.Mvvm;
using VisualHFT.Helpers;
using VisualHFT.Model;

namespace VisualHFT.AnalyticReports.ViewModel;

public class vmOverview : BindableBase
{
    public List<Position> Signals { get; set; }

    public string VolumeTraded { get; private set; }
    public string NumTrades { get; private set; }
    public string SharpeRatio { get; private set; }
    public string ProfitFactor { get; private set; }
    public string Expectancy { get; private set; }
    public string WinningPerc { get; private set; }
    public string MaxDrawDownPercDaily { get; private set; }
    public string MaxDrawDownPercIntraday { get; private set; }
    public string DailyAvgProfit { get; private set; }
    public string DailyAvgTrades { get; private set; }
    public string HourlyAvgProfit { get; private set; }
    public string HourlyAvgTrades { get; private set; }
    public string tTestValue { get; private set; }

    public void LoadData(List<Position> signals)
    {
        //if (ValuesChanged != null)
        //    ValuesChanged(this, new EventArgs(), 0, 0);

        Signals = signals;
        if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
        {
            if (Signals == null || Signals.Count == 0)
                throw new Exception("No signals found.");
        }
        else
        {
            return;
        }

        var oSignal = Signals[0];
        if (oSignal != null)
        {
            double totalCount = Signals.Count;
            var totalReturn = Signals.Sum(s => s.PipsPnLInCurrency.Value);
            double winCount = Signals.Where(s => s.PipsPnLInCurrency.Value >= 0).Count();
            double lossCount = Signals.Where(s => s.PipsPnLInCurrency.Value < 0).Count();
            var grossProfit = Signals.Where(s => s.PipsPnLInCurrency.HasValue && s.PipsPnLInCurrency.Value >= 0)
                .Sum(s => s.PipsPnLInCurrency.Value);
            var grossLoss = Signals.Where(s => s.PipsPnLInCurrency.HasValue && s.PipsPnLInCurrency.Value < 0)
                .Sum(s => s.PipsPnLInCurrency.Value);

            VolumeTraded = HelperFormat.FormatNumber((double)Signals.Sum(x => x.GetCloseQuantity + x.GetOpenQuantity));

            NumTrades = totalCount.ToString("n0");

            SharpeRatio = HelperAnalytics.GetIntradaySharpeRatio(Signals).ToString("n2");

            if (grossLoss > 0.0m)
                ProfitFactor = Math.Abs(grossProfit / grossLoss).ToString("n2");
            else
                ProfitFactor = "";

            Expectancy = HelperAnalytics.GetExpectancy(Signals).ToString("n2");

            WinningPerc = (winCount / totalCount).ToString("p2");

            //******************************************************************************************************
            MaxDrawDownPercDaily = HelperAnalytics.GetMaximumDrawdownPerc(Signals, false).ToString("p2");
            MaxDrawDownPercIntraday = HelperAnalytics.GetMaximumDrawdownPerc(Signals).ToString("p2");


            //HOURLY AVG PROFIT
            var avgDailyPnL = HelperAnalytics.GetAverageProfitByDay(Signals);
            DailyAvgProfit = avgDailyPnL.Equity.ToString("p2");
            DailyAvgTrades = "(" + avgDailyPnL.VolumeQty.ToString("n0") + ")";

            var avgHourlyPnL = HelperAnalytics.GetAverageProfitByHour(Signals);
            HourlyAvgProfit = avgHourlyPnL.Equity.ToString("p2");
            HourlyAvgTrades = "(" + avgHourlyPnL.VolumeQty.ToString("n0") + ")";

            //t = square root ( number of trades ) * (average profit per trade trade / standard deviation of trades)
            var tradesPnL = Signals.Select(x => (double)x.PipsPnLInCurrency).ToList();
            tTestValue = (Math.Sqrt(totalCount) * tradesPnL.Average() / tradesPnL.StdDev()).ToString("n2");

            RaisePropertyChanged(string.Empty);
        }
    }
}