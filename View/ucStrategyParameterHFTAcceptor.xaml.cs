using System.Windows;
using System.Windows.Controls;
using VisualHFT.Helpers;
using VisualHFT.ViewModel;

namespace VisualHFT.View;

/// <summary>
///     Interaction logic for ucStrategyParameterFirmMM.xaml
/// </summary>
public partial class ucStrategyParameterHFTAcceptor : UserControl
{
    //Dependency property
    public static readonly DependencyProperty ucStrategyParameterHFTAcceptorSymbolProperty =
        DependencyProperty.Register(
            "SelectedSymbol",
            typeof(string), typeof(ucStrategyParameterHFTAcceptor),
            new UIPropertyMetadata("", symbolChangedCallBack)
        );

    public static readonly DependencyProperty ucStrategyParameterHFTAcceptorLayerProperty = DependencyProperty.Register(
        "SelectedLayer",
        typeof(string), typeof(ucStrategyParameterHFTAcceptor),
        new UIPropertyMetadata("", layerChangedCallBack)
    );

    public static readonly DependencyProperty ucStrategyParameterHFTAcceptorSelectedStrategyProperty =
        DependencyProperty.Register(
            "SelectedStrategy",
            typeof(string), typeof(ucStrategyParameterHFTAcceptor),
            new UIPropertyMetadata("", strategyChangedCallBack)
        );

    public ucStrategyParameterHFTAcceptor()
    {
        InitializeComponent();
        DataContext = new vmStrategyParameterHFTAcceptor(HelperCommon.GLOBAL_DIALOGS);
        ((vmStrategyParameterHFTAcceptor)DataContext).IsActive = Visibility.Hidden;
    }

    public string SelectedSymbol
    {
        get => (string)GetValue(ucStrategyParameterHFTAcceptorSymbolProperty);
        set
        {
            SetValue(ucStrategyParameterHFTAcceptorSymbolProperty, value);
            ((vmStrategyParameterHFTAcceptor)DataContext).SelectedSymbol = value;
        }
    }

    public string SelectedLayer
    {
        get => (string)GetValue(ucStrategyParameterHFTAcceptorLayerProperty);
        set
        {
            SetValue(ucStrategyParameterHFTAcceptorLayerProperty, value);
            ((vmStrategyParameterHFTAcceptor)DataContext).SelectedLayer = value;
        }
    }

    public string SelectedStrategy
    {
        get => (string)GetValue(ucStrategyParameterHFTAcceptorSelectedStrategyProperty);
        set
        {
            SetValue(ucStrategyParameterHFTAcceptorSelectedStrategyProperty, value);
            ((vmStrategyParameterHFTAcceptor)DataContext).SelectedStrategy = value;
        }
    }

    private static void symbolChangedCallBack(DependencyObject property, DependencyPropertyChangedEventArgs args)
    {
        var ucSelf = (ucStrategyParameterHFTAcceptor)property;
        ucSelf.SelectedSymbol = (string)args.NewValue;
    }

    private static void layerChangedCallBack(DependencyObject property, DependencyPropertyChangedEventArgs args)
    {
        var ucSelf = (ucStrategyParameterHFTAcceptor)property;
        ucSelf.SelectedLayer = (string)args.NewValue;
    }

    private static void strategyChangedCallBack(DependencyObject property, DependencyPropertyChangedEventArgs args)
    {
        var ucSelf = (ucStrategyParameterHFTAcceptor)property;
        ucSelf.SelectedStrategy = (string)args.NewValue;
    }
}