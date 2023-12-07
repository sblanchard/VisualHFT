using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using VisualHFT.PluginManager;

namespace VisualHFT.ViewModel;

public class vmPluginManager : BindableBase, IDisposable
{
    private ObservableCollection<IPlugin> _plugins;

    public vmPluginManager()
    {
        _plugins = new ObservableCollection<IPlugin>(PluginManager.PluginManager.AllPlugins);
        RaisePropertyChanged(nameof(Plugins));

        StartPluginCommand = new DelegateCommand<IPlugin>(StartPlugin, CanStartPlugin);
        StopPluginCommand = new DelegateCommand<IPlugin>(StopPlugin, CanStopPlugin);
        ConfigurePluginCommand = new DelegateCommand<IPlugin>(ConfigurePlugin);
    }

    public ObservableCollection<IPlugin> Plugins
    {
        get => _plugins;
        set => SetProperty(ref _plugins, value);
    }

    public ICommand StartPluginCommand { get; }
    public ICommand StopPluginCommand { get; }
    public ICommand ConfigurePluginCommand { get; }

    public void Dispose()
    {
        // Any cleanup logic if needed
    }

    private bool CanStartPlugin(IPlugin plugin)
    {
        return plugin.Status != ePluginStatus.STARTED;
    }

    private bool CanStopPlugin(IPlugin plugin)
    {
        return plugin.Status != ePluginStatus.STOPPED;
    }

    private void StartPlugin(IPlugin plugin)
    {
        PluginManager.PluginManager.StartPlugin(plugin);
        _plugins = new ObservableCollection<IPlugin>(PluginManager.PluginManager.AllPlugins);
        // Notify of any property changes if needed
        RaisePropertyChanged(nameof(Plugins));
        // Refresh the CanExecute status
        (StartPluginCommand as DelegateCommand<IPlugin>).RaiseCanExecuteChanged();
        (StopPluginCommand as DelegateCommand<IPlugin>).RaiseCanExecuteChanged();
    }

    private void StopPlugin(IPlugin plugin)
    {
        PluginManager.PluginManager.StopPlugin(plugin);
        _plugins = new ObservableCollection<IPlugin>(PluginManager.PluginManager.AllPlugins);
        // Notify of any property changes if needed
        RaisePropertyChanged(nameof(Plugins));
        // Refresh the CanExecute status
        (StartPluginCommand as DelegateCommand<IPlugin>).RaiseCanExecuteChanged();
        (StopPluginCommand as DelegateCommand<IPlugin>).RaiseCanExecuteChanged();
    }

    private void ConfigurePlugin(IPlugin plugin)
    {
        PluginManager.PluginManager.SettingPlugin(plugin);
    }
}