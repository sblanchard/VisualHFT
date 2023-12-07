using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using PropertyChanged;
using VisualHFT.Helpers;

namespace VisualHFT.ViewModel.Model;

[AddINotifyPropertyChangedInterface]
public class Provider : VisualHFT.Model.Provider, INotifyPropertyChanged
{
    public Provider()
    {
    }

    public Provider(VisualHFT.Model.Provider p)
    {
        ProviderID = p.ProviderID;
        ProviderCode = p.ProviderCode;
        ProviderName = p.ProviderName;
        Status = p.Status;
        LastUpdated = p.LastUpdated;
        Plugin = p.Plugin;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public static ObservableCollection<Provider> CreateObservableCollection()
    {
        return new ObservableCollection<Provider>(HelperProvider.Instance.ToList().Select(x => new Provider(x)));
    }

    public void UpdateUI()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Status"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("StatusImage"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Tooltip"));
    }
}