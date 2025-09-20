using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Exceptions;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.LPSFlow;
using Newtonsoft.Json.Linq;

namespace LPS.Domain
{
    public partial class HttpIteration : Iteration, IBusinessEntity, ICloneable
    {
        private HttpIteration()
        {
            Type = IterationType.Http;
            TerminationRules = [];
        }
        ISkipIfEvaluator _skipIfEvaluator;
        private HttpIteration(
            ISkipIfEvaluator skipIfEvaluator,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            Type = IterationType.Http;
            _skipIfEvaluator = skipIfEvaluator;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }


        public HttpIteration(SetupCommand command,
            ISkipIfEvaluator skipIfEvaluator,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            ArgumentNullException.ThrowIfNull(command);
            Type = IterationType.Http;
            _skipIfEvaluator = skipIfEvaluator;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            this.Setup(command);
        }

        private int _numberOfSuccessfulCalls;
        private int _numberOfFailedCalls;
        public int NumberOfSuccessfulCalls
        {
            get => _numberOfSuccessfulCalls;
            set
            {
                if (this.IsValid)
                {
                    _numberOfSuccessfulCalls = value;
                }
            }
        }
        public int NumberOfFailedCalls
        {
            get => _numberOfFailedCalls;
            set
            {
                if (this.IsValid)
                {
                    _numberOfFailedCalls = value;
                }
            }
        }
        public int StartupDelay { get; private set; }
        public int? RequestCount { get; private set; }
        public int? Duration { get; private set; }
        public int? BatchSize { get; private set; }
        public int? CoolDownTime { get; private set; }
        public IterationMode? Mode { get; private set; }
        public bool MaximizeThroughput { get; private set; }
        public HttpRequest HttpRequest { get; protected set; }
        public FailureCriteria FailureCriteria { get; private set; }
        public ICollection<TerminationRule> TerminationRules { get; private set; }
    }

    public readonly struct FailureCriteria(
    ICollection<HttpStatusCode> errorStatusCodes,
    double? maxErrorRate,
    double? maxP90 = null,
    double? maxP50 = null,
    double? maxP10 = null,
    double? maxAvg = null)
    {
        public ICollection<HttpStatusCode> ErrorStatusCodes { get; } = errorStatusCodes;
        public double? MaxErrorRate { get; } = maxErrorRate;
        public double? MaxP90 { get; } = maxP90;
        public double? MaxP50 { get; } = maxP50;
        public double? MaxP10 { get; } = maxP10;
        public double? MaxAvg { get; } = maxAvg;
    }


    public readonly struct TerminationRule(
        ICollection<HttpStatusCode> errorStatusCodes,
        TimeSpan? gracePeriod, 
        double? maxErrorRate, 
        double? maxP90 = null,
        double? maxP50 = null,
        double? maxP10 = null,
        double? maxAvg = null)
    {
        /// <summary>
        /// The duration for which the threshold must remain above its value
        /// before terminating the test. If it recovers during this period, termination is aborted.
        /// </summary>
        public TimeSpan? GracePeriod { get; } = gracePeriod;

        /// <summary>Represents the status codes to consider as erroneous status codes</summary>
        public ICollection<HttpStatusCode> ErrorStatusCodes { get; } = errorStatusCodes;

        /// <summary>Represents the maximum error rate for a given grace period before the test is terminated</summary>
        public double? MaxErrorRate { get; } = maxErrorRate;

        /// <summary>Terminate if P90 response time stays >= this value during the grace period.</summary>
        public double? MaxP90 { get; } = maxP90;

        /// <summary>Terminate if P50 response time stays >= this value during the grace period.</summary>
        public double? MaxP50 { get; } = maxP50;

        /// <summary>Terminate if P10 response time stays >= this value during the grace period.</summary>
        public double? MaxP10 { get; } = maxP10;

        /// <summary>Terminate if Average response time stays >= this value during the grace period.</summary>
        public double? MaxAvg { get; } = maxAvg;
    }
}
