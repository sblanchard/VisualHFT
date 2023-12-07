namespace VisualHFT.AnalyticReports.ViewModel;

public class cheldBars
{
    public cheldBars(int secondsHeld, decimal? totalPnL, decimal totalWinPnL, decimal totalLossPnl, int winPnLCount,
        int lossPnlCount)
    {
        SecondsHeld = secondsHeld;
        TotalPnL = totalPnL;
        TotalWinPnL = totalWinPnL;
        TotalLossPnl = totalLossPnl;
        WinPnLCount = winPnLCount;
        LossPnlCount = lossPnlCount;
    }

    public int SecondsHeld { get; }
    public decimal? TotalPnL { get; }
    public decimal TotalWinPnL { get; }
    public decimal TotalLossPnl { get; }
    public int WinPnLCount { get; }
    public int LossPnlCount { get; }

    public override bool Equals(object obj)
    {
        return obj is cheldBars other &&
               SecondsHeld == other.SecondsHeld &&
               TotalPnL == other.TotalPnL &&
               TotalWinPnL == other.TotalWinPnL &&
               TotalLossPnl == other.TotalLossPnl &&
               WinPnLCount == other.WinPnLCount &&
               LossPnlCount == other.LossPnlCount;
    }

    public override int GetHashCode()
    {
        var hashCode = -2137046466;
        hashCode = hashCode * -1521134295 + SecondsHeld.GetHashCode();
        hashCode = hashCode * -1521134295 + TotalPnL.GetHashCode();
        hashCode = hashCode * -1521134295 + TotalWinPnL.GetHashCode();
        hashCode = hashCode * -1521134295 + TotalLossPnl.GetHashCode();
        hashCode = hashCode * -1521134295 + WinPnLCount.GetHashCode();
        hashCode = hashCode * -1521134295 + LossPnlCount.GetHashCode();
        return hashCode;
    }
}