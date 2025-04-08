using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisualHFT.TriggerEngine.Actions;

namespace VisualHFT.TriggerEngine
{
    /// <summary>
    /// Defines a single action to execute when a rule's condition is met.
    /// The type determines the implementation used (e.g., REST API call).
    /// </summary>
    public class TriggerAction
    {
        public ActionType Type { get; set; } = ActionType.RestApi;
        public RestApiAction? RestApi { get; set; }         // Only required if Type == RestApi
        // Future: Add more actions (e.g., UIAlertAction, PluginCommandAction)
    }
}
