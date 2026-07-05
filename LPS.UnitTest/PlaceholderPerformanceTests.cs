using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
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
    /// <summary>
    /// Guards the hot-path optimizations: the parsed-JSON memo on StringVariableHolder,
    /// single-pass parameter extraction, and cache eviction policies.
    /// </summary>
    public class PlaceholderPerformanceTests
    {
        private const string SessionId = "perf-session";
        private readonly CancellationToken _ct = CancellationToken.None;

        private static (PlaceholderResolverService resolver, ParameterExtractorService extractor, IfEvaluator evaluator) BuildServices()
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
            var lazyResolver = new Lazy<IPlaceholderResolverService>(() => resolver);
            var extractor = new ParameterExtractorService(lazyResolver, opId.Object, logger.Object);
            var processor = new PlaceholderProcessor(Array.Empty<IPlaceholderMethod>(), sessions, variables, opId.Object, logger.Object);
            resolver = new PlaceholderResolverService(processor, opId.Object, logger.Object);
            var evaluator = new IfEvaluator(resolver, node.Object, opId.Object, logger.Object);

            return (resolver, extractor, evaluator);
        }

        private static StringVariableHolder.VMaintainer NewJsonMaintainer(PlaceholderResolverService resolver)
        {
            return new StringVariableHolder.VMaintainer(resolver, new Mock<ILogger>().Object, Mock.Of<IRuntimeOperationIdProvider>())
                .WithType(VariableType.JsonString);
        }

        [Fact]
        public async Task Holder_ParsedJsonMemo_IsInvalidated_OnUpdate()
        {
            // Update the same holder in place (as find's loop does): path reads must reflect the
            // NEW value, never a stale memoized parse of the old one.
            var (resolver, _, _) = BuildServices();
            var maintainer = NewJsonMaintainer(resolver);

            var holder = (StringVariableHolder)await maintainer.WithRawValue("{\"a\":1}").UpdateAsync(CancellationToken.None);
            Assert.Equal("1", await holder.GetValueAsync(".a", SessionId, CancellationToken.None));
            // Read twice: second read is served from the memo and must match.
            Assert.Equal("1", await holder.GetValueAsync(".a", SessionId, CancellationToken.None));

            await maintainer.WithRawValue("{\"a\":2}").UpdateAsync(CancellationToken.None);
            Assert.Equal("2", await holder.GetValueAsync(".a", SessionId, CancellationToken.None));
        }

        [Fact]
        public async Task Holder_WithParsedToken_SeedsMemo_AndMatchesRawValue()
        {
            var (resolver, _, _) = BuildServices();
            var maintainer = NewJsonMaintainer(resolver);

            var element = Newtonsoft.Json.Linq.JToken.Parse("{\"name\":\"acme\",\"total\":42}");
            var holder = (StringVariableHolder)await maintainer
                .WithRawValue(element.ToString(Newtonsoft.Json.Formatting.None))
                .WithParsedToken(element)
                .UpdateAsync(CancellationToken.None);

            Assert.Equal("acme", await holder.GetValueAsync(".name", SessionId, CancellationToken.None));
            Assert.Equal("42", await holder.GetValueAsync(".total", SessionId, CancellationToken.None));

            // A later update WITHOUT a seeded token must not keep serving the old seed.
            await maintainer.WithRawValue("{\"name\":\"globex\",\"total\":7}").UpdateAsync(CancellationToken.None);
            Assert.Equal("globex", await holder.GetValueAsync(".name", SessionId, CancellationToken.None));
        }

        [Theory]
        [InlineData("source={\"a\":[1,2]}, where=\"${item.x}\" == \"y, z\", select=${item.id}", "where")]
        [InlineData("source={\"a\":[1,2]}, where=\"${item.x}\" == \"y, z\", select=${item.id}", "select")]
        [InlineData("source={\"a\":[1,2]}, where=\"${item.x}\" == \"y, z\", select=${item.id}", "source")]
        [InlineData("key=a=b=c, other=(1,2), third=[x, y], fourth={\"k\": \"v,w\"}", "key")]
        [InlineData("key=a=b=c, other=(1,2), third=[x, y], fourth={\"k\": \"v,w\"}", "other")]
        [InlineData("key=a=b=c, other=(1,2), third=[x, y], fourth={\"k\": \"v,w\"}", "third")]
        [InlineData("key=a=b=c, other=(1,2), third=[x, y], fourth={\"k\": \"v,w\"}", "fourth")]
        public void ParseParameters_Matches_ExtractRawString(string parameters, string key)
        {
            var (_, extractor, _) = BuildServices();

            var expected = extractor.ExtractRawString(parameters, key, "<default>");
            var dict = extractor.ParseParameters(parameters);
            var actual = dict.TryGetValue(key, out var v) ? v : "<default>";

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ParseParameters_MissingKey_IsAbsent()
        {
            var (_, extractor, _) = BuildServices();
            var dict = extractor.ParseParameters("a=1, b=2");
            Assert.False(dict.ContainsKey("missing"));
            Assert.Equal("1", dict["a"]);
            Assert.Equal("2", dict["b"]);
        }

        [Fact]
        public async Task IfEvaluator_ResultCache_TrimsPartially_NotFullWipe()
        {
            var (_, _, evaluator) = BuildServices();

            var cacheField = typeof(IfEvaluator).GetField("ResultCache", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(cacheField);
            var cache = (ConcurrentDictionary<string, bool>)cacheField.GetValue(null);

            var maxField = typeof(IfEvaluator).GetField("MaxResultCacheSize", BindingFlags.NonPublic | BindingFlags.Static);
            var max = (int)maxField.GetValue(null);

            cache.Clear();
            for (int i = 0; i < max + 200; i++)
            {
                cache["synthetic_" + i] = true;
            }

            // One real evaluation triggers the trim after its insert.
            var result = await evaluator.EvaluateAsync("1 == 1", SessionId, CancellationToken.None);
            Assert.True(result);

            Assert.InRange(cache.Count, 1, max);
            // Partial eviction: most of the cache must survive (Clear() would leave ~1 entry).
            Assert.True(cache.Count > max / 2, $"expected partial eviction, but only {cache.Count} entries survived");
            cache.Clear();
        }

        [Fact]
        public async Task IfEvaluator_OversizedResolvedExpression_IsNotCached()
        {
            var (_, _, evaluator) = BuildServices();

            var cacheField = typeof(IfEvaluator).GetField("ResultCache", BindingFlags.NonPublic | BindingFlags.Static);
            var cache = (ConcurrentDictionary<string, bool>)cacheField.GetValue(null);
            cache.Clear();

            // > 512 chars once resolved/normalized: evaluates fine but must not be inserted.
            // (Assert on the specific key, not cache emptiness — the static cache is shared with
            // other test classes that may run in parallel.)
            var bigLiteral = new string('x', 600);
            var result = await evaluator.EvaluateAsync($"\"{bigLiteral}\" == \"{bigLiteral}\"", SessionId, CancellationToken.None);

            Assert.True(result);
            Assert.DoesNotContain(cache.Keys, k => k.Contains(bigLiteral));
        }
    }
}
