﻿using System;
using System.Windows;

namespace VisualHFT.View;

/// <summary>
///     Interaction logic for VPIN.xaml
/// </summary>
public partial class ChartStudy : Window
{
    public ChartStudy()
    {
        InitializeComponent();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable) disposable.Dispose();
    }
}