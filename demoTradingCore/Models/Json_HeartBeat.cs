using System.Collections.Generic;
using Newtonsoft.Json;

namespace demoTradingCore.Models
{
    public class Json_HeartBeats : Json_BaseData
    {
        protected string _data;
        protected List<Json_HeartBeat> _dataObj;
        protected JsonSerializerSettings jsonSettings;

        public Json_HeartBeats()
        {
            jsonSettings = new JsonSerializerSettings();
            jsonSettings.DateFormatString = "yyyy.MM.dd-hh.mm.ss.ffffff";
            type = "HeartBeats";
        }

        public string data => _data;

        public List<Json_HeartBeat> dataObj
        {
            get => _dataObj;
            set
            {
                _dataObj = value;
                _data = JsonConvert.SerializeObject(_dataObj, jsonSettings);
            }
        }

        ~Json_HeartBeats()
        {
            jsonSettings = null;
        }
    }

    public class Json_HeartBeat
    {
        public int ProviderID;
        public string ProviderName;
        public int Status;
    }
}