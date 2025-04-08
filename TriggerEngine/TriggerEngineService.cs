using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualHFT.TriggerEngine
{
    /// <summary>
    /// Core service responsible for managing trigger rules, evaluating metric updates in real time,
    /// and executing defined actions when rule conditions are met.
    /// Acts as the central entry point for all plugin metric registrations.
    /// </summary>
    public static class TriggerEngineService
    {
        /// <summary>
        /// Registers a new incoming metric value from any plugin.
        /// This method is called by plugins whenever a tracked metric is updated.
        /// </summary>
        /// <param name="pluginID">Name of the plugin emitting the metric.</param>
        /// <param name="pluginName">Metric identifier.</param>
        /// <param name="value">Numeric value of the metric.</param>
        /// <param name="timestamp">Timestamp of the value.</param>
        public static void RegisterMetric(string pluginID, string pluginName, double value, DateTime timestamp)
        {
            // 1. Store value in memory (e.g., rolling buffer)
            // 2. Find active rules matching this plugin + metric
            // 3. Evaluate each rule
            // 4. If condition is met, execute all associated actions
        }

        public static void AddOrUpdateRule(TriggerRule rule)
        {
            // Upsert rule into internal dictionary or list
        }

        public static void RemoveRule(string name)
        {
            // Delete rule and clean up any state
        }
    }

}
