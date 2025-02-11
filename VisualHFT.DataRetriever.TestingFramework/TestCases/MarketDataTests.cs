using VisualHFT.Model;
using VisualHFT.Commons.Model;
using VisualHFT.Helpers;
using VisualHFT.DataRetriever.TestingFramework.Core;
using VisualHFT.Enums;


namespace VisualHFT.DataRetriever.TestingFramework.TestCases
{
    public class MarketDataTests 
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
        public void Test_MarketDataSnapshot()
        {
            OrderBook _actualOrderBook = null;
            //Subscribe to the OrderBook to Assert
            HelperOrderBook.Instance.Subscribe(lob => {_actualOrderBook = lob;});

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors) //run the same test for each plugin
            {
                //Arrange
                var snapshotModel = CreateInitialSnapshot();
                long _startingSequence = snapshotModel.Sequence;


                //Act
                mktConnector.InjectSnapshot(snapshotModel, _startingSequence);


                //Assert (all must remains equal)
                Assert.NotNull(_actualOrderBook);
                Assert.Equal(snapshotModel.Symbol, _actualOrderBook.Symbol);
                Assert.Equal(snapshotModel.Sequence, _actualOrderBook.Sequence);
                Assert.Equal(snapshotModel.Asks.Count(), _actualOrderBook.Asks.Count());
                for (int i = 0; i < snapshotModel.Asks.Count(); i++)
                {
                    Assert.Equal(snapshotModel.Asks[i].IsBid, _actualOrderBook.Asks[i].IsBid);
                    Assert.Equal(snapshotModel.Asks[i].Price, _actualOrderBook.Asks[i].Price);
                    Assert.Equal(snapshotModel.Asks[i].Size, _actualOrderBook.Asks[i].Size);
                }
                Assert.Equal(snapshotModel.Bids.Count(), _actualOrderBook.Bids.Count());
                for (int i = 0; i < snapshotModel.Bids.Count(); i++)
                {
                    Assert.Equal(snapshotModel.Bids[i].IsBid, _actualOrderBook.Bids[i].IsBid);
                    Assert.Equal(snapshotModel.Bids[i].Price, _actualOrderBook.Bids[i].Price);
                    Assert.Equal(snapshotModel.Bids[i].Size, _actualOrderBook.Bids[i].Size);
                }
            }
        }
        [Fact]
        public void Test_MarketDataDelta_DeleteExistingPrice()
        {
            OrderBook _actualOrderBook = null;
            //Subscribe to the OrderBook to Assert
            HelperOrderBook.Instance.Subscribe(lob => { _actualOrderBook = lob; });

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors)
            {
                //Arrange
                var snapshotModel = CreateInitialSnapshot();
                long _startingSequence = snapshotModel.Sequence;

                var bidDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = snapshotModel.Symbol, IsBid = true, EntryID = "10", Price = 1.00001, MDUpdateAction = eMDUpdateAction.Delete, Sequence = ++_startingSequence  } };
                var askDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = snapshotModel.Symbol, IsBid = false, EntryID = "1", Price = 1.00010, MDUpdateAction = eMDUpdateAction.Delete, Sequence = ++_startingSequence  } };


                //Act
                mktConnector.InjectSnapshot(snapshotModel, snapshotModel.Sequence);
                mktConnector.InjectDeltaModel(bidDeltaModel, askDeltaModel);


                //Assert
                Assert.NotNull(_actualOrderBook);
                Assert.Equal(snapshotModel.Symbol, _actualOrderBook.Symbol);
                Assert.Equal(snapshotModel.Asks.Count() - 1, _actualOrderBook.Asks.Count());
                Assert.Equal(snapshotModel.Bids.Count() - 1, _actualOrderBook.Bids.Count());
                Assert.Null(_actualOrderBook.Asks.FirstOrDefault(x => x.Price == 1.00010));
                Assert.Null(_actualOrderBook.Bids.FirstOrDefault(x => x.Price == 1.00001));
            }
        }
        [Fact]
        public void Test_MarketDataDelta_DeleteNonExistingPrice()
        {
            OrderBook _actualOrderBook = null;
            //Subscribe to the OrderBook to Assert
            HelperOrderBook.Instance.Subscribe(lob => { _actualOrderBook = lob; });

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors)
            {
                //Arrange
                var snapshotModel = CreateInitialSnapshot();
                long _startingSequence = snapshotModel.Sequence;

                var bidDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = snapshotModel.Symbol, IsBid = true, EntryID = "11", Price = 1.00000, MDUpdateAction = eMDUpdateAction.Delete, Sequence = ++_startingSequence } };
                var askDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = snapshotModel.Symbol, IsBid = false, EntryID = "0", Price = 1.00011, MDUpdateAction = eMDUpdateAction.Delete, Sequence = ++_startingSequence } };


                //Act
                mktConnector.InjectSnapshot(snapshotModel, snapshotModel.Sequence);
                mktConnector.InjectDeltaModel(bidDeltaModel, askDeltaModel);


                //Assert (all must remains equal)
                Assert.NotNull(_actualOrderBook);
                Assert.Equal(snapshotModel.Symbol, _actualOrderBook.Symbol);
                Assert.Equal(_startingSequence, _actualOrderBook.Sequence);
                Assert.Equal(snapshotModel.Asks.Count(), _actualOrderBook.Asks.Count());
                for (int i = 0; i < snapshotModel.Asks.Count(); i++)
                {
                    Assert.Equal(snapshotModel.Asks[i].IsBid, _actualOrderBook.Asks[i].IsBid);
                    Assert.Equal(snapshotModel.Asks[i].Price, _actualOrderBook.Asks[i].Price);
                    Assert.Equal(snapshotModel.Asks[i].Size, _actualOrderBook.Asks[i].Size);
                }
                Assert.Equal(snapshotModel.Bids.Count(), _actualOrderBook.Bids.Count());
                for (int i = 0; i < snapshotModel.Bids.Count(); i++)
                {
                    Assert.Equal(snapshotModel.Bids[i].IsBid, _actualOrderBook.Bids[i].IsBid);
                    Assert.Equal(snapshotModel.Bids[i].Price, _actualOrderBook.Bids[i].Price);
                    Assert.Equal(snapshotModel.Bids[i].Size, _actualOrderBook.Bids[i].Size);
                }
            }
        }
        [Fact]
        public void Test_MarketDataDelta_AddAtTopConsideringMaxDepth()
        {
            OrderBook _actualOrderBook = null;
            //Subscribe to the OrderBook to Assert
            HelperOrderBook.Instance.Subscribe(lob => { _actualOrderBook = lob; });

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors)
            {
                //Arrange
                var snapshotModel = CreateInitialSnapshot();
                long _startingSequence = snapshotModel.Sequence;

                var bidDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = snapshotModel.Symbol, IsBid = true, EntryID = "12", Price = 1.000055, Size = 1, MDUpdateAction = eMDUpdateAction.New, Sequence = ++_startingSequence } };
                var askDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = snapshotModel.Symbol, IsBid = false, EntryID = "13", Price = 1.000055, Size = 1, MDUpdateAction = eMDUpdateAction.New, Sequence = ++_startingSequence } };


                //Act
                mktConnector.InjectSnapshot(snapshotModel, snapshotModel.Sequence);
                _actualOrderBook.FilterBidAskByMaxDepth = true; //set to filter by max depth
                mktConnector.InjectDeltaModel(bidDeltaModel, askDeltaModel);


                //Assert
                Assert.NotNull(_actualOrderBook);
                Assert.Equal(snapshotModel.Symbol, _actualOrderBook.Symbol);
                Assert.Equal(snapshotModel.Asks.Count() , _actualOrderBook.Asks.Count());
                Assert.Equal(snapshotModel.Bids.Count() , _actualOrderBook.Bids.Count());

                var bidTOP = _actualOrderBook.GetTOB(true);
                var askTOP = _actualOrderBook.GetTOB(false);
                //Assert top of the bid
                Assert.Equal(bidDeltaModel.First().Price, bidTOP.Price);
                Assert.Equal(bidDeltaModel.First().Size, bidTOP.Size);
                //Assert top of the ask
                Assert.Equal(askDeltaModel.First().Price, askTOP.Price);
                Assert.Equal(askDeltaModel.First().Size, askTOP.Size);
            }
        }
        [Fact]
        public void Test_MarketDataDelta_AddAtBottomConsideringMaxDepth()
        {
            OrderBook _actualOrderBook = null;
            //Subscribe to the OrderBook to Assert
            HelperOrderBook.Instance.Subscribe(lob => { _actualOrderBook = lob; });

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors)
            {
                //Arrange
                var snapshotModel = CreateInitialSnapshot();
                long _startingSequence = snapshotModel.Sequence;

                var bidDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = snapshotModel.Symbol, IsBid = true, EntryID = "12", Price = 1.00000, Size = 1, MDUpdateAction = eMDUpdateAction.New, Sequence = ++_startingSequence } };
                var askDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = snapshotModel.Symbol, IsBid = false, EntryID = "13", Price = 1.00011, Size = 1, MDUpdateAction = eMDUpdateAction.New, Sequence = ++_startingSequence } };


                //Act
                mktConnector.InjectSnapshot(snapshotModel, snapshotModel.Sequence);
                _actualOrderBook.FilterBidAskByMaxDepth = true; //set to filter by max depth
                mktConnector.InjectDeltaModel(bidDeltaModel, askDeltaModel);


                //Assert
                Assert.NotNull(_actualOrderBook);
                Assert.Equal(snapshotModel.Symbol, _actualOrderBook.Symbol);
                Assert.Equal(snapshotModel.Asks.Count(), _actualOrderBook.Asks.Count());
                Assert.Equal(snapshotModel.Bids.Count(), _actualOrderBook.Bids.Count());

                var bidTOP = _actualOrderBook.GetTOB(true);
                var askTOP = _actualOrderBook.GetTOB(false);
                //Assert top of the bid
                Assert.Equal(snapshotModel.Bids.First().Price, bidTOP.Price);
                Assert.Equal(snapshotModel.Bids.First().Size, bidTOP.Size);
                //Assert top of the ask
                Assert.Equal(snapshotModel.Asks.First().Price, askTOP.Price);
                Assert.Equal(snapshotModel.Asks.First().Size, askTOP.Size);
                //items added don't exist
                Assert.Null(_actualOrderBook.Bids.FirstOrDefault(x => x.Price == 1.00000));
                Assert.Null(_actualOrderBook.Asks.FirstOrDefault(x => x.Price == 1.00011));

            }
        }
        [Fact]
        public void Test_MarketDataDelta_AddAtMiddleConsideringMaxDepth()
        {
            OrderBook _actualOrderBook = null;
            //Subscribe to the OrderBook to Assert
            HelperOrderBook.Instance.Subscribe(lob => { _actualOrderBook = lob; });

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors)
            {
                //Arrange
                var snapshotModel = CreateInitialSnapshot();
                long _startingSequence = snapshotModel.Sequence;

                var bidDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = snapshotModel.Symbol, IsBid = true, EntryID = "12", Price = 1.000035, Size = 1, MDUpdateAction = eMDUpdateAction.New, Sequence = ++_startingSequence } };
                var askDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = snapshotModel.Symbol, IsBid = false, EntryID = "13", Price = 1.000085, Size = 1, MDUpdateAction = eMDUpdateAction.New, Sequence = ++_startingSequence } };


                //Act
                mktConnector.InjectSnapshot(snapshotModel, snapshotModel.Sequence);
                _actualOrderBook.FilterBidAskByMaxDepth = true; //set to filter by max depth
                mktConnector.InjectDeltaModel(bidDeltaModel, askDeltaModel);


                //Assert
                Assert.NotNull(_actualOrderBook);
                Assert.Equal(snapshotModel.Symbol, _actualOrderBook.Symbol);
                Assert.Equal(snapshotModel.Asks.Count(), _actualOrderBook.Asks.Count());
                Assert.Equal(snapshotModel.Bids.Count(), _actualOrderBook.Bids.Count());

                var bidTOP = _actualOrderBook.GetTOB(true);
                var askTOP = _actualOrderBook.GetTOB(false);
                //Assert top of the bid
                Assert.Equal(snapshotModel.Bids.First().Price, bidTOP.Price);
                Assert.Equal(snapshotModel.Bids.First().Size, bidTOP.Size);
                //Assert top of the ask
                Assert.Equal(snapshotModel.Asks.First().Price, askTOP.Price);
                Assert.Equal(snapshotModel.Asks.First().Size, askTOP.Size);
                //items added don't exist
                Assert.NotNull(_actualOrderBook.Bids.FirstOrDefault(x => x.Price == 1.000035));
                Assert.NotNull(_actualOrderBook.Asks.FirstOrDefault(x => x.Price == 1.000085));

            }
        }
        [Fact]
        public void Test_MarketDataDelta_Change()
        {
            OrderBook _actualOrderBook = null;
            //Subscribe to the OrderBook to Assert
            HelperOrderBook.Instance.Subscribe(lob => { _actualOrderBook = lob; });

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors)
            {
                //Arrange
                var snapshotModel = CreateInitialSnapshot();
                long _startingSequence = snapshotModel.Sequence;

                var bidDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = snapshotModel.Symbol, IsBid = true, EntryID = "8", Price = 1.00003, Size = 99, MDUpdateAction = eMDUpdateAction.Change, Sequence = ++_startingSequence } };
                var askDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = snapshotModel.Symbol, IsBid = false, EntryID = "3", Price = 1.00008, Size = 99, MDUpdateAction = eMDUpdateAction.Change, Sequence = ++_startingSequence } };


                //Act
                mktConnector.InjectSnapshot(snapshotModel, snapshotModel.Sequence);
                _actualOrderBook.FilterBidAskByMaxDepth = true; //set to filter by max depth
                mktConnector.InjectDeltaModel(bidDeltaModel, askDeltaModel);


                //Assert
                Assert.NotNull(_actualOrderBook);
                Assert.Equal(snapshotModel.Symbol, _actualOrderBook.Symbol);
                Assert.Equal(snapshotModel.Asks.Count(), _actualOrderBook.Asks.Count());
                Assert.Equal(snapshotModel.Bids.Count(), _actualOrderBook.Bids.Count());

                //Assert bid change
                Assert.Equal(bidDeltaModel.First().Price, _actualOrderBook.Bids.First(x => x.Price == 1.00003).Price);
                Assert.Equal(bidDeltaModel.First().Size, _actualOrderBook.Bids.First(x => x.Price == 1.00003).Size);
                //Assert ask change
                Assert.Equal(askDeltaModel.First().Price, _actualOrderBook.Asks.First(x => x.Price == 1.00008).Price);
                Assert.Equal(askDeltaModel.First().Size, _actualOrderBook.Asks.First(x => x.Price == 1.00008).Size);
                //items added don't exist
                Assert.NotNull(_actualOrderBook.Bids.FirstOrDefault(x => x.Price == 1.00003));
                Assert.NotNull(_actualOrderBook.Asks.FirstOrDefault(x => x.Price == 1.00008));

            }

        }
        [Fact]
        public void Test_MarketDataDelta_SequenceLowerThanSnapshotShouldNotBeProcessed()
        {
            OrderBook _actualOrderBook = null;
            //Subscribe to the OrderBook to Assert
            HelperOrderBook.Instance.Subscribe(lob => { _actualOrderBook = lob; });

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors) //run the same test for each plugin
            {
                //Arrange
                var snapshotModel = CreateInitialSnapshot();
                long _startingSequence = 10; //start sequence of snapshot on 10 to see how delta will be applied
                snapshotModel.Sequence = _startingSequence;

                //Act
                mktConnector.InjectSnapshot(snapshotModel, snapshotModel.Sequence); //snapshot created
                mktConnector.InjectDeltaModel(new List<DeltaBookItem>()
                {
                    new DeltaBookItem() { Symbol = snapshotModel.Symbol, EntryID = "6", MDUpdateAction = eMDUpdateAction.Delete, IsBid = true, Price = 1.00005, Sequence = --_startingSequence}
                },new List<DeltaBookItem>()
                {
                    new DeltaBookItem() { Symbol = snapshotModel.Symbol, EntryID = "6", MDUpdateAction = eMDUpdateAction.Delete, IsBid = false, Price = 1.00006, Sequence = --_startingSequence}
                }); //delta with lower sequence should be not computed


                //Assert (no delta should have been processed, both Limit Order Books must remain the same)
                Assert.NotNull(_actualOrderBook);
                Assert.Equal(snapshotModel.Symbol, _actualOrderBook.Symbol);
                Assert.Equal(snapshotModel.Sequence, _actualOrderBook.Sequence); //sequence should be the same (since no update should have been processed)
                Assert.Equal(snapshotModel.Bids.Count(), _actualOrderBook.Bids.Count());
                Assert.Equal(snapshotModel.Asks.Count(), _actualOrderBook.Asks.Count());
            }

        }
        [Fact]
        public void Test_MarketDataDelta_SequenceMustBeUpdated()
        {
            OrderBook _actualOrderBook = null;
            //Subscribe to the OrderBook to Assert
            HelperOrderBook.Instance.Subscribe(lob => { _actualOrderBook = lob; });

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors) //run the same test for each plugin
            {
                //Arrange
                var snapshotModel = CreateInitialSnapshot();
                long _startingSequence = 10; //start sequence of snapshot on 10 to see how delta will be applied
                snapshotModel.Sequence = _startingSequence;

                //Act
                mktConnector.InjectSnapshot(snapshotModel, snapshotModel.Sequence); //snapshot created
                mktConnector.InjectDeltaModel(new List<DeltaBookItem>()
                {
                    new DeltaBookItem() { Symbol = snapshotModel.Symbol, EntryID = "6", MDUpdateAction = eMDUpdateAction.Delete, IsBid = true, Price = 1.00005, Sequence = ++_startingSequence}
                }, new List<DeltaBookItem>()
                {
                    new DeltaBookItem() { Symbol = snapshotModel.Symbol, EntryID = "6", MDUpdateAction = eMDUpdateAction.Delete, IsBid = false, Price = 1.00006, Sequence = ++_startingSequence}
                }); 


                //Assert (no delta should have been processed, both Limit Order Books must remain the same)
                Assert.NotNull(_actualOrderBook);
                Assert.Equal(snapshotModel.Symbol, _actualOrderBook.Symbol);
                Assert.Equal(_startingSequence, _actualOrderBook.Sequence); //sequence should be the same (since no update should have been processed)
                Assert.Equal(snapshotModel.Bids.Count() - 1, _actualOrderBook.Bids.Count());
                Assert.Equal(snapshotModel.Asks.Count() - 1, _actualOrderBook.Asks.Count());
            }

        }
        [Fact]
        public void Test_MarketDataDelta_SequenceGapShouldThrowException()
        {
            OrderBook _actualOrderBook = null;
            //Subscribe to the OrderBook to Assert
            HelperOrderBook.Instance.Subscribe(lob => { _actualOrderBook = lob; });

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors) //run the same test for each plugin
            {
                //Arrange
                var snapshotModel = CreateInitialSnapshot();
                long _startingSequence = 10; //start sequence of snapshot on 10 to see how delta will be applied
                snapshotModel.Sequence = _startingSequence;
                _startingSequence++; // add gap
                
                //Act
                mktConnector.InjectSnapshot(snapshotModel, snapshotModel.Sequence); //snapshot created
                Assert.Throws<Exception>(() => 
                {
                    mktConnector.InjectDeltaModel(new List<DeltaBookItem>()
                    {
                        new DeltaBookItem() { Symbol = snapshotModel.Symbol, EntryID = "6", MDUpdateAction = eMDUpdateAction.Delete, IsBid = true, Price = 1.00005, Sequence = ++_startingSequence}
                    }, new List<DeltaBookItem>()
                    {
                        new DeltaBookItem() { Symbol = snapshotModel.Symbol, EntryID = "6", MDUpdateAction = eMDUpdateAction.Delete, IsBid = false, Price = 1.00006, Sequence = ++_startingSequence}
                    });
                });

                //Assert (no delta should have been processed, both Limit Order Books must remain the same)
                Assert.NotNull(_actualOrderBook);
                Assert.Equal(snapshotModel.Symbol, _actualOrderBook.Symbol);
                Assert.Equal(snapshotModel.Sequence, _actualOrderBook.Sequence); //sequence should be the same (since no update should have been processed)
                Assert.Equal(snapshotModel.Bids.Count(), _actualOrderBook.Bids.Count());
                Assert.Equal(snapshotModel.Asks.Count(), _actualOrderBook.Asks.Count());
            }

        }
        [Fact]
        public void Test_MarketDataDelta_DiffSymbolShouldNotBeProcessed()
        {
            OrderBook _actualOrderBook = null;
            //Subscribe to the OrderBook to Assert
            HelperOrderBook.Instance.Subscribe(lob => { if (_actualOrderBook != null && _actualOrderBook.Symbol != lob.Symbol) return;  _actualOrderBook = lob;} );

            var marketConnectors = AssemblyLoader.LoadDataRetrievers();
            foreach (var mktConnector in marketConnectors)
            {
                //Arrange
                var snapshotModel = CreateInitialSnapshot();
                long _startingSequence = snapshotModel.Sequence;

                var bidDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = "XXX/XXX", IsBid = true, EntryID = "10", Price = 1.00001, MDUpdateAction = eMDUpdateAction.Delete, Sequence = ++_startingSequence } };
                var askDeltaModel = new List<DeltaBookItem>() { new DeltaBookItem() { Symbol = "XXX/XXX", IsBid = false, EntryID = "1", Price = 1.00010, MDUpdateAction = eMDUpdateAction.Delete, Sequence = ++_startingSequence } };


                //Act
                mktConnector.InjectSnapshot(snapshotModel, snapshotModel.Sequence);
                mktConnector.InjectDeltaModel(bidDeltaModel, askDeltaModel);


                //Assert
                Assert.NotNull(_actualOrderBook);
                Assert.Equal(snapshotModel.Symbol, _actualOrderBook.Symbol);
                Assert.Equal(snapshotModel.Asks.Count(), _actualOrderBook.Asks.Count());
                Assert.Equal(snapshotModel.Bids.Count(), _actualOrderBook.Bids.Count());
                Assert.NotNull(_actualOrderBook.Asks.FirstOrDefault(x => x.Price == 1.00010));
                Assert.NotNull(_actualOrderBook.Bids.FirstOrDefault(x => x.Price == 1.00001));
            }
        }


    }


}
