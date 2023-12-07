namespace VisualHFT.AnalyticReport;

public class ScatterChartData
{
    public ScatterChartData(double x, double y, string toolTip)
    {
        XValue = x;
        YValue = y;
        ToolTip = toolTip;
    }

    public double XValue { get; set; }
    public double YValue { get; set; }

    public string Brush
    {
        get
        {
            if (YValue < 0)
                return "Red";
            return "Green";
        }
    }

    public string ToolTip { get; set; }
}