using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Prism.Mvvm;
using VisualHFT.Commons.Studies;
using VisualHFT.Helpers;
using VisualHFT.Model;
using VisualHFT.PluginManager;
using VisualHFT.UserSettings;
using VisualHFT.View;
using VisualHFT.ViewModels;

namespace VisualHFT.ViewModel;

public class vmTile : BindableBase, IDisposable
{
    private Visibility _chartButtonVisibility;
    private bool _disposed; // to track whether the object has been disposed
    private bool _isGroup;
    private readonly IMultiStudy _multiStudy;

    private Visibility _settingButtonVisibility;
    //*********************************************************
    //*********************************************************

    private TileSettings _settings;

    //*********************************************************
    //*********************************************************
    private readonly IStudy _study;
    private string _tile_id;
    private string _title;
    private string _tooltip;
    private readonly UIUpdater uiUpdater;

    public vmTile(IMultiStudy multiStudy)
    {
        IsGroup = true;

        _multiStudy = multiStudy;
        ChildTiles = new ObservableCollection<vmTile>();
        foreach (var study in _multiStudy.Studies)
            ChildTiles.Add(new vmTile(study)
                { SettingButtonVisibility = Visibility.Hidden, ChartButtonVisibility = Visibility.Hidden });

        _tile_id = ((IPlugin)_multiStudy).GetPluginUniqueID();
        _title = _multiStudy.TileTitle;
        _tooltip = _multiStudy.TileToolTip;
        Value = ".";

        OpenSettingsCommand = new RelayCommand<vmTile>(OpenSettings);
        OpenChartCommand = new RelayCommand<vmTile>(OpenChartClick);
        uiUpdater = new UIUpdater(uiUpdaterAction, 300);

        RaisePropertyChanged(nameof(SelectedSymbol));
        RaisePropertyChanged(nameof(SelectedProviderName));

        RaisePropertyChanged(nameof(Title));
        RaisePropertyChanged(nameof(Tooltip));
        RaisePropertyChanged(nameof(IsGroup));
        SettingButtonVisibility = Visibility.Hidden;
        ChartButtonVisibility = Visibility.Hidden;
    }

    public vmTile(IStudy study)
    {
        IsGroup = false;

        _study = study;
        _tile_id = ((IPlugin)_study).GetPluginUniqueID();
        _title = _study.TileTitle;
        _tooltip = _study.TileToolTip;
        Value = ".";

        _study.OnCalculated += _study_OnCalculated;

        OpenSettingsCommand = new RelayCommand<vmTile>(OpenSettings);
        OpenChartCommand = new RelayCommand<vmTile>(OpenChartClick);
        uiUpdater = new UIUpdater(uiUpdaterAction, 300);

        RaisePropertyChanged(nameof(SelectedSymbol));
        RaisePropertyChanged(nameof(SelectedProviderName));

        RaisePropertyChanged(nameof(Title));
        RaisePropertyChanged(nameof(Tooltip));
        RaisePropertyChanged(nameof(IsGroup));
        SettingButtonVisibility = Visibility.Visible;
        ChartButtonVisibility = Visibility.Visible;
    }

    public ICommand OpenSettingsCommand { get; set; }
    public ICommand OpenChartCommand { get; }

    public string Value { get; private set; }

    public SolidColorBrush ValueColor { get; private set; } = Brushes.White;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Tooltip
    {
        get => _tooltip;
        set => SetProperty(ref _tooltip, value);
    }

    public string SelectedSymbol
    {
        get
        {
            if (_study != null)
                return ((IPlugin)_study).Settings.Symbol;
            if (_multiStudy != null)
                return ((IPlugin)_multiStudy).Settings.Symbol;
            return "";
        }
    }

    public string SelectedProviderName
    {
        get
        {
            if (_study != null)
                return ((IPlugin)_study).Settings.Provider.ProviderName;
            if (_multiStudy != null)
                return ((IPlugin)_multiStudy).Settings.Provider.ProviderName;
            return "";
        }
    }

    public bool IsGroup
    {
        get => _isGroup;
        set => SetProperty(ref _isGroup, value);
    }

    public Visibility SettingButtonVisibility
    {
        get => _settingButtonVisibility;
        set => SetProperty(ref _settingButtonVisibility, value);
    }

    public Visibility ChartButtonVisibility
    {
        get => _chartButtonVisibility;
        set => SetProperty(ref _chartButtonVisibility, value);
    }

    public ObservableCollection<vmTile> ChildTiles { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void _study_OnCalculated(object? sender, BaseStudyModel e)
    {
        Value = e.ValueFormatted;
        if (e.ValueColor != null)
            Application.Current.Dispatcher.Invoke(() =>
            {
                ValueColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(e.ValueColor));
            });
    }


    ~vmTile()
    {
        Dispose(false);
    }

    private void uiUpdaterAction()
    {
        RaisePropertyChanged(nameof(Value));
        RaisePropertyChanged(nameof(ValueColor));
    }

    public void UpdateAllUI()
    {
        uiUpdaterAction();
        RaisePropertyChanged(nameof(SelectedSymbol));
        RaisePropertyChanged(nameof(SelectedProviderName));
    }

    private void OpenChartClick(object obj)
    {
        if (_study != null)
        {
            var winChart = new ChartStudy();
            winChart.DataContext = new vmChartStudy(_study);
            winChart.Show();
        }
        else if (_multiStudy != null)
        {
            var winChart = new ChartStudy();
            winChart.DataContext = new vmChartStudy(_multiStudy);
            winChart.Show();
        }
    }

    private void OpenSettings(object obj)
    {
        if (_study != null)
        {
            PluginManager.PluginManager.SettingPlugin((IPlugin)_study);
        }
        else if (_multiStudy != null)
        {
            PluginManager.PluginManager.SettingPlugin((IPlugin)_multiStudy);
            foreach (var child in ChildTiles) child.UpdateAllUI();
        }

        RaisePropertyChanged(nameof(SelectedSymbol));
        RaisePropertyChanged(nameof(SelectedProviderName));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_study != null)
                {
                    _study.OnCalculated -= _study_OnCalculated;
                    _study.Dispose();
                }

                uiUpdater.Dispose();
            }

            _disposed = true;
        }
    }
}