using System.Collections.ObjectModel;
using VisualHFT.Model;

namespace VisualHFT.DataTradeRetriever;

public interface IDataTradeRetriever
{
    DateTime? SessionDate { get; set; }

    ReadOnlyCollection<Order> Orders { get; }
    ReadOnlyCollection<Position> Positions { get; }
    event EventHandler<IEnumerable<Order>> OnInitialLoad;
    event EventHandler<IEnumerable<Order>> OnDataReceived;
}