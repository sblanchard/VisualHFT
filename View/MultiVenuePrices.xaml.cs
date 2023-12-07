using System;
using System.Windows;

namespace VisualHFT.View;

/// <summary>
///     Interaction logic for LOBImbalances.xaml
/// </summary>
public partial class MultiVenuePrices : Window
{
    public MultiVenuePrices()
    {
        InitializeComponent();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable) disposable.Dispose();
    }
}