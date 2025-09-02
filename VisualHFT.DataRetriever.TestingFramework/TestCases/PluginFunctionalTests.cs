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
    public class PluginFunctionalTests : BasePluginTest
    {
        public PluginFunctionalTests(ITestOutputHelper testOutputHelper) 
            : base(testOutputHelper, TestConfiguration.Default()) // Can be changed to Fast() or Thorough() as needed
        {
            ValidateTestEnvironment();
            LogAvailablePlugins();
        }

        [Fact]
        public async Task Test_Plugin_StartStop_Async()
        {
            await ExecuteTestWithReporting(
                "Plugin Start/Stop Test",
                async (context, config, output) =>
                {
                    output.WriteLine($"Testing {context.PluginName} start/stop cycle");
                    
                    // Verify initial state
                    Assert.Equal(ePluginStatus.LOADED, context.Plugin.Status);
                    
                    // Start the plugin
                    await context.DataRetriever.StartAsync();
                    
                    // Wait for started status
                    var startSuccess = await context.WaitForStatusAsync(ePluginStatus.STARTED, config.StatusChangeTimeout);
                    if (!startSuccess)
                    {
                        throw new TimeoutException($"Plugin did not reach STARTED status within {config.StatusChangeTimeout}. Current status: {context.Plugin.Status}");
                    }
                    
                    Assert.Equal(ePluginStatus.STARTED, context.Plugin.Status);
                    output.WriteLine($"✓ {context.PluginName} started successfully");
                    
                    // Stop the plugin
                    await context.DataRetriever.StopAsync();
                    
                    // Wait for stopped status
                    var stopSuccess = await context.WaitForStatusAsync(ePluginStatus.STOPPED, config.StatusChangeTimeout);
                    if (!stopSuccess)
                    {
                        throw new TimeoutException($"Plugin did not reach STOPPED status within {config.StatusChangeTimeout}. Current status: {context.Plugin.Status}");
                    }
                    
                    Assert.Equal(ePluginStatus.STOPPED, context.Plugin.Status);
                    output.WriteLine($"✓ {context.PluginName} stopped successfully");
                    
                    return true;
                },
                result => result == true
            );
        }

        [Fact]
        public async Task Test_Plugin_HandlingReconnection_Async()
        {
            await ExecuteTestWithReporting(
                "Plugin Reconnection Test",
                async (context, config, output) =>
                {
                    output.WriteLine($"Testing {context.PluginName} reconnection handling");
                    
                    bool exceptionTriggered = false;
                    
                    // Set up subscription to trigger exception for reconnection test
                    Action<OrderBook> exceptionTrigger = (lob) =>
                    {
                        if (exceptionTriggered || lob.ProviderID != context.Plugin.Settings.Provider.ProviderID) 
                            return;
                        
                        exceptionTriggered = true;
                        output.WriteLine($"🔄 Triggering exception for {context.PluginName} to test reconnection");
                        throw new Exception("Test exception to trigger reconnection process");
                    };
                    
                    HelperOrderBook.Instance.Subscribe(exceptionTrigger);
                    
                    try
                    {
                        // Start the plugin
                        Assert.Equal(ePluginStatus.LOADED, context.Plugin.Status);
                        await context.DataRetriever.StartAsync();
                        
                        var startSuccess = await context.WaitForStatusAsync(ePluginStatus.STARTED, config.StatusChangeTimeout);
                        if (!startSuccess)
                        {
                            throw new TimeoutException($"Plugin did not start within {config.StatusChangeTimeout}");
                        }
                        output.WriteLine($"✓ {context.PluginName} initial start successful");
                        
                        // Wait for reconnection sequence: STARTED -> STOPPED -> STARTING -> STARTED
                        output.WriteLine($"⏳ Waiting for reconnection sequence...");
                        
                        var stoppedSuccess = await context.WaitForStatusAsync(ePluginStatus.STOPPED, config.StatusChangeTimeout);
                        if (!stoppedSuccess)
                        {
                            throw new TimeoutException($"Plugin did not reach STOPPED status during reconnection within {config.StatusChangeTimeout}");
                        }
                        output.WriteLine($"✓ {context.PluginName} detected stop during reconnection");
                        
                        var startingSuccess = await context.WaitForStatusAsync(ePluginStatus.STARTING, config.StatusChangeTimeout);
                        if (!startingSuccess)
                        {
                            throw new TimeoutException($"Plugin did not reach STARTING status during reconnection within {config.StatusChangeTimeout}");
                        }
                        output.WriteLine($"✓ {context.PluginName} detected restart attempt");
                        
                        var restartedSuccess = await context.WaitForStatusAsync(ePluginStatus.STARTED, config.StatusChangeTimeout);
                        if (!restartedSuccess)
                        {
                            throw new TimeoutException($"Plugin did not reach STARTED status after reconnection within {config.StatusChangeTimeout}");
                        }
                        output.WriteLine($"✓ {context.PluginName} successfully reconnected");
                        
                        return true;
                    }
                    finally
                    {
                        HelperOrderBook.Instance.Unsubscribe(exceptionTrigger);
                    }
                },
                result => result == true
            );
        }

        [Fact]
        public async Task Test_Plugin_OrderBookIntegrityAndResilience_Async()
        {
            await ExecuteTestWithReporting(
                "OrderBook Integrity Test", 
                async (context, config, output) =>
                {
                    output.WriteLine($"Testing {context.PluginName} order book integrity and resilience");
                    
                    // Start the plugin
                    await context.DataRetriever.StartAsync();
                    
                    var startSuccess = await context.WaitForStatusAsync(ePluginStatus.STARTED, config.StatusChangeTimeout);
                    if (!startSuccess)
                    {
                        throw new TimeoutException($"Plugin did not start within {config.StatusChangeTimeout}");
                    }
                    output.WriteLine($"✓ {context.PluginName} started successfully");
                    
                    // Wait for initial data
                    output.WriteLine($"⏳ Waiting {config.InitialDataDelay} for initial data...");
                    await Task.Delay(config.InitialDataDelay);
                    
                    var dataReceived = await context.WaitForDataAsync(config.DataReceptionTimeout);
                    if (!dataReceived)
                    {
                        throw new Exception($"No order book data received within {config.DataReceptionTimeout}");
                    }
                    output.WriteLine($"✓ {context.PluginName} data reception confirmed");
                    
                    // Monitor data integrity for specified duration
                    output.WriteLine($"🔍 Monitoring data integrity for {config.IntegrityTestDuration}...");
                    var testStartTime = DateTime.Now;
                    var crossedSpreadStartTime = (DateTime?)null;
                    var warnings = new List<string>();
                    var checksPerformed = 0;
                    
                    while (DateTime.Now.Subtract(testStartTime) < config.IntegrityTestDuration && context.LastException == null)
                    {
                        var orderBook = context.LastOrderBook;
                        checksPerformed++;
                        
                        if (orderBook != null)
                        {
                            // Check for crossed spread
                            var isCrossedSpread = orderBook.Spread < 0;
                            if (isCrossedSpread)
                            {
                                crossedSpreadStartTime ??= DateTime.Now;
                                
                                if (DateTime.Now.Subtract(crossedSpreadStartTime.Value) > config.CrossedSpreadTolerance)
                                {
                                    // Debug information about the crossed spread
                                    var bestBid = orderBook.GetTOB(true);
                                    var bestAsk = orderBook.GetTOB(false);
                                    var debugInfo = $"Best Bid: {bestBid?.Price:F6} @ {bestBid?.Size:F6}, Best Ask: {bestAsk?.Price:F6} @ {bestAsk?.Size:F6}";
                                    output.WriteLine($"🚨 DEBUG - Crossed spread details: {debugInfo}");
                                    
                                    throw new Exception($"Crossed spread detected and persisted for more than {config.CrossedSpreadTolerance}. Status: {context.Plugin.Status}, Spread: {orderBook.Spread}. {debugInfo}");
                                }
                            }
                            else
                            {
                                crossedSpreadStartTime = null;
                            }
                            
                            // Check depth limits
                            if (orderBook.MaxDepth > 0)
                            {
                                var bidCount = orderBook.Bids?.Count() ?? 0;
                                var askCount = orderBook.Asks?.Count() ?? 0;
                                
                                if (bidCount > orderBook.MaxDepth || askCount > orderBook.MaxDepth)
                                {
                                    throw new Exception($"Order book depth exceeds maximum allowed depth. Bids: {bidCount}, Asks: {askCount}, MaxDepth: {orderBook.MaxDepth}");
                                }
                            }
                            else 
                            {
                                var bidCount = orderBook.Bids?.Count() ?? 0;
                                var askCount = orderBook.Asks?.Count() ?? 0;
                                
                                if (bidCount > config.DepthWarningThreshold || askCount > config.DepthWarningThreshold)
                                {
                                    var warning = $"Depth exceeds threshold - Bids: {bidCount}, Asks: {askCount} (threshold: {config.DepthWarningThreshold})";
                                    if (!warnings.Contains(warning))
                                    {
                                        warnings.Add(warning);
                                        output.WriteLine($"⚠️  {warning}");
                                    }
                                }
                            }
                        }
                        
                        await Task.Delay(config.IntegrityCheckInterval);
                    }
                    
                    if (context.LastException != null)
                    {
                        throw context.LastException;
                    }
                    
                    if (context.LastOrderBook == null)
                    {
                        throw new Exception("No order book data was maintained during the test period");
                    }
                    
                    output.WriteLine($"✓ {context.PluginName} integrity test completed");
                    output.WriteLine($"  📊 Checks performed: {checksPerformed}");
                    output.WriteLine($"  📈 Final spread: {context.LastOrderBook.Spread:F6}");
                    output.WriteLine($"  📚 Final depth - Bids: {context.LastOrderBook.Bids?.Count() ?? 0}, Asks: {context.LastOrderBook.Asks?.Count() ?? 0}");
                    
                    if (warnings.Any())
                    {
                        output.WriteLine($"  ⚠️  Warnings: {warnings.Count}");
                    }
                    
                    return new TestResult { Success = true, Warnings = warnings };
                },
                result => result.Success
            );
        }

        // Helper class for test results
        private class TestResult
        {
            public bool Success { get; set; }
            public List<string> Warnings { get; set; } = new List<string>();
        }
    }
}
