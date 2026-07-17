using System;
using System.IO;
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
    public class ReadIfMethodTests
    {
        private const string SessionId = "readif-session";
        private readonly CancellationToken _ct = CancellationToken.None;

        private sealed class Harness
        {
            public ReadIfMethod ReadIf;
            public PlaceholderResolverService Resolver;
            public VariableManager Variables;
            public SessionManager Sessions;

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

            PlaceholderResolverService resolver = null;
            ExpressionEvaluator ifEvaluator = null;
            var lazyResolver = new Lazy<IPlaceholderResolverService>(() => resolver);
            var lazyEvaluator = new Lazy<IExpressionEvaluator>(() => ifEvaluator);

            var paramExtractor = new ParameterExtractorService(lazyResolver, opId.Object, logger.Object);
            var readIf = new ReadIfMethod(sessions, paramExtractor, logger.Object, opId.Object, variables, lazyResolver, lazyEvaluator);

            var processor = new PlaceholderProcessor(new IPlaceholderMethod[] { readIf }, sessions, variables, opId.Object, logger.Object);
            resolver = new PlaceholderResolverService(processor, opId.Object, logger.Object);
            ifEvaluator = new ExpressionEvaluator(resolver, node.Object, opId.Object, logger.Object);

            return new Harness { ReadIf = readIf, Resolver = resolver, Variables = variables, Sessions = sessions };
        }

        private async Task PutGlobalStringAsync(Harness h, string name, string value)
        {
            var holder = await new StringVariableHolder.VMaintainer(h.Resolver, new Mock<ILogger>().Object, Mock.Of<IRuntimeOperationIdProvider>())
                .WithType(VariableType.String)
                .WithRawValue(value)
                .SetGlobal()
                .UpdateAsync(_ct);
            await h.Variables.PutAsync(name, holder, _ct);
        }

        [Fact]
        public async Task ConditionTrue_ReadsFromVariable_AndStores()
        {
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "src", "hello");

            var result = await h.ReadIf.ExecuteAsync("condition=1 == 1, source=variable, name=src, variable=out", SessionId, _ct);

            Assert.Equal("hello", result);
            var stored = await h.GetStoredAsync("out", _ct);
            Assert.Equal("hello", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task ConditionFalse_NoElse_DoesNotRead_LeavesVariableUntouched()
        {
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "src", "hello");

            var result = await h.ReadIf.ExecuteAsync("condition=1 == 2, source=variable, name=src, variable=out", SessionId, _ct);

            Assert.Equal(string.Empty, result);
            Assert.Null(await h.GetStoredAsync("out", _ct));
        }

        [Fact]
        public async Task ConditionFalse_WithDefault_StoresDefault()
        {
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "src", "hello");

            var result = await h.ReadIf.ExecuteAsync("condition=1 == 2, source=variable, name=src, variable=out, default=fallback", SessionId, _ct);

            Assert.Equal("fallback", result);
            var stored = await h.GetStoredAsync("out", _ct);
            Assert.Equal("fallback", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task ConditionTrue_ReadsFromFile_AndStores()
        {
            var h = BuildHarness();
            var path = Path.Combine(Path.GetTempPath(), $"readif-{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(path, "file-content", _ct);

            try
            {
                var result = await h.ReadIf.ExecuteAsync($"condition=true, source=file, path={path}, variable=out", SessionId, _ct);

                Assert.Equal("file-content", result);
                var stored = await h.GetStoredAsync("out", _ct);
                Assert.Equal("file-content", await stored.GetRawValueAsync(_ct));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task Condition_UsesResolvedPlaceholder()
        {
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "flag", "yes");
            await PutGlobalStringAsync(h, "src", "read-me");

            var result = await h.Resolver.ResolvePlaceholdersAsync<string>(
                "${readif(condition=\"${flag}\" == \"yes\", source=variable, name=src, variable=out)}", SessionId, _ct);

            Assert.Equal("read-me", result);
            var stored = await h.GetStoredAsync("out", _ct);
            Assert.Equal("read-me", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task MissingCondition_ReturnsEmpty_AndLeavesVariableUntouched()
        {
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "src", "hello");

            var result = await h.ReadIf.ExecuteAsync("source=variable, name=src, variable=out", SessionId, _ct);

            Assert.Equal(string.Empty, result);
            Assert.Null(await h.GetStoredAsync("out", _ct));
        }
    }
}
