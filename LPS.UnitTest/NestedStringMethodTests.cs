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
    public class NestedStringMethodTests
    {
        private const string SessionId = "nested-string-session";
        private readonly CancellationToken _ct = CancellationToken.None;

        private sealed class Harness
        {
            public PlaceholderResolverService Resolver;
            public VariableManager Variables;
        }

        private static Harness BuildHarness()
        {
            var logger = new Mock<ILogger>();
            logger.Setup(x => x.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LPSLoggingLevel>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            logger.Setup(x => x.LogAsync(It.IsAny<string>(), It.IsAny<LPSLoggingLevel>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var opId = new Mock<IRuntimeOperationIdProvider>();
            opId.Setup(x => x.OperationId).Returns("op-id");

            var variables = new VariableManager(opId.Object, logger.Object);
            var sessions = new SessionManager(opId.Object, logger.Object);

            PlaceholderResolverService resolver = null;
            var lazyResolver = new Lazy<IPlaceholderResolverService>(() => resolver);
            var pe = new ParameterExtractorService(lazyResolver, opId.Object, logger.Object);

            var methods = new IPlaceholderMethod[]
            {
                new EndsWithMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new StartsWithMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new ContainsMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new ToLowerCaseMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
            };

            var processor = new PlaceholderProcessor(methods, sessions, variables, opId.Object, logger.Object);
            resolver = new PlaceholderResolverService(processor, opId.Object, logger.Object);

            return new Harness { Resolver = resolver, Variables = variables };
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
        public async Task EndsWith_WithNestedToLowerCaseSource_MatchesCaseInsensitively()
        {
            // Replacement for: $endswith(source=..., value=.BIZ, ignoreCase=true)
            // Lowercase the source with a nested $tolowercase, then compare (case-sensitively) to ".biz".
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "email", "TEST@EXAMPLE.BIZ");

            var result = await h.Resolver.ResolvePlaceholdersAsync<string>(
                "${endswith(source=$tolowercase(source=$email), value=.biz)}", SessionId, _ct);

            Assert.Equal("true", result);
        }

        [Fact]
        public async Task EndsWith_WithNestedToLowerCaseSource_NonMatchReturnsFalse()
        {
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "email", "TEST@EXAMPLE.COM");

            var result = await h.Resolver.ResolvePlaceholdersAsync<string>(
                "${endswith(source=$tolowercase(source=$email), value=.biz)}", SessionId, _ct);

            Assert.Equal("false", result);
        }

        [Fact]
        public async Task Contains_ValueResolvingToDollarText_IsNotMangled()
        {
            // A resolved value containing a literal '$' must be matched verbatim (single resolution pass).
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "price", "cost is $5 today");
            await PutGlobalStringAsync(h, "needle", "$5");

            var result = await h.Resolver.ResolvePlaceholdersAsync<string>(
                "${contains(source=$price, value=$needle)}", SessionId, _ct);

            Assert.Equal("true", result);
        }

        [Fact]
        public async Task StartsWith_MatchesResolvedPrefix()
        {
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "name", "Leanne Graham");

            var result = await h.Resolver.ResolvePlaceholdersAsync<string>(
                "${startswith(source=$name, value=Leanne)}", SessionId, _ct);

            Assert.Equal("true", result);
        }

        [Fact]
        public async Task EndsWith_EmptyResolvedValue_ReturnsFalse()
        {
            // Guard runs on the RESOLVED value: a value that resolves to empty yields false (not a
            // vacuous 'ends with empty string' true), preserving the original behavior.
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "email", "test@example.biz");
            await PutGlobalStringAsync(h, "suffix", "");

            var result = await h.Resolver.ResolvePlaceholdersAsync<string>(
                "${endswith(source=$email, value=$suffix)}", SessionId, _ct);

            Assert.Equal("false", result);
        }
    }
}
