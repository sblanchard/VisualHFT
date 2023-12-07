using System;
using System.Windows;

namespace VisualHFT.View;

/// <summary>
///     Interaction logic for TradeToOrderRatio.xaml
/// </summary>
public partial class MarketResilience : Window
{
    public MarketResilience()
    {
        InitializeComponent();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable) disposable.Dispose();
    }
}