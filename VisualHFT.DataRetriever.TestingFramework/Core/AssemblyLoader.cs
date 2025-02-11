using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VisualHFT.Commons.Helpers;
using VisualHFT.Commons.Interfaces;
using VisualHFT.PluginManager;


namespace VisualHFT.DataRetriever.TestingFramework.Core
{
    public class AssemblyLoader
    {
        public static List<IDataRetrieverTestable> LoadDataRetrievers()
        {
            List<IDataRetrieverTestable> translators = new List<IDataRetrieverTestable>();


            foreach (var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll"))
            {
                var assembly = Assembly.LoadFrom(file);
                foreach (var type in assembly.GetExportedTypes())
                {
                    if (!type.IsAbstract && type.GetInterfaces().Contains(typeof(IDataRetrieverTestable)))
                    {
                        var plugin = Activator.CreateInstance(type) as IDataRetrieverTestable;
                        translators.Add(plugin);
                    }
                }
            }

            return translators;
        }
    }
}
