using System.Collections.Generic;
using Newtonsoft.Json;

namespace demoTradingCore.Models
{
    public class Json_Exposure
    {
        public decimal SizeExposed { get; set; }
        public string StrategyName { get; set; }
        public string Symbol { get; set; }
        public decimal UnrealizedPL { get; set; }
    }

    public class JsonExposures : Json_BaseData
    {
        protected string _data;
        protected List<Json_Exposure> _dataObj;
        protected JsonSerializerSettings jsonSettings;

        public JsonExposures()
        {
            jsonSettings = new JsonSerializerSettings();
            jsonSettings.DateFormatString = "yyyy.MM.dd-hh.mm.ss.ffffff";
            type = "Exposures";
        }

        public string data => _data;

        public List<Json_Exposure> dataObj
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