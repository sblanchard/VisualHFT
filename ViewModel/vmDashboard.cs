using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using log4net;
using Prism.Mvvm;
using VisualHFT.Commons.Studies;
using VisualHFT.Helpers;

namespace VisualHFT.ViewModel;

public class vmDashboard : BindableBase
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private readonly Dictionary<string, Func<string, string, bool>> _dialogs;
    private string _selectedLayer;
    private string _selectedStrategy;
    private string _selectedSymbol;
    private ObservableCollection<vmTile> _tiles;
    protected vmOrderBook _vmOrderBook;
    protected vmPosition _vmPosition;

    protected vmStrategyParameterFirmMM _vmStrategyParamsFirmMM;


    public vmDashboard(Dictionary<string, Func<string, string, bool>> dialogs)
    {
        _dialogs = dialogs;
        CmdAbort = new RelayCommand<object>(DoAbort);

        HelperSymbol.Instance.OnCollectionChanged += ALLSYMBOLS_CollectionChanged;

        StrategyParamsFirmMM = new vmStrategyParameterFirmMM(HelperCommon.GLOBAL_DIALOGS);
        Positions = new vmPosition(HelperCommon.GLOBAL_DIALOGS);
        OrderBook = new vmOrderBook(HelperCommon.GLOBAL_DIALOGS);

        Task.Run(LoadTilesAsync);
    }


    public ObservableCollection<vmTile> Tiles
    {
        get => _tiles;
        set => SetProperty(ref _tiles, value);
    }

    public vmStrategyParameterFirmMM StrategyParamsFirmMM
    {
        get => _vmStrategyParamsFirmMM;
        set => SetProperty(ref _vmStrategyParamsFirmMM, value);
    }

    public vmPosition Positions
    {
        get => _vmPosition;
        set => SetProperty(ref _vmPosition, value);
    }

    public vmOrderBook OrderBook
    {
        get => _vmOrderBook;
        set => SetProperty(ref _vmOrderBook, value);
    }

    public RelayCommand<object> CmdAbort { get; set; }


    public string SelectedStrategy
    {
        get => _selectedStrategy;

        set
        {
            if (string.IsNullOrEmpty(value))
                value = "";
            if (value.IndexOf(":") > -1)
            {
                _selectedStrategy = value.Split(':')[0].Trim();
                _selectedLayer = value.Split(':')[1].Trim();
            }
            else
            {
                _selectedStrategy = value;
                _selectedLayer = "";
            }

            if (value != "")
            {
                _selectedSymbol = "-- All symbols --";
                if (_vmStrategyParamsFirmMM != null) _vmStrategyParamsFirmMM.SelectedStrategy = value;
                if (_vmPosition != null) _vmPosition.SelectedStrategy = value;

                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SelectedSymbol));
                RaisePropertyChanged(nameof(SelectedLayer));
            }

            ;
        }
    }

    public string SelectedSymbol
    {
        get => _selectedSymbol;
        set
        {
            if (_selectedSymbol != value)
            {
                _selectedSymbol = value;
                if (_vmStrategyParamsFirmMM != null) _vmStrategyParamsFirmMM.SelectedSymbol = value;
                if (_vmPosition != null) _vmPosition.SelectedSymbol = value;
                if (_vmOrderBook != null) _vmOrderBook.SelectedSymbol = value;

                RaisePropertyChanged();
            }
        }
    }

    public string SelectedLayer
    {
        get => _selectedLayer;
        set
        {
            if (_selectedLayer != value)
            {
                _selectedLayer = value;
                if (_vmOrderBook != null) _vmOrderBook.SelectedLayer = value;

                RaisePropertyChanged();
            }
        }
    }

    public ObservableCollection<string> SymbolList => new(HelperSymbol.Instance);

    private async Task LoadTilesAsync()
    {
        while (!PluginManager.PluginManager.AllPluginsReloaded)
            await Task.Delay(1000); // allow plugins to be loaded in

        Tiles = new ObservableCollection<vmTile>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            //first, load single studies
            foreach (var study in PluginManager.PluginManager.AllPlugins.Where(x => x is IStudy))
                Tiles.Add(new vmTile(study as IStudy));
            //then, load multi-studies
            foreach (var study in PluginManager.PluginManager.AllPlugins.Where(x => x is IMultiStudy))
                Tiles.Add(new vmTile(study as IMultiStudy));
        });
    }

    private void ALLSYMBOLS_CollectionChanged(object? sender, EventArgs e)
    {
        RefreshSymbolList();
    }

    private void RefreshSymbolList()
    {
        try
        {
            RaisePropertyChanged(nameof(SymbolList));
        }
        catch (Exception ex)
        {
            log.Error(ex.ToString());
        }
    }

    private void DoAbort(object item)
    {
        if (_dialogs.ContainsKey("confirm"))
            if (!_dialogs["confirm"]("Are you sure you want to abort the system?", ""))
                return;
        var bwDoAbort = new BackgroundWorker();
        bwDoAbort.DoWork += (ss, args) =>
        {
            try
            {
                args.Result = RESTFulHelper.SetVariable("ABORTSYSTEM");
            }
            catch
            {
                /*System.Threading.Thread.Sleep(5000);*/
            }
        };
        bwDoAbort.RunWorkerCompleted += (ss, args) =>
        {
            if (args.Result == null)
                _dialogs["popup"]("Message timeout.", "System Abort");
            else if (args.Result.ToBoolean())
                _dialogs["popup"]("Message received OK.", "System Abort");
            else
                _dialogs["popup"]("Message failed.", "System Abort");
        };
        if (!bwDoAbort.IsBusy)
            bwDoAbort.RunWorkerAsync();
    }
}