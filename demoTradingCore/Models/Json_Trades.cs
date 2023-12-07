using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace demoTradingCore.Models
{
    public class jsonTrade
    {
        public int ProviderId { get; set; }
        public string ProviderName { get; set; }
        public string Symbol { get; set; }
        public decimal Price { get; set; } = decimal.Zero;
        public decimal Size { get; set; } = decimal.Zero;
        public DateTime Timestamp { get; set; }
        public bool IsBuy { get; set; }
        public string Flags { get; set; }
    }

    public class jsonTrades : Json_BaseData
    {
        protected string _data;
        protected List<jsonTrade> _dataObj;
        protected JsonSerializerSettings jsonSettings;

        public jsonTrades()
        {
            jsonSettings = new JsonSerializerSettings();
            jsonSettings.DateFormatString = "yyyy.MM.dd-hh.mm.ss.ffffff";
            type = "Trades";
        }

        public string data => _data;

        //get {return Newtonsoft.Json.JsonConvert.SerializeObject(dataObj, jsonSettings); } 
        public List<jsonTrade> dataObj
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