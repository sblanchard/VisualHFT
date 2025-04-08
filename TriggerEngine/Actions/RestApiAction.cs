using System;
using System.Collections.Generic;
using System.Linq;
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
    }

}
