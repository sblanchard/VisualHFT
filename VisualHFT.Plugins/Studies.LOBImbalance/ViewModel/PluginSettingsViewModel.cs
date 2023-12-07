using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using VisualHFT.Helpers;
using VisualHFT.ViewModel.Model;

namespace VisualHFT.Studies.LOBImbalance.ViewModel;

public class PluginSettingsViewModel : INotifyPropertyChanged, IDataErrorInfo
{
    private readonly Action _actionCloseWindow;
    private AggregationLevel _aggregationLevelSelection;
    private Provider _selectedProvider;
    private int? _selectedProviderID;
    private string _selectedSymbol;
    private string _successMessage;


    private string _validationMessage;

    public PluginSettingsViewModel(Action actionCloseWindow)
    {
        OkCommand = new RelayCommand<object>(ExecuteOkCommand, CanExecuteOkCommand);
        CancelCommand = new RelayCommand<object>(ExecuteCancelCommand);
        _actionCloseWindow = actionCloseWindow;

        Symbols = new ObservableCollection<string>(HelperSymbol.Instance);
        Providers = Provider.CreateObservableCollection();
        OnPropertyChanged(nameof(Providers));
        OnPropertyChanged(nameof(Symbols));

        HelperProvider.Instance.OnDataReceived += PROVIDERS_OnDataReceived;
        HelperSymbol.Instance.OnCollectionChanged += ALLSYMBOLS_CollectionChanged;


        AggregationLevels = new ObservableCollection<Tuple<string, AggregationLevel>>();
        foreach (AggregationLevel level in Enum.GetValues(typeof(AggregationLevel)))
            AggregationLevels.Add(new Tuple<string, AggregationLevel>(HelperCommon.GetEnumDescription(level), level));
        AggregationLevelSelection = AggregationLevel.Automatic;


        LoadSelectedProviderID();
    }

    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }
    public Action UpdateSettingsFromUI { get; set; }


    public ObservableCollection<Provider> Providers { get; set; }

    public ObservableCollection<string> Symbols { get; set; }

    public int? SelectedProviderID
    {
        get => _selectedProviderID;
        set
        {
            _selectedProviderID = value;
            OnPropertyChanged(nameof(SelectedProviderID));
            RaiseCanExecuteChanged();
            LoadSelectedProviderID();
        }
    }

    public Provider SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            _selectedProvider = value;
            OnPropertyChanged(nameof(SelectedProvider));
            RaiseCanExecuteChanged();
            LoadSelectedProviderID();
        }
    }

    public string SelectedSymbol
    {
        get => _selectedSymbol;
        set
        {
            _selectedSymbol = value;
            RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(SelectedSymbol));
        }
    }

    public AggregationLevel AggregationLevelSelection
    {
        get => _aggregationLevelSelection;
        set
        {
            _aggregationLevelSelection = value;
            RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(AggregationLevelSelection));
        }
    }

    public ObservableCollection<Tuple<string, AggregationLevel>> AggregationLevels { get; set; }


    public string ValidationMessage
    {
        get => _validationMessage;
        set
        {
            _validationMessage = value;
            OnPropertyChanged(nameof(ValidationMessage));
        }
    }

    public string SuccessMessage
    {
        get => _successMessage;
        set
        {
            _successMessage = value;
            OnPropertyChanged(nameof(SuccessMessage));
        }
    }

    public string Error => null;

    public string this[string columnName]
    {
        get
        {
            switch (columnName)
            {
                case nameof(SelectedProvider):
                    if (SelectedProvider == null)
                        return "Select the Provider.";
                    break;
                case nameof(SelectedSymbol):
                    if (string.IsNullOrWhiteSpace(SelectedSymbol))
                        return "Select the Symbol.";
                    break;

                default:
                    return null;
            }

            return null;
        }
    }


    public event PropertyChangedEventHandler PropertyChanged;

    private void ExecuteOkCommand(object obj)
    {
        SuccessMessage = "Settings saved successfully!";
        UpdateSettingsFromUI?.Invoke();
        _actionCloseWindow?.Invoke();
    }

    private void ExecuteCancelCommand(object obj)
    {
        _actionCloseWindow?.Invoke();
    }

    private bool CanExecuteOkCommand(object obj)
    {
        // This checks if any validation message exists for any of the properties
        return string.IsNullOrWhiteSpace(this[nameof(SelectedProvider)]) &&
               string.IsNullOrWhiteSpace(this[nameof(SelectedSymbol)]);
    }

    private void RaiseCanExecuteChanged()
    {
        (OkCommand as RelayCommand<object>)?.RaiseCanExecuteChanged();
    }

    private void LoadSelectedProviderID()
    {
        if (_selectedProvider != null)
        {
            _selectedProviderID = _selectedProvider.ProviderID;
            OnPropertyChanged(nameof(SelectedSymbol));
        }
        else if (_selectedProviderID.HasValue && Providers.Any())
        {
            _selectedProvider = Providers.FirstOrDefault(x => x.ProviderID == _selectedProviderID.Value);
            OnPropertyChanged(nameof(SelectedProvider));
        }
    }

    private void ALLSYMBOLS_CollectionChanged(object? sender, EventArgs e)
    {
        Symbols = new ObservableCollection<string>(HelperSymbol.Instance);
        OnPropertyChanged(nameof(Symbols));
    }

    private void PROVIDERS_OnDataReceived(object? sender, VisualHFT.Model.Provider e)
    {
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            var item = new Provider(e);
            if (!Providers.Any(x => x.ProviderCode == e.ProviderCode))
                Providers.Add(item);
            if (_selectedProvider == null &&
                e.Status == eSESSIONSTATUS.BOTH_CONNECTED) //default provider must be the first who's Active
                SelectedProvider = item;
        }));
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}