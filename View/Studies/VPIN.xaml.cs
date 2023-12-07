using System;
using System.Windows;

namespace VisualHFT.View;

/// <summary>
///     Interaction logic for VPIN.xaml
/// </summary>
public partial class VPIN : Window
{
    public VPIN()
    {
        InitializeComponent();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable) disposable.Dispose();
    }
}