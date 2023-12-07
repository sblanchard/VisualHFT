using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Prism.Mvvm;
using VisualHFT.Commons.Studies;
using VisualHFT.Helpers;
using VisualHFT.Model;
using VisualHFT.PluginManager;
using VisualHFT.UserSettings;
using VisualHFT.ViewModel;
using Provider = VisualHFT.ViewModel.Model.Provider;

namespace VisualHFT.ViewModels
{
    public class vmChartStudy : BindableBase, IDisposable
    {
        private readonly object _locker = new object();


        private Dictionary<string, Tuple<AggregatedCollection<PlotInfo>, LineSeries>> _allDataSeries;
        private bool _disposed = false; // to track whether the object has been disposed

        private int _MAX_ITEMS = 500;
        private IPlugin _plugin;

        private ObservableCollection<AggregatedCollection<BaseStudyModel>> _rollingValues;
        private Provider _selectedProvider;
        private string _selectedSymbol;
        private ISetting _settings;
        private List<IStudy> _studies = new List<IStudy>();

        private Dictionary<IStudy, AggregatedCollection<BaseStudyModel>> _studyToDataMap =
            new Dictionary<IStudy, AggregatedCollection<BaseStudyModel>>();

        private UIUpdater uiUpdater;

        public vmChartStudy(IStudy study)
        {
            _studies.Add(study);
            _settings = ((IPlugin)study).Settings;
            _plugin = (IPlugin)study;
            StudyAxisTitle = study.TileTitle;

            RaisePropertyChanged(nameof(StudyAxisTitle));
            OpenSettingsCommand = new RelayCommand<vmTile>(OpenSettings);
            InitializeChart();
            InitializeData();
            uiUpdater = new UIUpdater(uiUpdaterAction);
        }

        public vmChartStudy(IMultiStudy multiStudy)
        {
            foreach (var study in multiStudy.Studies)
            {
                _studies.Add(study);
            }

            _settings = ((IPlugin)multiStudy).Settings;
            _plugin = (IPlugin)multiStudy;
            StudyAxisTitle = multiStudy.TileTitle;

            RaisePropertyChanged(nameof(StudyAxisTitle));
            OpenSettingsCommand = new RelayCommand<vmTile>(OpenSettings);
            InitializeChart();
            InitializeData();
            uiUpdater = new UIUpdater(uiUpdaterAction);
        }

        public string StudyTitle { get; set; }
        public string StudyAxisTitle { get; set; }
        public ICommand OpenSettingsCommand { get; set; }
        public PlotModel MyPlotModel { get; set; }

