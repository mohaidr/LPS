using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;

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

        private static void Validate(HttpIteration.SetupCommand command)
        {
            var skipIfEvaluator = new StubSkipIfEvaluator();
            var logger = new StubLogger();
            var operationIdProvider = new StubRuntimeOperationIdProvider();
            var entity = CreateIterationInstance(skipIfEvaluator, logger, operationIdProvider);

            _ = new HttpIteration.Validator(entity, command, skipIfEvaluator, logger, operationIdProvider);
        }

        private static HttpIteration CreateIterationInstance(
            ISkipIfEvaluator skipIfEvaluator,
            ILogger logger,
            IRuntimeOperationIdProvider operationIdProvider)
        {
            var ctor = typeof(HttpIteration).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types:
                [
                    typeof(ISkipIfEvaluator),
                    typeof(ILogger),
                    typeof(IRuntimeOperationIdProvider)
                ],
                modifiers: null);

            Assert.NotNull(ctor);

            var entity = ctor!.Invoke([skipIfEvaluator, logger, operationIdProvider]) as HttpIteration;
            Assert.NotNull(entity);

            return entity!;
        }

        private sealed class StubSkipIfEvaluator : ISkipIfEvaluator
        {
            public Task<bool> ShouldSkipAsync(string skipIfExpression, string sessionId, CancellationToken token)
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