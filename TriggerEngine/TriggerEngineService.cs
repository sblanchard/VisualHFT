using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Linq;
using VisualHFT.Helpers;
using VisualHFT.ViewModel;
using VisualHFT.ViewModels;

namespace VisualHFT.TriggerEngine
{

    public record MetricEvent(string Plugin, string Metric, double Value, DateTime Timestamp);


    /// <summary>
    /// Core service responsible for managing trigger rules, evaluating metric updates in real time,
    /// and executing defined actions when rule conditions are met.
    /// Acts as the central entry point for all plugin metric registrations.
    /// </summary>
    public static class TriggerEngineService
    {
        private static readonly List<TriggerRule> lstRule = new();
        private static readonly object ruleLock = new();

        private static readonly ConcurrentDictionary<string, double> LastMetricValues = new();
        private static readonly ConcurrentDictionary<string, DateTime> ConditionStartTimes = new();
        private static readonly ConcurrentDictionary<string, DateTime> ActionLastFiredTimes = new();

        private static readonly Channel<MetricEvent> MetricChannel = Channel.CreateUnbounded<MetricEvent>();


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

            _ = MetricChannel.Writer.WriteAsync(new MetricEvent(pluginID, pluginName, value, timestamp));
        }

        public static void AddOrUpdateRule(TriggerRule rule)
        {
            lock (ruleLock)
            {
                var existing = lstRule.Find(r => r.Name == rule.Name);
                if (existing != null) lstRule.Remove(existing);
                lstRule.Add(rule);
            }
        }

        public static void RemoveRule(string name)
        {
            lock (ruleLock)
            {
                var rule = lstRule.FirstOrDefault(x => x.Name == name);
                if (rule != null) lstRule.Remove(rule);
            }
        }
        public static List<TriggerRule> GetRules()
        {
            lock (ruleLock)
            {
                return lstRule.ToList();
            }
        }

         public static async Task StartBackgroundWorkerAsync(CancellationToken token)
        {
            while (await MetricChannel.Reader.WaitToReadAsync(token))
            {
                while (MetricChannel.Reader.TryRead(out var metricEvent))
                {
                    ProcessMetric(metricEvent);
                }
            }
        }

        private static void ProcessMetric(MetricEvent e)
        {
            string metricKey = $"{e.Plugin}.{e.Metric}";
            var previous = LastMetricValues.ContainsKey(metricKey) ? LastMetricValues[metricKey] : double.NaN;
            LastMetricValues[metricKey] = e.Value;

            var ruleSnapshot = GetRules();

            foreach (var rule in ruleSnapshot)
            {
                if (!rule.IsEnabled) continue;

                bool allConditionsMet = true;
                for (int i = 0; i < rule.Condition.Count; i++)
                {
                    var condition = rule.Condition[i];
                    if (condition.Plugin != e.Plugin || condition.Metric != e.Metric)
                        continue;

                    string condKey = $"{rule.Name}|{i}|{e.Plugin}.{e.Metric}";
                    bool satisfied = condition.Window != null
                        ? IsConditionSatisfiedWithWindow(condition, e.Value, previous, e.Timestamp, condKey)
                        : EvaluateDirect(condition, e.Value, previous);

                    if (!satisfied)
                    {
                        allConditionsMet = false;
                        break;
                    }
                }

                if (allConditionsMet)
                {
                    for (int i = 0; i < rule.Actions.Count; i++)
                    {
                        var action = rule.Actions[i];
                        string actionKey = $"{rule.Name}|{i}";

                        TimeSpan cooldown = GetCooldownSpan(action.CooldownDuration, action.CooldownUnit);
                        if (ActionLastFiredTimes.TryGetValue(actionKey, out var lastFireTime) && (e.Timestamp - lastFireTime) < cooldown)
                            continue;

                        ActionLastFiredTimes[actionKey] = e.Timestamp;
                        _ = ExecuteActionAsync(action, e.Plugin, e.Metric, e.Value, e.Timestamp);
                    }
                }
            }
        }

        private static bool EvaluateDirect(TriggerCondition condition, double current, double previous)
        {
            return condition.Operator switch
            {
                ConditionOperator.Equals => current == condition.Threshold,
                ConditionOperator.GreaterThan => current > condition.Threshold,
                ConditionOperator.LessThan => current < condition.Threshold,
                ConditionOperator.CrossesAbove => previous < condition.Threshold && current >= condition.Threshold,
                ConditionOperator.CrossesBelow => previous > condition.Threshold && current <= condition.Threshold,
                _ => false
            };
        }

        private static bool IsConditionSatisfiedWithWindow(TriggerCondition condition, double current, double previous, DateTime timestamp, string conditionKey)
        {
            bool isNowTrue = EvaluateDirect(condition, current, previous);
            TimeSpan requiredWindow = GetTimeSpan(condition.Window);

            if (!isNowTrue)
            {
                ConditionStartTimes.TryRemove(conditionKey, out _);
                return false;
            }

            if (!ConditionStartTimes.TryGetValue(conditionKey, out var start))
            {
                ConditionStartTimes[conditionKey] = timestamp;
                return false;
            }

            return (timestamp - start) >= requiredWindow;
        }

        private static TimeSpan GetTimeSpan(TimeWindow window)
        {
            return window.Unit switch
            {
                TimeWindowUnit.Seconds => TimeSpan.FromSeconds(window.Duration),
                TimeWindowUnit.Milliseconds => TimeSpan.FromMilliseconds(window.Duration),
                TimeWindowUnit.Ticks => TimeSpan.FromTicks(window.Duration),
                _ => TimeSpan.Zero
            };
        }

        private static TimeSpan GetCooldownSpan(int duration, TimeWindowUnit unit)
        {
            return unit switch
            {
                TimeWindowUnit.Seconds => TimeSpan.FromSeconds(duration),
                TimeWindowUnit.Minutes => TimeSpan.FromMinutes(duration),
                TimeWindowUnit.Hours => TimeSpan.FromHours(duration),
                TimeWindowUnit.Days => TimeSpan.FromDays(duration),
                _ => TimeSpan.Zero
            };
        }

        private static Task ExecuteActionAsync(TriggerAction action, string plugin, string metric, double value, DateTime timestamp)
        {
            if (action.Type == ActionType.RestApi && action.RestApi != null)
            {
                var body = action.RestApi.BodyTemplate
                    .Replace("{{metric}}", metric)
                    .Replace("{{value}}", value.ToString())
                    .Replace("{{timestamp}}", timestamp.ToString("o"));

                 
            }
            return Task.CompletedTask;
        }

    }

}
