namespace VisualHFT.DataRetriever;

public interface IDataRetriever : IDisposable
{
    event EventHandler<DataEventArgs> OnDataReceived;
    Task StartAsync();
    Task StopAsync();
}

public class DataEventArgs : EventArgs
{
    public string DataType { get; set; }
    public string RawData { get; set; }
    public object ParsedModel { get; set; }
}