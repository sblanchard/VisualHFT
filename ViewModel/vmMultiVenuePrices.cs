using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using Prism.Mvvm;
using VisualHFT.Helpers;
using VisualHFT.Model;

namespace VisualHFT.ViewModel;

public class vmMultiVenuePrices : BindableBase, IDisposable
{
    private AggregationLevel _aggregationLevelSelection;
    private readonly Dictionary<int, Tuple<AggregatedCollection<PlotInfo>, LineSeries>> _allDataSeries;
    private bool _disposed; // to track whether the object has been disposed
    private readonly Dictionary<int, double> _latesPrice;
    private readonly int _MAX_ITEMS = 500;
    private string _selectedSymbol;
    private readonly UIUpdater uiUpdater;

    public vmMultiVenuePrices()
    {
        Symbols = new ObservableCollection<string>(HelperSymbol.Instance);
        HelperSymbol.Instance.OnCollectionChanged += ALLSYMBOLS_CollectionChanged;
        RaisePropertyChanged(nameof(Symbols));

        HelperOrderBook.Instance.Subscribe(LIMITORDERBOOK_OnDataReceived);


        AggregationLevels = new ObservableCollection<Tuple<string, AggregationLevel>>();
        foreach (AggregationLevel level in Enum.GetValues(typeof(AggregationLevel)))
            AggregationLevels.Add(new Tuple<string, AggregationLevel>(HelperCommon.GetEnumDescription(level), level));
        AggregationLevelSelection = AggregationLevel.Ms100;
        uiUpdater = new UIUpdater(uiUpdaterAction);

        _allDataSeries = new Dictionary<int, Tuple<AggregatedCollection<PlotInfo>, LineSeries>>();
        _latesPrice = new Dictionary<int, double>();
    }

    public ObservableCollection<string> Symbols { get; set; }

    public string SelectedSymbol
    {
        get => _selectedSymbol;
        set => SetProperty(ref _selectedSymbol, value, () => Clear());
    }

    public AggregationLevel AggregationLevelSelection
    {
        get => _aggregationLevelSelection;
        set => SetProperty(ref _aggregationLevelSelection, value, () => Clear());
    }

    public ObservableCollection<Tuple<string, AggregationLevel>> AggregationLevels { get; set; }
    public PlotModel MyPlotModel { get; private set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~vmMultiVenuePrices()
    {
        Dispose(false);
    }

    private static void Aggregation(PlotInfo existing, PlotInfo newItem)
    {
        existing.Date = newItem.Date;
        existing.Value = newItem.Value;
    }


    private void uiUpdaterAction()
    {
        RaisePropertyChanged(nameof(MyPlotModel));
        if (MyPlotModel != null)
            MyPlotModel.InvalidatePlot(true);
    }

    private void ALLSYMBOLS_CollectionChanged(object? sender, EventArgs e)
    {
        Symbols = new ObservableCollection<string>(HelperSymbol.Instance);
        RaisePropertyChanged(nameof(Symbols));
    }

    private void LIMITORDERBOOK_OnDataReceived(OrderBook e)
    {
        if (_selectedSymbol == "" || _selectedSymbol == "-- All symbols --" || _selectedSymbol != e.Symbol)
            return;


        if (!_allDataSeries.ContainsKey(e.ProviderID))
        {
            _latesPrice.Add(e.ProviderID, 0);
            var series = new LineSeries
            {
                Title = e.ProviderName,
                Color = OxyColors.Beige,
                ItemsSource = null
            };
            if (MyPlotModel == null)
            {
                MyPlotModel = new PlotModel();
                MyPlotModel.IsLegendVisible = true;

                var xAxe = new DateTimeAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = "Timestamp",
                    TitleColor = OxyColors.White,
                    TextColor = OxyColors.White,
                    //xAxe.StringFormat = "HH:mm:ss:fff"

                    AxislineColor = OxyColors.White,
                    TitleFontSize = 16
                };
                var yAxe = new LinearAxis
                {
                    Position = AxisPosition.Right,
                    Title = "Price",
                    TitleColor = OxyColors.White,
                    TextColor = OxyColors.White,
                    StringFormat = "N2",

                    AxislineColor = OxyColors.White,
                    TitleFontSize = 16
                };

                MyPlotModel.Axes.Add(xAxe);
                MyPlotModel.Axes.Add(yAxe);
            }

            var serieColor = MapProviderCodeToOxyColor(e.ProviderID);
            MyPlotModel.Legends.Add(new Legend
            {
                LegendSymbolPlacement = LegendSymbolPlacement.Right,
                LegendTextColor = serieColor,
                LegendItemAlignment = HorizontalAlignment.Right,
                TextColor = serieColor
            });
            series.Color = serieColor;
            MyPlotModel.Series.Add(series);
            MyPlotModel.InvalidatePlot(true); // This refreshes the plot


            _allDataSeries.Add(e.ProviderID, new Tuple<AggregatedCollection<PlotInfo>, LineSeries>(
                new AggregatedCollection<PlotInfo>(_aggregationLevelSelection, _MAX_ITEMS, x => x.Date, Aggregation),
                series)
            );
        }

        _latesPrice[e.ProviderID] = e.MidPrice;
        foreach (var key in _allDataSeries.Keys)
        {
            _allDataSeries[key].Item1.Add(new PlotInfo { Date = DateTime.Now, Value = _latesPrice[key] });
            _allDataSeries[key].Item2.ItemsSource =
                _allDataSeries[key].Item1.Select(x => new DataPoint(x.Date.Ticks, x.Value));
        }
    }

    private OxyColor MapProviderCodeToOxyColor(int providerCode)
    {
        // Get all the OxyColors from the OxyColors class
        var allColors = typeof(OxyColors).GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(field => field.FieldType == typeof(OxyColor))
            .Select(field => (OxyColor)field.GetValue(null))
            .ToArray();

        // Exclude the Undefined and Automatic colors
        allColors = allColors.Where(color => color != OxyColors.Undefined && color != OxyColors.Automatic).ToArray();

        // Shuffle the colors using a seeded random number generator
        allColors = Shuffle(allColors, new Random(providerCode)).ToArray();

        // Return the first color from the shuffled array
        return allColors[0];
    }

    private IEnumerable<T> Shuffle<T>(IEnumerable<T> source, Random rng)
    {
        var elements = source.ToArray();
        for (var i = elements.Length - 1; i >= 0; i--)
        {
            var swapIndex = rng.Next(i + 1);
            yield return elements[swapIndex];
            elements[swapIndex] = elements[i];
        }
    }

    private void Clear()
    {
        MyPlotModel = null;
        if (_allDataSeries != null)
            _allDataSeries.Clear();
        if (_latesPrice != null)
            _latesPrice.Clear();
        RaisePropertyChanged(nameof(MyPlotModel));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                HelperOrderBook.Instance.Unsubscribe(LIMITORDERBOOK_OnDataReceived);
                HelperSymbol.Instance.OnCollectionChanged -= ALLSYMBOLS_CollectionChanged;

                Clear();
                uiUpdater.Dispose();
            }

            _disposed = true;
        }
    }
}