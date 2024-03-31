using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;

namespace LPS.Infrastructure.Monitoring.EventListeners
{
    internal class LPSResourceEventListener : EventListener
    {
        private double _memoryUsageMB;
        private double _cpuTime;

        public LPSResourceEventListener()
        {
        }

        public double MemoryUsageMB { get { return _memoryUsageMB; } }
        public double CPUPercentage { get { return _cpuTime; } }
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (!source.Name.Equals("System.Runtime"))
            {
                return;
            }

            EnableEvents(source, EventLevel.Verbose, EventKeywords.All, new Dictionary<string, string>() { ["EventCounterIntervalSec"] = "1" });
        }
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.Payload == null)
            {
                return;
            }

            if (!eventData.EventName.Equals("EventCounters"))
            {
                return;
            }

            for (int i = 0; i < eventData.Payload.Count; ++i)
            {
                if (eventData.Payload[i] is IDictionary<string, object> eventPayload)
                {
                    var (counterName, counterValue) = GetRelevantMetric(eventPayload);
                    switch (counterName)
                    {
                        case "working-set":
                            double.TryParse(counterValue, out _memoryUsageMB);
                            break;
                        case "cpu-usage":
                            double.TryParse(counterValue, out _cpuTime);
                            break;
                    }
                }
            }
        }

        private static (string counterName, string counterValue) GetRelevantMetric(
        IDictionary<string, object> eventPayload)
        {
            var counterName = "";
            var counterValue = "";

            if (eventPayload.TryGetValue("Name", out object displayValue))
            {
                counterName = displayValue.ToString();
            }
            if (eventPayload.TryGetValue("Mean", out object value) ||
                eventPayload.TryGetValue("Increment", out value))
            {
                counterValue = value.ToString();
            }
            return (counterName, counterValue);
        }
    }
}
