using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VisualHFT.Model;

namespace VisualHFT.Helpers;

public class HelperActiveOrder : ConcurrentDictionary<string, Order>
{
    public event EventHandler<Order> OnDataReceived;
    public event EventHandler<Order> OnDataRemoved;

    ~HelperActiveOrder()
    {
    }

    protected virtual void RaiseOnDataReceived(List<Order> orders)
    {
        var _handler = OnDataReceived;
        if (_handler != null && Application.Current != null)
            foreach (var o in orders)
                _handler(this, o);
    }

    protected virtual void RaiseOnDataRemoved(List<Order> orders)
    {
        var _handler = OnDataRemoved;
        if (_handler != null && Application.Current != null)
            foreach (var o in orders)
                _handler(this, o);
    }

    public bool TryFindOrder(int providerId, string symbol, double price, out Order order)
    {
        var o = this.Select(x => x.Value)
            .Where(x => x.ProviderId == providerId && x.Symbol == symbol && x.PricePlaced == price).FirstOrDefault();
        if (o != null)
        {
            return TryGetValue(o.ClOrdId, out order);
        }

        order = null;
        return false;
    }

    public void UpdateData(IEnumerable<Order> orders)
    {
        var _listToRemove = new List<Order>();
        var _listToAdd = new List<Order>();
        _listToRemove = this.Where(x => !orders.Any(o => o.ClOrdId == x.Value.ClOrdId)).Select(x => x.Value).ToList();
        _listToAdd = orders.Where(x => !this.Any(o => o.Value.ClOrdId == x.ClOrdId)).ToList();


        foreach (var o in _listToRemove)
            TryRemove(o.ClOrdId, out var fakeOrder);
        if (_listToRemove.Any())
            RaiseOnDataRemoved(_listToRemove);

        var _listToAddOrUpdate = new List<Order>();
        foreach (var o in _listToAdd)
            if (UpdateData(o))
                _listToAddOrUpdate.Add(o);
        if (_listToAddOrUpdate.Any())
            RaiseOnDataReceived(_listToAddOrUpdate);
    }

    private bool UpdateData(Order order)
    {
        if (order != null)
        {
            //Check provider
            if (!ContainsKey(order.ClOrdId))
            {
                order.CreationTimeStamp = DateTime.Now;
                return TryAdd(order.ClOrdId, order);
            }

            return TryUpdate(order.ClOrdId, order, this[order.ClOrdId]);
        }

        return false;
    }
}