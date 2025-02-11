using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisualHFT.Commons.Helpers;
using VisualHFT.Commons.Interfaces;
using VisualHFT.DataRetriever.TestingFramework.Core;
using VisualHFT.DataTradeRetriever;
using VisualHFT.Enums;
using VisualHFT.Helpers;
using VisualHFT.Model;

namespace VisualHFT.DataRetriever.TestingFramework.TestCases
{
    public class PrivateMessageTests
    {
        private OrderBook CreateInitialSnapshot()
        {
            var _symbol = "EUR/USD";
            return new OrderBook(_symbol, 5, 5)
            {
                Asks = new CachedCollection<BookItem>(null)
                {
                    new BookItem() { Price = 1.00010, Size = 100, Symbol = _symbol, EntryID = "1", IsBid = false, },
                    new BookItem() { Price = 1.00009, Size = 100, Symbol = _symbol, EntryID = "2", IsBid = false, },
                    new BookItem() { Price = 1.00008, Size = 100, Symbol = _symbol, EntryID = "3", IsBid = false, },
                    new BookItem() { Price = 1.00007, Size = 100, Symbol = _symbol, EntryID = "4", IsBid = false, },
                    new BookItem() { Price = 1.00006, Size = 100, Symbol = _symbol, EntryID = "5", IsBid = false, },
                },
                Bids = new CachedCollection<BookItem>(null)
                {
                    new BookItem() { Price = 1.00005, Size = 100, Symbol = _symbol, EntryID = "6", IsBid = true, },
                    new BookItem() { Price = 1.00004, Size = 100, Symbol = _symbol, EntryID = "7", IsBid = true, },
                    new BookItem() { Price = 1.00003, Size = 100, Symbol = _symbol, EntryID = "8", IsBid = true, },
                    new BookItem() { Price = 1.00002, Size = 100, Symbol = _symbol, EntryID = "9", IsBid = true, },
                    new BookItem() { Price = 1.00001, Size = 100, Symbol = _symbol, EntryID = "10", IsBid = true, },
                },
                Sequence = 1,
            };
        }


        [Fact]
        public void Test_PrivateMessage_Scenario1()
        {
            /*
             Test Case 1: Place and Cancel a Limit Buy Order Below Market Price
            */


            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors) //run the same test for each plugin
            {
                //Arrange -> This will execute the private message scenario, creating the expected executed orders
                List<VisualHFT.Model.Order> _expectedExecutedOrders = mktConnector.ExecutePrivateMessageScenario(eTestingPrivateMessageScenario.SCENARIO_1);
                var _expectedOrderSent = _expectedExecutedOrders.FirstOrDefault();


                //Act
                var _actualExecutedOrders = HelperPosition.Instance.GetAllPositions().SelectMany(x => x.GetAllOrders(null));
                var _actualOrderSent = _actualExecutedOrders.FirstOrDefault();


                //Assert 
                Assert.NotNull(_actualExecutedOrders);
                Assert.Single(_actualExecutedOrders);
                Assert.NotNull(_expectedOrderSent);
                Assert.NotNull(_actualOrderSent);

                Assert.Equal(_expectedOrderSent.OrderID, _actualOrderSent.OrderID);
                Assert.Equal(_expectedOrderSent.ProviderId, _actualOrderSent.ProviderId);
                Assert.Equal(_expectedOrderSent.Symbol, _actualOrderSent.Symbol);
                Assert.Equal(_expectedOrderSent.Side, _actualOrderSent.Side);
                Assert.Equal(_expectedOrderSent.OrderType, _actualOrderSent.OrderType);
                Assert.Equal(_expectedOrderSent.PricePlaced, _actualOrderSent.PricePlaced);
                Assert.Equal(_expectedOrderSent.Quantity, _actualOrderSent.Quantity);
                Assert.Equal(eORDERSTATUS.CANCELED, _actualOrderSent.Status);

                //PositionManager.GetPosition(_actualOrderSent.Symbol, _actualOrderSent.ProviderId);

            }
        }

    }

}
