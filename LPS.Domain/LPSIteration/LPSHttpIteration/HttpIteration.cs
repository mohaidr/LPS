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
            FailureRules = [];  
        }
        IIfEvaluator _ifEvaluator;
        private HttpIteration(
            IIfEvaluator ifEvaluator,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            Type = IterationType.Http;
            _ifEvaluator = ifEvaluator;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            TerminationRules = [];
            FailureRules = [];  
        }


        public HttpIteration(SetupCommand command,
            IIfEvaluator skipIfEvaluator,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            ArgumentNullException.ThrowIfNull(command);
            Type = IterationType.Http;
            _ifEvaluator = skipIfEvaluator;
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
        
        // Inline operator support for termination rules
        public ICollection<TerminationRule> TerminationRules { get; private set; }
        
        // Inline operator support for failure rules
        public ICollection<FailureRule> FailureRules { get; private set; }
    }

    // Termination rule with inline operator support
    public readonly struct TerminationRule
    {
        public TerminationRule(string metric, TimeSpan gracePeriod, string errorStatusCodes = null, string expression = null)
        {
            Metric = metric;
            GracePeriod = gracePeriod;
            ErrorStatusCodes = errorStatusCodes;
            Expression = expression;
        }

        public string? Metric { get; }
        public TimeSpan GracePeriod { get; }

        /// <summary>
        /// For ErrorRate metrics: defines which HTTP status codes count as errors.
        /// Uses the same operator syntax as StatusCode rules.
        /// Examples: ">= 500", ">= 400", "= 429", "between 500 and 599"
        /// If not specified for ErrorRate, defaults to ">= 400".
        /// </summary>
        public string ErrorStatusCodes { get; }

        /// <summary>
        /// Optional Flee expression condition to terminate.
        /// If this expression evaluates to true, termination is triggered immediately (independent of Metric).
        /// Examples: "${Metrics.Main.Load.Throughput.ErrorRate} > 0.10"
        /// </summary>
        public string Expression { get; }

    }

    // Failure rule with inline operator support
    public readonly struct FailureRule
    {
        public FailureRule(string metric, string errorStatusCodes = null, string expression = null)
        {
            Metric = metric;
            ErrorStatusCodes = errorStatusCodes;
            Expression = expression;
        }

        public string? Metric { get; }

        /// <summary>
        /// For ErrorRate metrics: defines which HTTP status codes count as errors.
        /// Uses the same operator syntax as StatusCode rules.
        /// Examples: ">= 500", ">= 400", "= 401", "between 400 and 599"
        /// If not specified for ErrorRate, defaults to ">= 400" (all client and server errors).
        /// </summary>
        public string ErrorStatusCodes { get; }

        /// <summary>
        /// Optional Flee expression condition.
        /// If this expression evaluates to true, failure is triggered immediately (independent of Metric).
        /// Examples: "${Metrics.Main.Load.Throughput.ErrorRate} > 0.10"
        /// </summary>
        public string Expression { get; }

    }
}
