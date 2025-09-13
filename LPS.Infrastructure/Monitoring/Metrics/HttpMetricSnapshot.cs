using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public abstract class HttpMetricSnapshot : IHttpSnapshot
    {
        [JsonIgnore]
        public DateTime TimeStamp { get; protected set; }
        [JsonIgnore]
        public string RoundName { get; protected set; }
        [JsonIgnore]
        public Guid IterationId { get; protected set; }
        [JsonIgnore]
        public string IterationName { get; protected set; }
        [JsonIgnore]
        public string URL { get; protected set; }
        [JsonIgnore]
        public string HttpMethod { get; protected set; }
        [JsonIgnore]
        public string HttpVersion { get; protected set; }

        public abstract LPSMetricType MetricType { get; }
    }
}
