﻿using System;

namespace VisualHFT.Helpers;

public class HelperStrategyParams
{
    public event EventHandler<string> OnDataUpdateReceived;

    public virtual void RaiseOnDataUpdateReceived(string jsonData)
    {
        var _handler = OnDataUpdateReceived;
        if (_handler != null) _handler(this, jsonData);
    }
}