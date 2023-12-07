namespace VisualHFT.Model;

public class OpenExecution
{
    public int ExecutionID { get; set; }
    public long PositionID { get; set; }
    public string ClOrdId { get; set; }
    public string ExecID { get; set; }
    public DateTime LocalTimeStamp { get; set; }
    public DateTime ServerTimeStamp { get; set; }
    public decimal? Price { get; set; }
    public int ProviderID { get; set; }
    public decimal? QtyFilled { get; set; }
    public int? Side { get; set; }
    public int? Status { get; set; }
    public bool IsOpen { get; set; }
}