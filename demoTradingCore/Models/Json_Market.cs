using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace demoTradingCore.Models
{
    public class jsonBookItem
    {
        public int DecimalPlaces { get; set; }
        public long EntryID { get; set; }
        public bool IsBid { get; set; }
        public string LayerName { get; set; }
        public DateTime LocalTimeStamp { get; set; }
        public decimal Price { get; set; }
        public int ProviderID { get; set; }
        public DateTime ServerTimeStamp { get; set; }
        public decimal Size { get; set; }
        public string Symbol { get; set; }
    }

    public class jsonMarket
    {
        public List<jsonBookItem> Asks { get; set; }
        public List<jsonBookItem> Bids { get; set; }
        public int DecimalPlaces { get; set; }
        public int ProviderId { get; set; }
        public string ProviderName { get; set; }
        public int ProviderStatus { get; set; }
        public string Symbol { get; set; }
        public int SymbolMultiplier { get; set; }
    }

    public class jsonMarkets : Json_BaseData
    {
        protected string _data;
        protected List<jsonMarket> _dataObj;
        protected JsonSerializerSettings jsonSettings;

        public jsonMarkets()
        {
            jsonSettings = new JsonSerializerSettings();
            jsonSettings.DateFormatString = "yyyy.MM.dd-hh.mm.ss.ffffff";
            type = "Market";
        }

        public string data => _data;

        //get {return Newtonsoft.Json.JsonConvert.SerializeObject(dataObj, jsonSettings); } 
        public List<jsonMarket> dataObj
        {
            get => _dataObj;
            set
            {
                _dataObj = value;
                _data = JsonConvert.SerializeObject(_dataObj, jsonSettings);
            }
        }
    }
}