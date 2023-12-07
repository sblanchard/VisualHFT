﻿using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using log4net.Config;

namespace VisualHFT;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        //Initialize logging
        XmlConfigurator.Configure(new FileInfo("log4net.config"));

        //Launch the GC cleanup thread
        Task.Run(async () => { await GCCleanupAsync(); });

        //Load Plugins
        Task.Run(async () => { await LoadPlugins(); });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        PluginManager.PluginManager.UnloadPlugins();
        base.OnExit(e);
    }

    private async Task LoadPlugins()
    {
        PluginManager.PluginManager.AllPluginsReloaded = false;
        PluginManager.PluginManager.LoadPlugins();
        PluginManager.PluginManager.StartPlugins();
        PluginManager.PluginManager.AllPluginsReloaded = true;
    }

    private async Task GCCleanupAsync()
    {
        //due to the high volume of data do this periodically.(this will get fired every 5 secs)

        while (true)
        {
            await Task.Delay(5000);
            GC.Collect(); //force garbage collection
        }

        ;
    }
}