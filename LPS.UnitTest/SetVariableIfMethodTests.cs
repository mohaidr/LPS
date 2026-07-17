using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.Nodes;
using LPS.Infrastructure.PlaceHolderService;
using LPS.Infrastructure.PlaceHolderService.Methods;
using LPS.Infrastructure.Skip;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices.VariableHolders;
using Moq;
using Xunit;

namespace LPS.UnitTest
{
    public class SetVariableIfMethodTests
    {
        private const string SessionId = "setif-session";
        private readonly CancellationToken _ct = CancellationToken.None;

        private sealed class Harness
        {
            public SetVariableIfMethod SetIf;
            public PlaceholderResolverService Resolver;
            public VariableManager Variables;
            public SessionManager Sessions;
            public PlaceholderProcessor Processor;

            // Variable-storing methods default to session scope; check session first, then global.
            public async Task<IVariableHolder> GetStoredAsync(string name, CancellationToken token)
                => await Sessions.GetVariableAsync(SessionId, name, token) ?? await Variables.GetAsync(name, token);
        }

        private static Harness BuildHarness()
        {
            var logger = new Mock<ILogger>();
            logger.Setup(x => x.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LPSLoggingLevel>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);
            logger.Setup(x => x.LogAsync(It.IsAny<string>(), It.IsAny<LPSLoggingLevel>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

            var opId = new Mock<IRuntimeOperationIdProvider>();
            opId.Setup(x => x.OperationId).Returns("op-id");

            var node = new Mock<INodeMetadata>();
            node.Setup(x => x.NodeName).Returns("node");
            node.Setup(x => x.NodeIP).Returns("127.0.0.1");

            var variables = new VariableManager(opId.Object, logger.Object);
            var sessions = new SessionManager(opId.Object, logger.Object);

            // Break the resolver <-> evaluator/method cycle with lazies (same as production DI).
            PlaceholderResolverService resolver = null;
            ExpressionEvaluator ifEvaluator = null;
            var lazyResolver = new Lazy<IPlaceholderResolverService>(() => resolver);
            var lazyEvaluator = new Lazy<IExpressionEvaluator>(() => ifEvaluator);

            var paramExtractor = new ParameterExtractorService(lazyResolver, opId.Object, logger.Object);
            var setIf = new SetVariableIfMethod(paramExtractor, logger.Object, opId.Object, variables, lazyResolver, lazyEvaluator, sessions);

            var processor = new PlaceholderProcessor(new IPlaceholderMethod[] { setIf }, sessions, variables, opId.Object, logger.Object);
            resolver = new PlaceholderResolverService(processor, opId.Object, logger.Object);
            ifEvaluator = new ExpressionEvaluator(resolver, node.Object, opId.Object, logger.Object);

            return new Harness { SetIf = setIf, Resolver = resolver, Variables = variables, Sessions = sessions, Processor = processor };
        }

        private async Task PutGlobalNumberAsync(Harness h, string name, int value)
        {
            var holder = await new NumberVariableHolder.VMaintainer(new Mock<ILogger>().Object, Mock.Of<IRuntimeOperationIdProvider>())
                .WithRawValue(value)
                .SetGlobal()
                .UpdateAsync(_ct);
            await h.Variables.PutAsync(name, holder, _ct);
        }

        [Fact]
        public async Task ConditionTrue_StoresValue()
        {
            var h = BuildHarness();

            var result = await h.SetIf.ExecuteAsync("variable=size, condition=10 > 5, value=big", SessionId, _ct);

            Assert.Equal("big", result);
            var stored = await h.GetStoredAsync("size", _ct);
            Assert.Equal("big", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task ConditionFalse_WithDefault_StoresDefault()
        {
            var h = BuildHarness();

            var result = await h.SetIf.ExecuteAsync("variable=size, condition=10 < 5, value=big, default=small", SessionId, _ct);

            Assert.Equal("small", result);
            var stored = await h.GetStoredAsync("size", _ct);
            Assert.Equal("small", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task ConditionFalse_NoDefault_LeavesVariableUntouched()
        {
            var h = BuildHarness();

            var result = await h.SetIf.ExecuteAsync("variable=size, condition=10 < 5, value=big", SessionId, _ct);

            Assert.Equal(string.Empty, result);
            var stored = await h.GetStoredAsync("size", _ct);
            Assert.Null(stored);
        }

        [Fact]
        public async Task Condition_UsesResolvedPlaceholder()
        {
            var h = BuildHarness();
            await PutGlobalNumberAsync(h, "count", 7);

            // The outer resolver substitutes ${count} before the method is dispatched.
            var result = await h.Resolver.ResolvePlaceholdersAsync<string>(
                "${setvariableif(variable=level, condition=${count} > 5, value=high, default=low)}", SessionId, _ct);

            Assert.Equal("high", result);
            var stored = await h.GetStoredAsync("level", _ct);
            Assert.Equal("high", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task AsInt_StoresTypedNumber()
        {
            var h = BuildHarness();

            var result = await h.SetIf.ExecuteAsync("variable=n, condition=1 == 1, value=42, as=int", SessionId, _ct);

            Assert.Equal("42", result);
            var stored = await h.GetStoredAsync("n", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("42", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task AsJson_StoresParsedJson()
        {
            var h = BuildHarness();

            var result = await h.SetIf.ExecuteAsync("variable=obj, condition=true, value={\"a\":1}, as=json", SessionId, _ct);

            Assert.Equal("{\"a\":1}", result);
            var stored = await h.GetStoredAsync("obj", _ct);
            Assert.Equal("{\"a\":1}", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task AsInt_EvaluatesArithmetic()
        {
            var h = BuildHarness();

            await h.SetIf.ExecuteAsync("variable=sum, condition=true, value=2+3, as=int", SessionId, _ct);

            var stored = await h.GetStoredAsync("sum", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("5", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task MissingVariableName_ReturnsEmpty_AndStoresNothing()
        {
            var h = BuildHarness();

            var result = await h.SetIf.ExecuteAsync("condition=1 == 1, value=x", SessionId, _ct);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task MissingCondition_ReturnsEmpty_AndLeavesVariableUntouched()
        {
            var h = BuildHarness();

            var result = await h.SetIf.ExecuteAsync("variable=size, value=x", SessionId, _ct);

            Assert.Equal(string.Empty, result);
            var stored = await h.GetStoredAsync("size", _ct);
            Assert.Null(stored);
        }

        [Fact]
        public async Task SetIfAlias_DispatchesToMethod()
        {
            var h = BuildHarness();

            var result = await h.Processor.ProcessPlaceholderAsync("setif(variable=size, condition=2 > 1, value=big)", SessionId, _ct);

            Assert.Equal("big", result);
            var stored = await h.GetStoredAsync("size", _ct);
            Assert.Equal("big", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task NotTakenBranch_WithNestedMethod_DoesNotFireEagerly()
        {
            // Option A: method args are NOT pre-resolved. A side-effecting method in the NOT-taken branch
            // (here the 'default', because the condition is true) must not execute.
            var h = BuildHarnessWithSet();

            var result = await h.Resolver.ResolvePlaceholdersAsync<string>(
                "${setvariableif(variable=out, condition=1 == 1, value=taken, default=$set(variable=sideEffect, value=fired))}",
                SessionId, _ct);

            Assert.Equal("taken", result);
            Assert.Equal("taken", await (await h.GetStoredAsync("out", _ct)).GetRawValueAsync(_ct));
            // The default's $set was in the untaken branch and must NOT have run.
            Assert.Null(await h.GetStoredAsync("sideEffect", _ct));
        }

        private static Harness BuildHarnessWithSet()
        {
            var logger = new Mock<ILogger>();
            logger.Setup(x => x.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LPSLoggingLevel>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            logger.Setup(x => x.LogAsync(It.IsAny<string>(), It.IsAny<LPSLoggingLevel>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var opId = new Mock<IRuntimeOperationIdProvider>();
            opId.Setup(x => x.OperationId).Returns("op-id");
            var node = new Mock<INodeMetadata>();
            node.Setup(x => x.NodeName).Returns("node");
            node.Setup(x => x.NodeIP).Returns("127.0.0.1");

            var variables = new VariableManager(opId.Object, logger.Object);
            var sessions = new SessionManager(opId.Object, logger.Object);

            PlaceholderResolverService resolver = null;
            ExpressionEvaluator ifEvaluator = null;
            var lazyResolver = new Lazy<IPlaceholderResolverService>(() => resolver);
            var lazyEvaluator = new Lazy<IExpressionEvaluator>(() => ifEvaluator);

            var paramExtractor = new ParameterExtractorService(lazyResolver, opId.Object, logger.Object);
            var setIf = new SetVariableIfMethod(paramExtractor, logger.Object, opId.Object, variables, lazyResolver, lazyEvaluator, sessions);
            var set = new SetVariableMethod(sessions, paramExtractor, logger.Object, opId.Object, variables, lazyResolver);

            var processor = new PlaceholderProcessor(new IPlaceholderMethod[] { setIf, set }, sessions, variables, opId.Object, logger.Object);
            resolver = new PlaceholderResolverService(processor, opId.Object, logger.Object);
            ifEvaluator = new ExpressionEvaluator(resolver, node.Object, opId.Object, logger.Object);

            return new Harness { SetIf = setIf, Resolver = resolver, Variables = variables, Sessions = sessions, Processor = processor };
        }
    }
}
