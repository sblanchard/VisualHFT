using System.Collections.Concurrent;
using VisualHFT.Model;

namespace VisualHFT.Commons.SubscriberBuffers;

public class TradeSubscriberBuffer
{
    public TradeSubscriberBuffer(Action<Trade> processor)
    {
        Processor = processor;
        Task.Run(Process);
    }

    public BlockingCollection<Trade> Buffer { get; } = new();
    public Action<Trade> Processor { get; }

    public int Count => Buffer.Count;

    private void Process()
    {
        foreach (var trade in Buffer.GetConsumingEnumerable()) Processor(trade);
    }

    public void Add(Trade book)
    {
        Buffer.Add(book);
    }
}