        public ObservableCollection<AggregatedCollection<BaseStudyModel>> ChartData
        {
            get
            {
                lock (_locker)
                    return _rollingValues;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~vmChartStudy()
        {
            Dispose(false);
        }

        private void _study_OnCalculated(object? sender, BaseStudyModel e)
        {
            //need to link the incoming study with the _allDataSeries
            string key = ((IStudy)sender).TileTitle;
            var item = _allDataSeries[key].Item1.GetObjectPool().Get();
            item.Date = e.Timestamp;
            item.Value = (double)e.Value;
            _allDataSeries[key].Item1.Add(item);
            _allDataSeries[key].Item2.ItemsSource =
                _allDataSeries[key].Item1.Select(x => new DataPoint(x.Date.Ticks, x.Value));


            key = "Market Mid Price";
            _allDataSeries[key].Item1.Add(new PlotInfo() { Date = e.Timestamp, Value = (double)e.MarketMidPrice });
            _allDataSeries[key].Item2.ItemsSource =
                _allDataSeries[key].Item1.Select(x => new DataPoint(x.Date.Ticks, x.Value));
        }

        private void uiUpdaterAction()
        {
            RaisePropertyChanged(nameof(MyPlotModel));
            if (MyPlotModel != null)
                MyPlotModel.InvalidatePlot(true);
        }

        private void OpenSettings(object obj)
        {
            PluginManager.PluginManager.SettingPlugin(_plugin);
            InitializeData();
        }

        private void InitializeChart()
        {
            MyPlotModel = new PlotModel();
            MyPlotModel.IsLegendVisible = true;

            var xAxe = new DateTimeAxis()
            {
                Position = AxisPosition.Bottom,
                Title = "Timestamp",
                TitleColor = OxyColors.White,
                TextColor = OxyColors.White,
                //StringFormat = "HH:mm:ss",

                AxislineColor = OxyColors.White,
                TitleFontSize = 16
            };
            var yAxe = new LinearAxis()
            {
                Key = "yAxe",
                Position = AxisPosition.Left,
                Title = this.StudyAxisTitle,
                TitleColor = OxyColors.Blue,
                TextColor = OxyColors.Blue,
                StringFormat = "N2",

                AxislineColor = OxyColors.White,
                TitleFontSize = 16,
                FontSize = 12
            };
            var yAxeMarket = new LinearAxis()
            {
                Key = "yAxeMarket",
                Position = AxisPosition.Right,
                Title = "Market Mid Price",
                TitleColor = OxyColors.White,
                TextColor = OxyColors.White,
                StringFormat = "N2",

                AxislineColor = OxyColors.White,
                TitleFontSize = 16,
                FontSize = 12
            };
            MyPlotModel.Axes.Add(xAxe);
            MyPlotModel.Axes.Add(yAxe);
            MyPlotModel.Axes.Add(yAxeMarket);
            MyPlotModel.InvalidatePlot(true);
        }

        private void InitializeData()
        {
            _allDataSeries = new Dictionary<string, Tuple<AggregatedCollection<PlotInfo>, LineSeries>>();

            _rollingValues = new ObservableCollection<AggregatedCollection<BaseStudyModel>>();
            _studyToDataMap.Clear(); // Clear the map when re-initializing


            MyPlotModel.Series.Clear();
            foreach (IStudy study in _studies)
            {
                study.OnCalculated += _study_OnCalculated;

                var series = new LineSeries
                {
                    DataFieldX = "Date",
                    DataFieldY = "Value",
                    Title = study.TileTitle,
                    YAxisKey = "yAxe"
                };

                _allDataSeries.Add(study.TileTitle, new Tuple<AggregatedCollection<PlotInfo>, LineSeries>(
                    new AggregatedCollection<PlotInfo>(_settings.AggregationLevel, _MAX_ITEMS, x => x.Date,
                        (PlotInfo existing, PlotInfo newItem) => { existing.Value = newItem.Value; }),
                    series)
                );
                MyPlotModel.Series.Add(series);
            }

            //ADD MARKET SERIES
            {
                var mktSeries = new LineSeries
                {
                    Title = "Market Mid Price",
                    Color = OxyColors.White,
                    StrokeThickness = 5,
                    YAxisKey = "yAxeMarket",
                    TrackerFormatString = "{0}\n{1}: {2}\n{3}: {4}"
                };
                _allDataSeries.Add(mktSeries.Title, new Tuple<AggregatedCollection<PlotInfo>, LineSeries>(
                    new AggregatedCollection<PlotInfo>(_settings.AggregationLevel, _MAX_ITEMS, x => x.Date,
                        (PlotInfo existing, PlotInfo newItem) => { existing.Value = newItem.Value; }),
                    mktSeries)
                );
                MyPlotModel.Series.Add(mktSeries);
            }
            MyPlotModel.InvalidatePlot(true); // Forces the plot to redraw


            StudyTitle = StudyAxisTitle + " " +
                         _settings.Symbol + "-" +
                         _settings.Provider?.ProviderName + " " +
                         "[" + _settings.AggregationLevel.ToString() + "]";
            RaisePropertyChanged(nameof(StudyTitle));

            _selectedSymbol = _settings.Symbol;
            _selectedProvider = new Provider(_settings.Provider);
            uiUpdaterAction();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _studyToDataMap.Clear();
                    uiUpdater.Dispose();
                    if (_studies != null)
                    {
                        foreach (var s in _studies)
                        {
                            s.OnCalculated -= _study_OnCalculated;
                            s.Dispose();
                        }
                    }
                }

                _disposed = true;
            }
        }
    }
}