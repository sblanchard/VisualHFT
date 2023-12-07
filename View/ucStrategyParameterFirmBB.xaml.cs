using System.Windows;
using System.Windows.Controls;
using VisualHFT.Helpers;
using VisualHFT.ViewModel;

namespace VisualHFT.View;

/// <summary>
///     Interaction logic for ucStrategyParameterFirmBB.xaml
/// </summary>
public partial class ucStrategyParameterFirmBB : UserControl
{
    //Dependency property

    public static readonly DependencyProperty ucStrategyParameterFirmBBSymbolProperty = DependencyProperty.Register(
        "SelectedSymbol",
        typeof(string), typeof(ucStrategyParameterFirmBB),
        new UIPropertyMetadata("", symbolChangedCallBack)
    );

    public static readonly DependencyProperty ucStrategyParameterFirmBBLayerProperty = DependencyProperty.Register(
        "SelectedLayer",
        typeof(string), typeof(ucStrategyParameterFirmBB),
        new UIPropertyMetadata("", layerChangedCallBack)
    );

    public static readonly DependencyProperty ucStrategyParameterFirmBBSelectedStrategyProperty =
        DependencyProperty.Register(
            "SelectedStrategy",
            typeof(string), typeof(ucStrategyParameterFirmBB),
            new UIPropertyMetadata("", strategyChangedCallBack)
        );

    public ucStrategyParameterFirmBB()
    {
        InitializeComponent();
        DataContext = new vmStrategyParameterFirmBB(HelperCommon.GLOBAL_DIALOGS);
        ((vmStrategyParameterFirmBB)DataContext).IsActive = Visibility.Hidden;
    }


    public string SelectedSymbol
    {
        get => (string)GetValue(ucStrategyParameterFirmBBSymbolProperty);
        set
        {
            SetValue(ucStrategyParameterFirmBBSymbolProperty, value);
            ((vmStrategyParameterFirmBB)DataContext).SelectedSymbol = value;
        }
    }

    public string SelectedLayer
    {
        get => (string)GetValue(ucStrategyParameterFirmBBLayerProperty);
        set
        {
            SetValue(ucStrategyParameterFirmBBLayerProperty, value);
            ((vmStrategyParameterFirmBB)DataContext).SelectedLayer = value;
        }
    }

    public string SelectedStrategy
    {
        get => (string)GetValue(ucStrategyParameterFirmBBSelectedStrategyProperty);
        set
        {
            SetValue(ucStrategyParameterFirmBBSelectedStrategyProperty, value);
            ((vmStrategyParameterFirmBB)DataContext).SelectedStrategy = value;
        }
    }

    private static void symbolChangedCallBack(DependencyObject property, DependencyPropertyChangedEventArgs args)
    {
        var ucSelf = (ucStrategyParameterFirmBB)property;
        ucSelf.SelectedSymbol = (string)args.NewValue;
    }

    private static void layerChangedCallBack(DependencyObject property, DependencyPropertyChangedEventArgs args)
    {
        var ucSelf = (ucStrategyParameterFirmBB)property;
        ucSelf.SelectedLayer = (string)args.NewValue;
    }

    private static void strategyChangedCallBack(DependencyObject property, DependencyPropertyChangedEventArgs args)
    {
        var ucSelf = (ucStrategyParameterFirmBB)property;
        ucSelf.SelectedStrategy = (string)args.NewValue;
    }
}