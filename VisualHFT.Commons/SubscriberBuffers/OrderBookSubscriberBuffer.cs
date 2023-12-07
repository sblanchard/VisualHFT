using System.Collections.Concurrent;
using VisualHFT.Model;

namespace VisualHFT.Commons.SubscriberBuffers;

public class OrderBookSubscriberBuffer
{
    public OrderBookSubscriberBuffer(Action<OrderBook> processor)
    {
        Processor = processor;
        Task.Run(Process);
    }

    public BlockingCollection<OrderBook> Buffer { get; } = new();
    public Action<OrderBook> Processor { get; }

    public int Count => Buffer.Count;

    private void Process()
    {
        foreach (var book in Buffer.GetConsumingEnumerable()) Processor(book);
    }

    public void Add(OrderBook book)
    {
        Buffer.Add(book);
    }
}