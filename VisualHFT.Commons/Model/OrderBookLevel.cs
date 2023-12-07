namespace VisualHFT.Model;

public class OrderBookLevel
{
    public DateTime Date { get; set; }

    public double DateIndex { get; set; }

    public double Price { get; set; }

    public double Size { get; set; }
}