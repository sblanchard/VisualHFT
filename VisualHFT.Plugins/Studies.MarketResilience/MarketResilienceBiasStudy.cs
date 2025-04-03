using System;
using System.Threading.Tasks;
using Studies.MarketResilience.Model;
using VisualHFT.Commons.Helpers;
using VisualHFT.Commons.Model;
using VisualHFT.Commons.PluginManager;
using VisualHFT.Commons.Pools;
using VisualHFT.Enums;
using VisualHFT.Helpers;
using VisualHFT.Model;
using VisualHFT.PluginManager;
using VisualHFT.Studies.MarketResilience.Model;
using VisualHFT.Studies.MarketResilience.UserControls;
using VisualHFT.Studies.MarketResilience.ViewModel;
using VisualHFT.UserSettings;

namespace VisualHFT.Studies
{

    public class MarketResilienceBiasStudy : BasePluginStudy
    {
        private static class OrderBookSnapshotPool
        {
            // Create a pool for OrderBookSnapshot objects.
            public static readonly CustomObjectPool<OrderBookSnapshot> Instance = new CustomObjectPool<OrderBookSnapshot>(maxPoolSize: 1000);
        }
        private bool _disposed = false; // to track whether the object has been disposed
        private PlugInSettings _settings;
        private MarketResilienceWithBias _mrBiasCalc;
        private HelperCustomQueue<OrderBookSnapshot> _QUEUE;

        // Event declaration
        public override event EventHandler<decimal> OnAlertTriggered;

        public override string Name { get; set; } = "Market Resilience Bias";
        public override string Version { get; set; } = "1.0.0";
        public override string Description { get; set; } = "Market Resilience Bias.";
        public override string Author { get; set; } = "VisualHFT";
        public override ISetting Settings { get => _settings; set => _settings = (PlugInSettings)value; }
        public override Action CloseSettingWindow { get; set; }
        public override string TileTitle { get; set; } = "MRB";
        public override string TileToolTip { get; set; } = "<b>Market Resilience Bias</b> (MRB) is a real-time metric that quantifies the directional tendency of the market following a large trade. <br/> It provides insights into the prevailing sentiment among market participants, enhancing traders' understanding of market dynamics.<br/><br/>" +
                "The <b>MRB</b> score is derived from the behavior of the Limit Order Book (LOB) post-trade:<br/>" +
                "1. <b>Volume Addition Rate:</b> Analyzes the rate at which volume is added to the bid and ask sides of the LOB after a trade.<br/>" +
                "2. <b>Directional Inclination:</b> Determines whether the market is leaning towards a bullish or bearish stance based on the volume addition rate.<br/>" +
                "<br/>" +
                "The <b>MRB</b> score indicates the market's bias, with a value of 1 representing a bullish sentiment (sentiment up) and -1 representing a bearish sentiment (sentiment down). A zero (0) value represent unknown bias.";

        public MarketResilienceBiasStudy()
        {
            _QUEUE = new HelperCustomQueue<OrderBookSnapshot>($"<OrderBookSnapshot>_{this.Name}", QUEUE_onRead, QUEUE_onError);
        }

        ~MarketResilienceBiasStudy()
        {
            Dispose(false);
        }


        public override async Task StartAsync()
        {
            await base.StartAsync();//call the base first
            
            _mrBiasCalc = new MarketResilienceWithBias(_settings);
            _QUEUE.Clear();
            HelperOrderBook.Instance.Subscribe(LIMITORDERBOOK_OnDataReceived);
            HelperTrade.Instance.Subscribe(TRADE_OnDataReceived);



            log.Info($"{this.Name} Plugin has successfully started.");
            Status = ePluginStatus.STARTED;
        }

        public override async Task StopAsync()
        {
            Status = ePluginStatus.STOPPING;
            log.Info($"{this.Name} is stopping.");

            HelperOrderBook.Instance.Unsubscribe(LIMITORDERBOOK_OnDataReceived);
            HelperTrade.Instance.Unsubscribe(TRADE_OnDataReceived);

            await base.StopAsync();
        }



