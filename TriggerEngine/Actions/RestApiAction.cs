using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace VisualHFT.TriggerEngine.Actions
{
    /// <summary>
    /// Represents a REST API call that will be executed when a trigger fires.
    /// Supports dynamic payloads using placeholders (e.g. {{metric}}, {{value}}, etc.).
    /// </summary>
    public class RestApiAction
    {
        public string Url { get; set; }                    // Destination API
        public string Method { get; set; } = "POST";       // POST or GET (for now)
        public string BodyTemplate { get; set; }           // JSON payload (e.g. includes {{metric}}, {{value}})
        public Dictionary<string, string> Headers { get; set; } = new();

        public async Task ExecuteAsync(string plugin, string metric, double value, DateTime timestamp)
        {
            try
            {
                using var httpClient = new HttpClient();

                // Apply headers
                foreach (var header in Headers)
                {
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }

                string body = BodyTemplate
                    .Replace("{{plugin}}", plugin)
                    .Replace("{{metric}}", metric)
                    .Replace("{{value}}", value.ToString(CultureInfo.InvariantCulture))
                    .Replace("{{timestamp}}", timestamp.ToString("o"));

                if (Method.ToUpper() == "GET")
                {
                    // Assume body data goes in query string (optional enhancement)
                    string fullUrl = $"{Url}?plugin={plugin}&metric={metric}&value={value}&timestamp={Uri.EscapeDataString(timestamp.ToString("o"))}";
                    var response = await httpClient.GetAsync(fullUrl);
                    Console.WriteLine($"[RestApiAction] GET {response.StatusCode}: {fullUrl}");
                }
                else if (Method.ToUpper() == "POST")
                {
                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(Url, content);
                    Console.WriteLine($"[RestApiAction] POST {response.StatusCode}: {Url}");
                }
                else
                {
                    Console.WriteLine($"[RestApiAction] Unsupported method: {Method}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RestApiAction] Error: {ex.Message}");
            }
        }
    }

}
