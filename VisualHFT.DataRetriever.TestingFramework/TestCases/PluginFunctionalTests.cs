using System.Diagnostics;
using VisualHFT.Model;
using VisualHFT.Helpers;
using VisualHFT.DataRetriever.TestingFramework.Core;
using Xunit.Abstractions;
using VisualHFT.PluginManager;
using VisualHFT.Commons.Helpers;

using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace VisualHFT.DataRetriever.TestingFramework.TestCases
{
    [Trait("Category", "Run_Manually")]
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
            var errors = new List<ErrorReporting>();
            var marketConnectors = AssemblyLoader.LoadDataRetrievers();

            foreach (var mktConnector in marketConnectors)
            {
                var connectorName = mktConnector.GetType().Name;
                try
                {
                    _testOutputHelper.WriteLine($"Testing {connectorName}");
                    var dataRetriever = mktConnector as IDataRetriever;
                    var plugin = mktConnector as IPlugin;

                    if (dataRetriever == null || plugin == null)
                        throw new Exception("Plugin does not implement required interfaces.");

                    Assert.Equal(ePluginStatus.LOADED, plugin.Status);
                    await dataRetriever.StartAsync();
                    Assert.Equal(ePluginStatus.STARTED, plugin.Status);
                    await dataRetriever.StopAsync();
                    Assert.Equal(ePluginStatus.STOPPED, plugin.Status);

                    _testOutputHelper.WriteLine($"++ {connectorName} passed.");
                }
                catch (Exception e)
                {
                    errors.Add(new ErrorReporting() { Message = e.Message, PluginName = connectorName, MessageType = ErrorMessageTypes.ERROR });
                }
            }

            PrintCollectedError(errors);
        }

        [Fact]
        public async Task Test_Plugin_HandlingReconnection_Async()
        {
            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            var errors = new List<ErrorReporting>();

            object _lock = new object();
            OrderBook _actualOrderBook = null;
            bool exceptionTriggered = false;
            Stopwatch sp = new Stopwatch(); // to monitor timeout
            const int TIMEOUT_SECONDS_WAITING_FOR_STATE_CHANGE = 60;

            // Setup the subscription only once
            lock (_lock)
            {
                HelperOrderBook.Instance.Reset(); // reset previous subscriptions
                HelperOrderBook.Instance.Subscribe(lob =>
                {
                    if (exceptionTriggered)
                        return;
                    _actualOrderBook = lob;
                    exceptionTriggered = true;
                    throw new Exception("This should trigger reconnection process.");
                });
            }


            foreach (var mktConnector in marketConnectors)
            {
                var connectorName = mktConnector.GetType().Name;
                _testOutputHelper.WriteLine($"TESTING IN {connectorName}");

                var dataRetriever = mktConnector as IDataRetriever;
                var plugin = mktConnector as IPlugin;

                try
                {
                    // Verify initial state and start
                    Assert.Equal(ePluginStatus.LOADED, plugin.Status);
                    await dataRetriever.StartAsync();
                    Assert.Equal(ePluginStatus.STARTED, plugin.Status);

                    // Reset exception flag for this plugin run
                    exceptionTriggered = false;

                    // Wait for reconnection (expect status STOPPED)
                    sp.Start();
                    while (plugin.Status != ePluginStatus.STOPPED)
                    {
                        if (sp.Elapsed.TotalSeconds > TIMEOUT_SECONDS_WAITING_FOR_STATE_CHANGE)
                        {
                            throw new Exception("Timeout while waiting for status STOPPED");
                        }
                        await Task.Delay(10);
                    }
                    sp.Reset();

                    // Wait for reconnection (expect status STARTING)
                    sp.Start();
                    while (plugin.Status != ePluginStatus.STARTING)
                    {
                        if (sp.Elapsed.TotalSeconds > TIMEOUT_SECONDS_WAITING_FOR_STATE_CHANGE)
                        {
                            throw new Exception("Timeout while waiting for status STARTING");
                        }
                        await Task.Delay(100);
                    }
                    sp.Reset();

                    // Wait for reconnection (expect status STARTED)
                    sp.Start();
                    while (plugin.Status != ePluginStatus.STARTED)
                    {
                        if (sp.Elapsed.TotalSeconds > TIMEOUT_SECONDS_WAITING_FOR_STATE_CHANGE)
                        {
                            throw new Exception("Timeout while waiting for status STARTED");
                        }
                        await Task.Delay(100);
                    }
                    sp.Reset();

                    // Stop and dispose the connector
                    sp.Stop();
                    await dataRetriever.StopAsync();
                    dataRetriever.Dispose();
                    _testOutputHelper.WriteLine($"TESTING {connectorName} OK");
                }
                catch (Exception ex)
                {
                    errors.Add(new ErrorReporting() { Message = "EXCEPTION: " + ex.Message, MessageType = ErrorMessageTypes.ERROR, PluginName = connectorName });
                }
            }

            PrintCollectedError(errors);
        }
        [Fact]
        public async Task Test_Plugin_OrderBookIntegrityAndResilience_Async()
        {
            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            var errors = new List<ErrorReporting>();

            object _lock = new object();
            OrderBook _actualOrderBook = null;
            Exception _receivedException = null;
            int currentProviderID = 0;
            const int CHECK_INTERVAL_MS = 100; // delay between checks
            const int TESTING_DURATION_SECONDS = 20; // duration of the test

            // Set up the shared subscription and notification listener
            lock (_lock)
            {
                HelperOrderBook.Instance.Reset();
                HelperOrderBook.Instance.Subscribe(lob =>
                {
                    if (lob.ProviderID != currentProviderID)
                        return;
                    lock (_lock)
                    {
                        _actualOrderBook = lob;
                    }
                });
            }

            HelperNotificationManager.Instance.NotificationAdded += (sender, e) =>
            {
                if (e.Notification.NotificationType == HelprNorificationManagerTypes.ERROR)
                {
                    _receivedException = e.Notification.Exception;
                }
            };
            await Task.Delay(300); // Allow subscription to be set up.


            foreach (var mktConnector in marketConnectors)
            {
                var CONNECTOR_NAME = mktConnector.GetType().Name;
                _testOutputHelper.WriteLine($"TESTING IN {CONNECTOR_NAME}");

                try
                {
                    var dataRetriever = mktConnector as IDataRetriever;
                    var plugin = mktConnector as IPlugin;

                    if (dataRetriever == null || plugin == null)
                        throw new Exception("Does not implement required interfaces.");
                    currentProviderID = plugin.Settings.Provider.ProviderID;
                    _actualOrderBook = null; //reset the order book
                    await dataRetriever.StartAsync();
                    await Task.Delay(5000); // Allow time for data to be received.
                    

                    DateTime startProcess = DateTime.Now;
                    DateTime? startCheckingSpread = null;
                    // Run for up to 10 seconds (or until an exception is received)
                    while (DateTime.Now.Subtract(startProcess).TotalSeconds < TESTING_DURATION_SECONDS && _receivedException == null)
                    {
                        bool isCrossedSpread = false;
                        bool isDepthExceeded = false;
                        lock (_lock)
                        {
                            isCrossedSpread = _actualOrderBook != null && _actualOrderBook.Spread < 0;

                            if (_actualOrderBook != null && _actualOrderBook.MaxDepth > 0)
                            {
                                if (_actualOrderBook.Bids?.Count() > _actualOrderBook.MaxDepth ||
                                    _actualOrderBook.Asks?.Count() > _actualOrderBook.MaxDepth)
                                {
                                    isDepthExceeded = true;
                                }
                            }
                            else if ((_actualOrderBook != null && _actualOrderBook.MaxDepth == 0) && (_actualOrderBook.Bids?.Count() > 30 || _actualOrderBook.Asks?.Count() > 30))
                            {
                                errors.Add(new ErrorReporting() { Message = "Ask/Bid depth is greater than 30 and no Maximum Depth is set. Potential performance hit.", PluginName = CONNECTOR_NAME, MessageType = ErrorMessageTypes.WARNING });
                            }
                        }



                        // Check for crossed spread.
                        if (isCrossedSpread)
                        {
                            if (!startCheckingSpread.HasValue)
                                startCheckingSpread = DateTime.Now;
                            // If crossed spread persists for more than 5 seconds, consider it an error.
                            if (DateTime.Now.Subtract(startCheckingSpread.Value).TotalSeconds > 1)
                            {
                                _testOutputHelper.WriteLine($"FAILED TESTING SPREAD IN {CONNECTOR_NAME} with status={plugin.Status}");
                                throw new Exception("Crossed spread detected. Status=" + plugin.Status);
                            }
                        }
                        else
                        {
                            startCheckingSpread = null;
                        }
                        // Check for depth exceeded.
                        if (isDepthExceeded)
                        {
                            _testOutputHelper.WriteLine($"FAILED TESTING DEPTH IN {CONNECTOR_NAME}");
                            throw new Exception("Order book depth exceeds the maximum allowed depth as per settings.");
                        }

                        await Task.Delay(CHECK_INTERVAL_MS);
                    }

                    await dataRetriever.StopAsync();
                    dataRetriever.Dispose();

                    if (_receivedException != null)
                    {
                        throw _receivedException;
                    }
                    if (_actualOrderBook == null)
                    {
                        throw new Exception("Order book was not received within the expected timeframe.");
                    }

                    _testOutputHelper.WriteLine($"TESTING {CONNECTOR_NAME} OK");
                }
                catch (Exception ex)
                {
                    errors.Add(new ErrorReporting(){ Message = ex.Message, PluginName = CONNECTOR_NAME, MessageType = ErrorMessageTypes.ERROR});
                }
                finally
                {
                    // Reset state for the next plugin.
                    _receivedException = null;
                    _actualOrderBook = null;
                }
            }

            PrintCollectedError(errors);
        }



        private void PrintCollectedError(List<ErrorReporting> errors)
        {
            if (errors.Any())
            {
                string errorReport = "";
                foreach (var pluginName in errors.Select(x => x.PluginName).Distinct())
                {
                    errorReport += pluginName + ":" + Environment.NewLine + "\t" +
                                   string.Join(Environment.NewLine, errors.Where(x => x.PluginName == pluginName)
                                       .Select(x => $"[{x.MessageType.ToString()}] - {x.Message}"))
                                   + Environment.NewLine;
                }

                _testOutputHelper.WriteLine(Environment.NewLine + "Aggregate error report:" + Environment.NewLine + Environment.NewLine + errorReport);
                if (errors.Any(x => x.MessageType == ErrorMessageTypes.ERROR))
                    Assert.Fail(errorReport);
            }

        }

    }
}
