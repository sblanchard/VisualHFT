namespace VisualHFT.AnalyticReports.ViewModel;

public class cPLRangeDuration
{
    public cPLRangeDuration(double duration, double pLRange)
    {
        Duration = duration;
        PLRange = pLRange;
    }

    public double Duration { get; }
    public double PLRange { get; }

    public override bool Equals(object obj)
    {
        return obj is cPLRangeDuration other &&
               Duration == other.Duration &&
               PLRange == other.PLRange;
    }

    public override int GetHashCode()
    {
        var hashCode = 429623771;
        hashCode = hashCode * -1521134295 + Duration.GetHashCode();
        hashCode = hashCode * -1521134295 + PLRange.GetHashCode();
        return hashCode;
    }
}