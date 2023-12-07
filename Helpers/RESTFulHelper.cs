using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using VisualHFT.Model;

namespace VisualHFT.Helpers;

internal class CustomDateConverter : JsonConverter
{
    private const string DatePositionFormat = "yyyy'.'MM'.'dd'-'HH'.'mm'.'ss'.'ffffff";

    public override bool CanWrite => false;

    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(DateTime) || objectType == typeof(DateTime?);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        //scenario: "2022.10.19-14.49.58.808384"            -- OK
        //scenario: "2022-10-19T14:40:49.2291586-04:00"     -- NOT WORKING

        var rawDate = (string)reader.Value;
        if (string.IsNullOrEmpty(rawDate))
            return null;
        //Validate that milliseconds are 6 chars long
        if (rawDate.IndexOf(".") > -1)
        {
            var lenghtMilliseconds = rawDate.Split('.')[5].Length;
            if (rawDate.Split('.')[5].Length < 6)
                for (var i = 0; i < 6 - lenghtMilliseconds; i++)
                    rawDate = rawDate + "0";
        }

        DateTime date;

        // First try to parse the date string as is (in case it is correctly formatted)
        if (DateTime.TryParse(rawDate, out date))
            return date;
        if (DateTime.TryParseExact(rawDate, DatePositionFormat, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out date))
            return date;

        // If not, see if the string matches the known bad format. 
        // If so, replace the ':' with '.' and reparse.
        if (rawDate.Length > 19 && rawDate[19] == ':')
        {
            rawDate = rawDate.Substring(0, 19) + '.' + rawDate.Substring(20);
            if (DateTime.TryParse(rawDate, out date)) return date;
        }

        // It's not a date after all, so just return the default value
        if (objectType == typeof(DateTime?))
            return null;

        return DateTime.MinValue;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

public class RESTFulHelper
{
    private const string serverSecurityCode = "admin";
    private static readonly string BaseURL = ConfigurationManager.AppSettings["RestFullConnection"];

    public static async Task<T> GetVariable<T>() where T : new()
    {
        var variableName = "";
        if (typeof(T) == typeof(List<Position>))
            return default;
        if (typeof(T) == typeof(List<Provider>))
            return default;
        //variableName = "ALLPROVIDERS";
        if (typeof(T) == typeof(List<StrategyParametersFirmMMVM>))
            variableName = "STRATEGY_PARAMETERS_FIRMMM";
        else if (typeof(T) == typeof(List<StrategyParametersFirmBBVM>))
            variableName = "STRATEGY_PARAMETERS_FIRMBB";
        else if (typeof(T) == typeof(List<StrategyParametersHFTAcceptorVM>))
            variableName = "STRATEGY_PARAMETERS_HFTACCEPTOR";
        else if (typeof(T) == typeof(List<StrategyParametersBBookVM>))
            variableName = "STRATEGY_PARAMETERS_BBOOK";
        else
            return default;

        var urlBuilder = new UriBuilder(BaseURL);
        var query = HttpUtility.ParseQueryString(urlBuilder.Query);
        query["CODE"] = serverSecurityCode;
        query["VARIABLENAME"] = variableName;
        urlBuilder.Query = query.ToString();

        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(urlBuilder.ToString());


        HttpResponseMessage responseMessage = null;
        try
        {
            responseMessage = await httpClient.GetAsync("");
            if (responseMessage.IsSuccessStatusCode)
                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        Converters = new List<JsonConverter> { new CustomDateConverter() },
                        DateParseHandling = DateParseHandling.None
                    };
                    return JsonConvert.DeserializeObject<T>(responseMessage.Content.ReadAsStringAsync().Result,
                        settings);
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("GET: " + variableName, ex);
                }

            throw new ApplicationException("GET: " + variableName);
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public static async Task<bool> SetVariable<T>(T variable)
    {
        var variableName = "";
        if (typeof(T) == typeof(List<Position>))
            variableName = "POSITIONS";
        else if (typeof(T) == typeof(List<Provider>))
            variableName = "ALLPROVIDERS";
        else if (typeof(T) == typeof(List<StrategyParametersFirmMMVM>))
            variableName = "STRATEGY_PARAMETERS_FIRMMM";
        else if (typeof(T) == typeof(List<StrategyParametersFirmBBVM>))
            variableName = "STRATEGY_PARAMETERS_FIRMBB";
        else if (typeof(T) == typeof(List<StrategyParametersHFTAcceptorVM>))
            variableName = "STRATEGY_PARAMETERS_HFTACCEPTOR";
        else if (typeof(T) == typeof(List<StrategyParametersBBookVM>))
            variableName = "STRATEGY_PARAMETERS_BBOOK";
        else if (typeof(T) == typeof(string))
            variableName = variable.ToString();

        var urlBuilder = new UriBuilder(BaseURL);
        var query = HttpUtility.ParseQueryString(urlBuilder.Query);
        query["CODE"] = serverSecurityCode;
        query["VARIABLENAME"] = variableName;
        urlBuilder.Query = query.ToString();

        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(urlBuilder.ToString());

        var jsonObject = JsonConvert.SerializeObject(variable);
        var content = new StringContent(jsonObject, Encoding.UTF8, "application/json");

        HttpResponseMessage responseMessage = null;
        responseMessage = await httpClient.PostAsync(urlBuilder.ToString(), content);
        return responseMessage.IsSuccessStatusCode;
    }
}