        private void LIMITORDERBOOK_OnDataReceived(OrderBook e)
        {
            /*
             * ***************************************************************************************************
             * TRANSFORM the incoming object (decouple it)
             * DO NOT hold this call back, since other components depends on the speed of this specific call back.
             * DO NOT BLOCK
             * IDEALLY, USE QUEUES TO DECOUPLE
             * ***************************************************************************************************
             */

            if (e == null)
                return;
            if (_settings.Provider.ProviderID != e.ProviderID || _settings.Symbol != e.Symbol)
                return;

            OrderBookSnapshot snapshot = OrderBookSnapshotPool.Instance.Get();
            // Initialize its state based on the master OrderBook.
            snapshot.UpdateFrom(e);
            // Enqueue for processing.
            _QUEUE.Add(snapshot);

        }
        private void TRADE_OnDataReceived(Trade e)
        {
            _mrBiasCalc.OnTrade(e);
            DoCalculationAndSend();
        }
        private void QUEUE_onRead(OrderBookSnapshot e)
        {
            _mrBiasCalc.OnOrderBookUpdate(e);
            DoCalculationAndSend();
            OrderBookSnapshotPool.Instance.Return(e);
        }
        private void QUEUE_onError(Exception ex)
        {
            var _error = $"Unhandled error in the Queue: {ex.Message}";
            log.Error(_error, ex);
            HelperNotificationManager.Instance.AddNotification(this.Name, _error,
                HelprNorificationManagerTypes.ERROR, HelprNorificationManagerCategories.PLUGINS, ex);

            Task.Run(() => HandleRestart(_error, ex));
        }

        protected override void onDataAggregation(BaseStudyModel existing, BaseStudyModel newItem, int counterAggreated)
        {
            //Aggregation: last
            existing.Value = newItem.Value;
            existing.ValueFormatted = newItem.ValueFormatted;
            existing.MarketMidPrice = newItem.MarketMidPrice;

            base.onDataAggregation(existing, newItem, counterAggreated);
        }


        protected void DoCalculationAndSend()
        {
            if (Status != VisualHFT.PluginManager.ePluginStatus.STARTED) return;

            // Trigger any events or updates based on the new T2O ratio
            eMarketBias _valueBias = _mrBiasCalc.CurrentMarketBias;
            string _valueFormatted = "-"; //unknown
            string _valueColor = "White";
            

            _valueFormatted = _valueBias == eMarketBias.Bullish ? "↑" : (_valueBias == eMarketBias.Bearish ? "↓" : "-");
            _valueColor = _valueBias == eMarketBias.Bullish ? "Green" : (_valueBias == eMarketBias.Bearish ? "Red" : "White");

            var newItem = new BaseStudyModel
            {
                Value = _valueBias == eMarketBias.Bullish? 1: (_valueBias == eMarketBias.Bearish? -1: 0),
                ValueFormatted = _valueFormatted,
                ValueColor = _valueColor,
                MarketMidPrice = (decimal)_mrBiasCalc.MidMarketPrice,
                Timestamp = HelperTimeProvider.Now
            };

            AddCalculation(newItem);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    HelperOrderBook.Instance.Unsubscribe(LIMITORDERBOOK_OnDataReceived);
                    HelperTrade.Instance.Unsubscribe(TRADE_OnDataReceived);
                    _QUEUE.Dispose();
                    _mrBiasCalc.Dispose();
                    OrderBookSnapshotPool.Instance.Dispose();
                    base.Dispose();
                }

            }
        }
        protected override void LoadSettings()
        {
            _settings = LoadFromUserSettings<PlugInSettings>();
            if (_settings == null)
            {
                InitializeDefaultSettings();
            }
            if (_settings.Provider == null) //To prevent back compability with older setting formats
            {
                _settings.Provider = new Provider();
            }
        }
        protected override void SaveSettings()
        {
            SaveToUserSettings(_settings);
        }
        protected override void InitializeDefaultSettings()
        {
            _settings = new PlugInSettings()
            {
                Symbol = "",
                Provider = new Provider(),
                AggregationLevel = AggregationLevel.Ms500
            };
            SaveToUserSettings(_settings);
        }
        public override object GetUISettings()
        {
            PluginSettingsView view = new PluginSettingsView();
            PluginSettingsViewModel viewModel = new PluginSettingsViewModel(CloseSettingWindow);
            viewModel.SelectedSymbol = _settings.Symbol;
            viewModel.SelectedProviderID = _settings.Provider.ProviderID;
            viewModel.AggregationLevelSelection = _settings.AggregationLevel;

            viewModel.UpdateSettingsFromUI = () =>
            {
                _settings.Symbol = viewModel.SelectedSymbol;
                _settings.Provider = viewModel.SelectedProvider;
                _settings.AggregationLevel = viewModel.AggregationLevelSelection;

                SaveSettings();

                //run this because it will allow to restart with the new values
                Task.Run(async () => await HandleRestart($"{this.Name} is starting (from reloading settings).", null, true));


            };
            // Display the view, perhaps in a dialog or a new window.
            view.DataContext = viewModel;
            return view;
        }

    }
}
