using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisualHFT.Commons.Interfaces;


namespace VisualHFT.DataRetriever.TestingFramework.Core
{
    public class TestManager
    {
        private readonly List<IDataRetrieverTestable> _marketConnectors;
        private readonly ScenarioBuilder _scenarioBuilder;

        public TestManager()
        {
            _marketConnectors = AssemblyLoader.LoadDataRetrievers();
            _scenarioBuilder = new ScenarioBuilder();
        }

        public void RunTests()
        {
            if (_marketConnectors.Count == 0)
            {
                Console.WriteLine("No exchange translators found.");
                return;
            }

        }
    }


}
