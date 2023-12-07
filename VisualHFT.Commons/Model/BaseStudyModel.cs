namespace VisualHFT.Model;

public class BaseStudyModel
{
    public DateTime Timestamp { get; set; }

    public decimal Value { get; set; }

    public string ValueFormatted { get; set; }

    public string ValueColor { get; set; } = null;

    public decimal MarketMidPrice { get; set; }
}