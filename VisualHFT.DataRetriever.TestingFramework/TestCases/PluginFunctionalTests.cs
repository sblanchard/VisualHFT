using System.Diagnostics;
using VisualHFT.Model;
using VisualHFT.Helpers;
using VisualHFT.DataRetriever.TestingFramework.Core;
using Xunit.Abstractions;
using VisualHFT.PluginManager;
using System.Reflection.Metadata;

namespace VisualHFT.DataRetriever.TestingFramework.TestCases
{
    public class PluginFunctionalTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public PluginFunctionalTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task Test_Plugin_StartStop_Async()
        {
            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors)
            {
                var CONNECTOR_NAME = mktConnector.GetType().Name;
                _testOutputHelper.WriteLine($"TESTING IN {CONNECTOR_NAME}");



                var _dataRetriever = mktConnector as IDataRetriever;
                var _plugin = mktConnector as IPlugin;

                Assert.Equal(ePluginStatus.LOADED, _plugin.Status);
                await _dataRetriever.StartAsync();
                Assert.Equal(ePluginStatus.STARTED, _plugin.Status);
                await _dataRetriever.StopAsync();
                Assert.Equal(ePluginStatus.STOPPED, _plugin.Status);
            }
        }
        [Fact]
        public async Task Test_Plugin_HandlingReconnection_Async()
        {
            OrderBook _actualOrderBook = null;
            bool exceptionTriggered = false;
            Stopwatch sp = new Stopwatch(); //to monitor timeout
            const int TIMEOUT_SECONDS_WAITING_FOR_STATE_CHANGE = 20;
            const int SECONDS_TO_WAIT_BEFORE_START_CHECKING = 5;

            HelperOrderBook.Instance.Subscribe(lob =>
            {
                if (exceptionTriggered)
                    return;
                _actualOrderBook = lob;
                exceptionTriggered = true;
                throw new Exception("This should trigger reconnection process.");
            });

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors)
            {
                var CONNECTOR_NAME = mktConnector.GetType().Name;
                _testOutputHelper.WriteLine($"TESTING IN {CONNECTOR_NAME}");


                var _dataRetriever = mktConnector as IDataRetriever;
                var _plugin = mktConnector as IPlugin;

                Assert.Equal(ePluginStatus.LOADED, _plugin.Status);
                await _dataRetriever.StartAsync();
                Assert.Equal(ePluginStatus.STARTED, _plugin.Status);
                await Task.Delay(SECONDS_TO_WAIT_BEFORE_START_CHECKING); //wait for exception to be thrown



                //wait for reconnection (next expected status STOPPED)
                sp.Start();
                while (_plugin.Status != ePluginStatus.STOPPED)
                {
                    if (sp.Elapsed.TotalSeconds > TIMEOUT_SECONDS_WAITING_FOR_STATE_CHANGE)
                        Assert.Fail($"{CONNECTOR_NAME}: Timeout while waiting for status STOPPED");
                    await Task.Delay(500); //wait for reconnection
                }
                sp.Reset();


                //wait for reconnection (next expected status STARTING)
                sp.Start();
                while (_plugin.Status != ePluginStatus.STARTING)
                {
                    if (sp.Elapsed.TotalSeconds > TIMEOUT_SECONDS_WAITING_FOR_STATE_CHANGE)
                        Assert.Fail($"{CONNECTOR_NAME}: Timeout while waiting for status STARTING");
                    await Task.Delay(500); //wait for reconnection
                }
                sp.Reset();

                //wait for reconnection (next expected status STARTED)
                sp.Start();
                while (_plugin.Status != ePluginStatus.STARTED)
                {
                    if (sp.Elapsed.TotalSeconds > TIMEOUT_SECONDS_WAITING_FOR_STATE_CHANGE)
                        Assert.Fail($"{CONNECTOR_NAME}: Timeout while waiting for status STARTED");
                    await Task.Delay(500); //wait for reconnection
                }
                sp.Reset();
            }
        }

        [Fact]
        public async Task Test_Plugin_CheckForCrossSpreadAfter5secs_Async()
        {
            OrderBook _actualOrderBook = null;
            
            HelperOrderBook.Instance.Subscribe(lob =>
            {
                _actualOrderBook = lob;
            });

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors)
            {
                var CONNECTOR_NAME = mktConnector.GetType().Name;
                _testOutputHelper.WriteLine($"TESTING IN {CONNECTOR_NAME}");


                var _dataRetriever = mktConnector as IDataRetriever;
                await _dataRetriever.StartAsync();
                DateTime startProcess = DateTime.Now;
                DateTime? startCheckingSpread = null;
                while (DateTime.Now.Subtract(startProcess).TotalSeconds < 10)
                {
                    if (_actualOrderBook != null)
                    {
                        if (_actualOrderBook.Spread < 0)
                        {
                            if (!startCheckingSpread.HasValue)
                                startCheckingSpread = DateTime.Now;
                            if (DateTime.Now.Subtract(startCheckingSpread.Value).TotalSeconds > 5)
                            {
                                _testOutputHelper.WriteLine($"FAILED TESTING SPREAD IN {CONNECTOR_NAME}");
                                Assert.Fail("Crossed spread detected.");
                            }
                        }
                        else
                            startCheckingSpread = null;
                    }
                }
            }

            Assert.NotNull(_actualOrderBook);
        }

    }
}
