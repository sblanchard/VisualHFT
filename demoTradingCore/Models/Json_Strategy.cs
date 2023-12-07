using System.Collections.Generic;
using Newtonsoft.Json;

namespace demoTradingCore.Models
{
    public class Json_Strategy
    {
        public string StrategyCode { get; set; }
    }


    public class jsonStrategies : Json_BaseData
    {
        protected string _data;
        protected List<Json_Strategy> _dataObj;
        protected JsonSerializerSettings jsonSettings;

        public jsonStrategies()
        {
            jsonSettings = new JsonSerializerSettings();
            jsonSettings.DateFormatString = "yyyy.MM.dd-hh.mm.ss.ffffff";
            type = "Strategies";
        }

        public string data => _data;

        public List<Json_Strategy> dataObj
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