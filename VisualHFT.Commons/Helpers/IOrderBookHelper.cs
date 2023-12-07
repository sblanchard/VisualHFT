using VisualHFT.Model;

namespace VisualHFT.Helpers;

public interface IOrderBookHelper
{
    //event EventHandler<OrderBook> OnDataReceived;
    void Subscribe(Action<OrderBook> processor);
    void UpdateData(IEnumerable<OrderBook> data);
}