using System.Windows;
using System.Windows.Controls;
using VisualHFT.Helpers;
using VisualHFT.ViewModel;

namespace VisualHFT.View;

/// <summary>
///     Interaction logic for ucStrategyParameterFirmMM.xaml
/// </summary>
public partial class ucStrategyParameterBBook : UserControl
{
    //Dependency property
    public static readonly DependencyProperty ucStrategyParameterBBookSymbolProperty = DependencyProperty.Register(
        "SelectedSymbol",
        typeof(string), typeof(ucStrategyParameterBBook),
        new UIPropertyMetadata("", symbolChangedCallBack)
    );

    public static readonly DependencyProperty ucStrategyParameterBBookLayerProperty = DependencyProperty.Register(
        "SelectedLayer",
        typeof(string), typeof(ucStrategyParameterBBook),
        new UIPropertyMetadata("", layerChangedCallBack)
    );

    public static readonly DependencyProperty ucStrategyParameterBBookSelectedStrategyProperty =
        DependencyProperty.Register(
            "SelectedStrategy",
            typeof(string), typeof(ucStrategyParameterBBook),
            new UIPropertyMetadata("", strategyChangedCallBack)
        );

    private string _selectedLayer;


    private string _selectedStrategy;


    private string _selectedSymbol;

    public ucStrategyParameterBBook()
    {
        InitializeComponent();
        DataContext = new vmStrategyParameterBBook(HelperCommon.GLOBAL_DIALOGS);
        ((vmStrategyParameterBBook)DataContext).IsActive = Visibility.Hidden;
    }

    public string SelectedSymbol
    {
        get => _selectedSymbol;
        set
        {
            _selectedSymbol = value;
            ((vmStrategyParameterBBook)DataContext).SelectedSymbol = value;
        }
    }

    public string SelectedLayer
    {
        get => _selectedLayer;
        set
        {
            _selectedLayer = value;
            ((vmStrategyParameterBBook)DataContext).SelectedLayer = value;
        }
    }

    public string SelectedStrategy
    {
        get => _selectedStrategy;
        set
        {
            _selectedStrategy = value;
            ((vmStrategyParameterBBook)DataContext).SelectedStrategy = value;
        }
    }


    private static void symbolChangedCallBack(DependencyObject property, DependencyPropertyChangedEventArgs args)
    {
        var ucSelf = (ucStrategyParameterBBook)property;
        ucSelf.SelectedSymbol = (string)args.NewValue;
    }

    private static void layerChangedCallBack(DependencyObject property, DependencyPropertyChangedEventArgs args)
    {
        var ucSelf = (ucStrategyParameterBBook)property;
        ucSelf.SelectedLayer = (string)args.NewValue;
    }

    private static void strategyChangedCallBack(DependencyObject property, DependencyPropertyChangedEventArgs args)
    {
        var ucSelf = (ucStrategyParameterBBook)property;
        ucSelf.SelectedStrategy = (string)args.NewValue;
    }
}