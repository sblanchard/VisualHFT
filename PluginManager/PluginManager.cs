using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using log4net;
using VisualHFT.Commons.Studies;
using VisualHFT.DataRetriever;
using VisualHFT.Helpers;
using VisualHFT.View;

namespace VisualHFT.PluginManager;

public static class PluginManager
{
    private static readonly List<IPlugin> ALL_PLUGINS = new();
    private static readonly object _locker = new();
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    public static List<IPlugin> AllPlugins
    {
        get
        {
            lock (_locker)
            {
                return ALL_PLUGINS;
            }
        }
    }

    public static bool AllPluginsReloaded { get; internal set; }

    public static void LoadPlugins()
    {
        // 1. By default load all dll's in current Folder. 
        var pluginsDirectory =
            AppDomain.CurrentDomain.BaseDirectory; // This gets the directory where your WPF app is running
        lock (_locker)
        {
            LoadPluginsByDirectory(pluginsDirectory);
        }

        // 3. Load Other Plugins in different folders

        // 4. If is Started, then Start

        // 5. If empty or Stopped. Do nothing.
    }

    public static void StartPlugins()
    {
        lock (_locker)
        {
            if (ALL_PLUGINS.Count == 0) return;
            foreach (var plugin in ALL_PLUGINS) StartPlugin(plugin);
        }
    }

    public static void StartPlugin(IPlugin plugin)
    {
        if (plugin != null)
        {
            if (plugin is IDataRetriever dataRetriever)
            {
                //DATA RETRIEVER = WEBSOCKETS
                var processor = new DataProcessor(dataRetriever);
                dataRetriever.StartAsync();
            }
            else if (plugin is IStudy study)
            {
                study.StartAsync();
            }
        }
    }

    public static void StopPlugin(IPlugin plugin)
    {
        if (plugin != null)
            if (plugin is IDataRetriever dataRetriever)
            {
                //DATA RETRIEVER = WEBSOCKETS
                var processor = new DataProcessor(dataRetriever);
                dataRetriever.StopAsync();
            }
    }

    public static void SettingPlugin(IPlugin plugin)
    {
        UserControl _ucSettings = null;
        if (plugin != null)
        {
            var formSettings = new PluginSettings();
            plugin.CloseSettingWindow = () => { formSettings.Close(); };

            _ucSettings = plugin.GetUISettings() as UserControl;
            if (_ucSettings == null)
            {
                plugin.CloseSettingWindow = null;
                formSettings = null;
                return;
            }

            formSettings.MainGrid.Children.Add(_ucSettings);
            formSettings.Title = $"{plugin.Name} Settings";
            formSettings.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            formSettings.Topmost = true;
            formSettings.ShowInTaskbar = false;
            formSettings.ShowDialog();
        }
    }

    public static void UnloadPlugins()
    {
        lock (_locker)
        {
            if (ALL_PLUGINS.Count == 0) return;
            foreach (var plugin in ALL_PLUGINS.OfType<IDisposable>()) plugin.Dispose();
        }
    }


    private static void LoadPluginsByDirectory(string pluginsDirectory)
    {
        foreach (var file in Directory.GetFiles(pluginsDirectory, "*.dll"))
            try
            {
                var assembly = Assembly.LoadFrom(file);
                foreach (var type in assembly.GetExportedTypes())
                    if (!type.IsAbstract && type.GetInterfaces().Contains(typeof(IPlugin)))
                    {
                        var plugin = Activator.CreateInstance(type) as IPlugin;
                        if (string.IsNullOrEmpty(plugin.Name))
                            continue;
                        ALL_PLUGINS.Add(plugin);
                        plugin.OnError += Plugin_OnError;
                        log.Info("Plugins: " + plugin.Name + " loaded OK.");
                    }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                throw new Exception($"Plugin {file} has failed to load. Error: " + ex.Message);
            }
    }

    private static void Plugin_OnError(object? sender, ErrorEventArgs e)
    {
        if (e.IsCritical)
        {
            log.Error(e.PluginName, e.Exception);
            HelperCommon.GLOBAL_DIALOGS["error"](e.Exception.Message, e.PluginName);
        }
        else
        {
            //LOG error
            log.Error(e.PluginName, e.Exception);
        }
    }
}