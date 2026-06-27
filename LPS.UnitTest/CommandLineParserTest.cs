using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.LPSRequest.LPSHttpRequest;

namespace LPS.UnitTest
{
    public class CommandLineParserTest
    {
        [Fact]
        public void DomainValidator_FailureRule_WithSkipRatio_IsValid()
        {
            var command = CreateBaseValidCommand();
            command.FailureRules =
            [
                new FailureRule("SkipRatio > 0.1")
            ];

            Validate(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_TerminationRule_WithSkipRatio_IsValid()
        {
            var command = CreateBaseValidCommand();
            command.TerminationRules =
            [
                new TerminationRule("SkipRatio between 0.05 and 0.2", TimeSpan.FromSeconds(30))
            ];

            Validate(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_TerminationRule_WithZeroGracePeriod_IsValid()
        {
            var command = CreateBaseValidCommand();
            command.TerminationRules =
            [
                new TerminationRule("ErrorRate > 0.1", TimeSpan.Zero)
            ];

            Validate(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_TerminationRule_WithNegativeGracePeriod_IsInvalid()
        {
            var command = CreateBaseValidCommand();
            command.TerminationRules =
            [
                new TerminationRule("ErrorRate > 0.1", TimeSpan.FromSeconds(-1))
            ];

            Validate(command);

            Assert.False(command.IsValid);
        }

        [Fact]
        public void DomainValidator_FailureRule_WithSkipRatioAggregation_IsInvalid()
        {
            var command = CreateBaseValidCommand();
            command.FailureRules =
            [
                new FailureRule("SkipRatio.P95 > 0.1")
            ];

            Validate(command);

            Assert.False(command.IsValid);
        }

        [Fact]
        public void DomainValidator_TerminationRule_WithSkippedRequestsCountAndExpression_IsValid()
        {
            var command = CreateBaseValidCommand();
            command.TerminationRules =
            [
                new TerminationRule("SkippedRequestsCount > 25", TimeSpan.Zero, expression: "${Metrics.Main.Load.Throughput.RPS} > 50")
            ];

            Validate(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_FailureRule_WithSkippedRequestsCountAndExpression_IsValid()
        {
            var command = CreateBaseValidCommand();
            command.FailureRules =
            [
                new FailureRule("SkippedRequestsCount >= 10", expression: "${Metrics.Main.Load.Duration.P90ResponseTime} > 250")
            ];

            Validate(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_TerminationRule_WithSkippedRequestsCountAggregation_IsInvalid()
        {
            var command = CreateBaseValidCommand();
            command.TerminationRules =
            [
                new TerminationRule("SkippedRequestsCount.P95 > 10", TimeSpan.Zero)
            ];

            Validate(command);

            Assert.False(command.IsValid);
        }

        [Fact]
        public void DomainValidator_FailureRule_WithInvalidExpression_IsInvalid()
        {
            var command = CreateBaseValidCommand();
            command.FailureRules =
            [
                new FailureRule("ErrorRate > 0.05", expression: "this-is-not-a-valid-expression")
            ];

            Validate(command);

            Assert.False(command.IsValid);
        }

        [Fact]
        public void DomainValidator_HttpRequest_RetryIf_WithValidConfig_IsValid()
        {
            var command = CreateBaseValidRequestCommand();
            command.Retry = new RetryPolicy
            {
                If = "1 = 1",
                MaxRetries = 3,
                BaseDelayInMs = 100,
                MaxDelayInMs = 1000
            };

            ValidateRequest(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_HttpRequest_RetryIf_WithInvalidMaxDelay_IsInvalid()
        {
            var command = CreateBaseValidRequestCommand();
            command.Retry = new RetryPolicy
            {
                If = "1 = 1",
                MaxRetries = 3,
                BaseDelayInMs = 1000,
                MaxDelayInMs = 100
            };

            ValidateRequest(command);

            Assert.False(command.IsValid);
        }

        [Fact]
        public void DomainValidator_HttpRequest_MaxRetriesWithoutRetryIf_IsValid()
        {
            // MaxRetries without RetryIf is now valid - retries just won't happen since there's no condition
            var command = CreateBaseValidRequestCommand();
            command.Retry = new RetryPolicy
            {
                If = string.Empty,
                MaxRetries = 2,
                BaseDelayInMs = 100,
                MaxDelayInMs = 500
            };

            ValidateRequest(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_HttpRequest_MaxRetriesOnly_IsValid()
        {
            var command = CreateBaseValidRequestCommand();
            command.Retry = new RetryPolicy
            {
                If = "${LastResponse.StatusCode} >= 500",
                MaxRetries = 3,
                BaseDelayInMs = null,
                MaxDelayInMs = null
            };

            ValidateRequest(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_HttpRequest_MaxRetriesAndBaseDelayOnly_IsValid()
        {
            var command = CreateBaseValidRequestCommand();
            command.Retry = new RetryPolicy
            {
                If = "${LastResponse.StatusCode} >= 500",
                MaxRetries = 3,
                BaseDelayInMs = 500,
                MaxDelayInMs = null
            };

            ValidateRequest(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_HttpRequest_BaseDelayOnlyWithoutMaxRetries_IsInvalid()
        {
            var command = CreateBaseValidRequestCommand();
            command.Retry = new RetryPolicy
            {
                If = "${LastResponse.StatusCode} >= 500",
                MaxRetries = null,
                BaseDelayInMs = 500,
                MaxDelayInMs = null
            };

            ValidateRequest(command);

            Assert.False(command.IsValid);
        }

        [Fact]
        public void DomainValidator_HttpRequest_AllRetryParameters_IsValid()
        {
            var command = CreateBaseValidRequestCommand();
            command.Retry = new RetryPolicy
            {
                If = "${LastResponse.StatusCode} >= 500",
                MaxRetries = 3,
                BaseDelayInMs = 100,
                MaxDelayInMs = 2000
            };

            ValidateRequest(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_TerminationRule_WithExpressionOnly_IsValid()
        {
            var command = CreateBaseValidCommand();
            command.TerminationRules =
            [
                new TerminationRule(null, TimeSpan.Zero, expression: "${Metrics.RoundName.IterationName.Throughput.RPS} > 50")
            ];

            Validate(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_FailureRule_WithExpressionOnly_IsValid()
        {
            var command = CreateBaseValidCommand();
            command.FailureRules =
            [
                new FailureRule(null, expression: "${Metrics.RoundName.IterationName.Duration.P90ResponseTime} > 250")
            ];

            Validate(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_HttpRequest_StopIf_WithRetryIf_IsValid()
        {
            var command = CreateBaseValidRequestCommand();
            command.Retry = new RetryPolicy
            {
                If = "${LastResponse.StatusCode} >= 500",
                StopIf = "${LastResponse.StatusCode} = 400",
                MaxRetries = 3,
                BaseDelayInMs = 100,
                MaxDelayInMs = 1000
            };

            ValidateRequest(command);

            Assert.True(command.IsValid);
        }

        [Fact]
        public void DomainValidator_HttpRequest_StopIf_SameAsRetryIf_IsInvalid()
        {
            var command = CreateBaseValidRequestCommand();
            command.Retry = new RetryPolicy
            {
                If = "${LastResponse.StatusCode} >= 500",
                StopIf = "${LastResponse.StatusCode} >= 500",
                MaxRetries = 3,
                BaseDelayInMs = 100,
                MaxDelayInMs = 1000
            };

            ValidateRequest(command);

            Assert.False(command.IsValid);
        }

        private static HttpIteration.SetupCommand CreateBaseValidCommand()
        {
            return new HttpIteration.SetupCommand
            {
                Name = "skip-ratio-test",
                StartupDelay = 0,
                MaximizeThroughput = false,
                Mode = IterationMode.R,
                RequestCount = 1,
                FailureRules = new List<FailureRule>(),
                TerminationRules = new List<TerminationRule>()
            };
        }

        private static HttpRequest.SetupCommand CreateBaseValidRequestCommand()
        {
            return new HttpRequest.SetupCommand
            {
                Url = new URL("https://example.com"),
                HttpMethod = "GET",
                HttpVersion = "2.0",
                SaveResponse = false,
                DownloadHtmlEmbeddedResources = false,
                SupportH2C = false,
                HttpHeaders = new Dictionary<string, string>(),
                Retry = new RetryPolicy
                {
                    MaxRetries = 0,
                    BaseDelayInMs = 100,
                    MaxDelayInMs = 5000
                }
            };
        }

        private static void Validate(HttpIteration.SetupCommand command)
        {
            var skipIfEvaluator = new StubSkipIfEvaluator();
            var logger = new StubLogger();
            var operationIdProvider = new StubRuntimeOperationIdProvider();
            var entity = CreateIterationInstance(skipIfEvaluator, logger, operationIdProvider);

            _ = new HttpIteration.Validator(entity, command, skipIfEvaluator, logger, operationIdProvider);
        }

        private static void ValidateRequest(HttpRequest.SetupCommand command)
        {
            var logger = new StubLogger();
            var operationIdProvider = new StubRuntimeOperationIdProvider();
            var entity = CreateRequestInstance(logger, operationIdProvider);

            _ = new HttpRequest.Validator(entity, command, logger, operationIdProvider);
        }

        private static HttpIteration CreateIterationInstance(
            IIfEvaluator skipIfEvaluator,
            ILogger logger,
            IRuntimeOperationIdProvider operationIdProvider)
        {
            var ctor = typeof(HttpIteration).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types:
                [
                    typeof(IIfEvaluator),
                    typeof(ILogger),
                    typeof(IRuntimeOperationIdProvider)
                ],
                modifiers: null);

            Assert.NotNull(ctor);

            var entity = ctor!.Invoke([skipIfEvaluator, logger, operationIdProvider]) as HttpIteration;
            Assert.NotNull(entity);

            return entity!;
        }

        private static HttpRequest CreateRequestInstance(
            ILogger logger,
            IRuntimeOperationIdProvider operationIdProvider)
        {
            var ctor = typeof(HttpRequest).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types:
                [
                    typeof(ILogger),
                    typeof(IRuntimeOperationIdProvider)
                ],
                modifiers: null);

            Assert.NotNull(ctor);

            var entity = ctor!.Invoke([logger, operationIdProvider]) as HttpRequest;
            Assert.NotNull(entity);

            return entity!;
        }

        private sealed class StubSkipIfEvaluator : IIfEvaluator
        {
            public Task<bool> EvaluateAsync(string skipIfExpression, string sessionId, CancellationToken token)
                => Task.FromResult(false);
        }

        private sealed class StubRuntimeOperationIdProvider : IRuntimeOperationIdProvider
        {
            public string OperationId => "unit-test-op";
        }

        private sealed class StubLogger : ILogger
        {
            public void Log(string eventId, string diagnosticMessage, LPSLoggingLevel level, CancellationToken token = default) { }

            public Task LogAsync(string eventId, string diagnosticMessage, LPSLoggingLevel level, CancellationToken token = default)
                => Task.CompletedTask;

            public void Log(string diagnosticMessage, LPSLoggingLevel level, CancellationToken token = default) { }

            public Task LogAsync(string diagnosticMessage, LPSLoggingLevel level, CancellationToken token = default)
                => Task.CompletedTask;

            public void Flush() { }

            public Task FlushAsync() => Task.CompletedTask;
        }
    }
}