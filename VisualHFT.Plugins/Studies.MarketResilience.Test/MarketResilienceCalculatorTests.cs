using System;
using System.Threading;
using VisualHFT.Model;
using Studies.MarketResilience.Model;
using VisualHFT.Studies.MarketResilience.Model;
using Xunit;

namespace Studies.MarketResilience.Tests
{
    public class MarketResilienceTests
    {
        private PlugInSettings _settings;
        public MarketResilienceTests()
        {
            _settings = new PlugInSettings()
            {
                MinShockTimeDifference = 500, TradeSizeShockThresholdMultiplier = 3, SpreadShockThresholdMultiplier = 3
            };
        }
        private OrderBook CreateOrderBook(decimal spread, decimal bidPrice, decimal askPrice)
        {
            var ob = new OrderBook();
            
            ob.LoadData(new[] { new BookItem { Price = (double?)askPrice, Size = 100 } },
                        new[] { new BookItem { Price = (double?)bidPrice, Size = 100 } });
            return ob;
        }

        [Fact]
        public void MarketResilienceCalculator_ShouldTrigger_AfterShockAndRecovery()
        {
            var mrCalc = new MarketResilienceCalculator(_settings);

            // Feed historical stable data
            for (int i = 0; i < 30; i++)
            {
                mrCalc.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));
                mrCalc.OnTrade(new Trade { Size = 10, Price = 500.25m, Timestamp = DateTime.Now });
            }

            // Trigger shocks explicitly
            mrCalc.OnTrade(new Trade { Size = 5000, Price = 500, Timestamp = DateTime.Now });
            mrCalc.OnOrderBookUpdate(CreateOrderBook(5m, 495, 500));

            Thread.Sleep(_settings.MinShockTimeDifference.Value - 100); // Simulate time passing to mimic recovery period

            // Recover spread
            mrCalc.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));

            // Verify MR recalculation occurred
            Assert.NotEqual(1m, mrCalc.CurrentMRScore);
            Assert.InRange(mrCalc.CurrentMRScore, 0, 1);
        }
        [Fact]
        public void MarketResilienceCalculator_ShouldNotTrigger_IfTradeAndSpreadAreTooFarApart()
        {
            var mrCalc = new MarketResilienceCalculator(_settings);

            // Feed historical stable data
            for (int i = 0; i < 30; i++)
            {
                mrCalc.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));
                mrCalc.OnTrade(new Trade { Size = 10, Price = 500.25m, Timestamp = DateTime.Now });
            }

            // Trigger shocks explicitly
            mrCalc.OnTrade(new Trade { Size = 5000, Price = 500, Timestamp = DateTime.Now });
            Thread.Sleep(_settings.MinShockTimeDifference.Value + 10); // Simulate time passing to mimic timing out the recovery period
            mrCalc.OnOrderBookUpdate(CreateOrderBook(5m, 495, 500));

            // Recover spread => which should be avoid by MR calculation becuase time has passed
            mrCalc.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));

            // Verify MR recalculation occurred
            Assert.Equal(1m, mrCalc.CurrentMRScore);
        }
        [Fact]
        public void MarketResilienceWithBias_ShouldDetectBearishBias()
        {
            var mrCalcBias = new MarketResilienceWithBias(_settings);

            // Feed stable historical data
            for (int i = 0; i < 30; i++)
            {
                mrCalcBias.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));
                mrCalcBias.OnTrade(new Trade { Size = 10, Price = 500.25m, Timestamp = DateTime.Now });
            }

            // Simulate bearish event (bid side hit)
            mrCalcBias.OnTrade(new Trade { Size = 5000, Price = 499.5m, Timestamp = DateTime.Now });
            mrCalcBias.OnOrderBookUpdate(CreateOrderBook(5m, 495, 500.5m));

            Thread.Sleep(250); // Simulate market recovery delay

            // Bid side does not fully recover
            mrCalcBias.OnOrderBookUpdate(CreateOrderBook(0.5m, 495, 495.5m)); // bid side still lower than initial

            // Verify MR recalculation and bearish bias
            Assert.NotEqual(1m, mrCalcBias.CurrentMRScore);
            Assert.Equal(eMarketBias.Bearish, mrCalcBias.CurrentMarketBias);
        }

        [Fact]
        public void MarketResilienceWithBias_ShouldDetectBullishBias()
        {
            var mrCalcBias = new MarketResilienceWithBias(_settings);

            // Feed stable historical data
            for (int i = 0; i < 30; i++)
            {
                mrCalcBias.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));
                mrCalcBias.OnTrade(new Trade { Size = 10, Price = 500.25m, Timestamp = DateTime.Now });
            }

            // Simulate bullish event (ask side hit)
            mrCalcBias.OnTrade(new Trade { Size = 5000, Price = 501m, Timestamp = DateTime.Now });
            mrCalcBias.OnOrderBookUpdate(CreateOrderBook(5m, 500, 505));

            Thread.Sleep(_settings.MinShockTimeDifference.Value - 100); // Simulate time passing to mimic recovery period

            // Ask side does not fully recover
            mrCalcBias.OnOrderBookUpdate(CreateOrderBook(0.5m, 502.5m, 503)); // ask side still higher than initial

            // Verify MR recalculation and bullish bias
            Assert.NotEqual(1m, mrCalcBias.CurrentMRScore);
            Assert.Equal(eMarketBias.Bullish, mrCalcBias.CurrentMarketBias);
        }

        [Fact]
        public void MarketResilienceWithBias_ShouldDetectNeutralBias_WhenFullyRecovered()
        {
            var mrCalcBias = new MarketResilienceWithBias(_settings);

            // Feed stable historical data
            for (int i = 0; i < 30; i++)
            {
                mrCalcBias.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));
                mrCalcBias.OnTrade(new Trade { Size = 10, Price = 500.25m, Timestamp = DateTime.Now });
            }

            // Simulate bid-side hit event
            mrCalcBias.OnTrade(new Trade { Size = 5000, Price = 499.5m, Timestamp = DateTime.Now });
            mrCalcBias.OnOrderBookUpdate(CreateOrderBook(5m, 495, 500));

            Thread.Sleep(_settings.MinShockTimeDifference.Value - 100); // Simulate time passing to mimic recovery period

            // Fully recover bid side
            mrCalcBias.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m)); // bid side fully recovers

            // Verify MR recalculation and neutral bias
            Assert.NotEqual(1m, mrCalcBias.CurrentMRScore);
            Assert.Equal(eMarketBias.Neutral, mrCalcBias.CurrentMarketBias);
        }
    }
}
