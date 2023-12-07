using System;
using System.Collections.Generic;
using Prism.Mvvm;
using VisualHFT.Model;

namespace VisualHFT.AnalyticReports.ViewModel;

public class vmStrategyHeader : BindableBase
{
    public List<Position> Signals { get; set; }
    public string StrategyName { get; private set; }
    public string StrategyText { get; private set; }

    public void LoadData(List<Position> signals)
    {
        Signals = signals;
        if (Signals == null || Signals.Count == 0)
            throw new Exception("No signals found.");

        try
        {
            //StrategyName = "Strategies: " + string.Join(", ", this.Signals.Select(x => x.StrategyUsed.StrategyCode).Distinct().ToArray());
            //StrategyText = " ----- ";
        }
        catch
        {
        }

        RaisePropertyChanged(string.Empty);
    }
}