using System;
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
    public class FindMethodTests
    {
        private const string SessionId = "find-session";
        private readonly CancellationToken _ct = CancellationToken.None;

        private const string Orders =
            "[{\"id\":1,\"status\":\"shipped\",\"total\":150}," +
            "{\"id\":2,\"status\":\"pending\",\"total\":50}," +
            "{\"id\":3,\"status\":\"shipped\",\"total\":300}]";

        private sealed class Harness
        {
            public FindMethod Find;
            public PlaceholderResolverService Resolver;
            public VariableManager Variables;
            public SessionManager Sessions;
            public ExpressionEvaluator IfEvaluator;
            public Mock<ILogger> Logger;

            // find stores session-scoped by default; check session first, then global.
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
            var find = new FindMethod(paramExtractor, logger.Object, opId.Object, variables, lazyResolver, lazyEvaluator, sessions);

            var processor = new PlaceholderProcessor(new IPlaceholderMethod[] { find }, sessions, variables, opId.Object, logger.Object);
            resolver = new PlaceholderResolverService(processor, opId.Object, logger.Object);
            ifEvaluator = new ExpressionEvaluator(resolver, node.Object, opId.Object, logger.Object);

            return new Harness { Find = find, Resolver = resolver, Variables = variables, Sessions = sessions, IfEvaluator = ifEvaluator, Logger = logger };
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

        private async Task PutGlobalJsonAsync(Harness h, string name, string json)
        {
            var holder = await new StringVariableHolder.VMaintainer(h.Resolver, new Mock<ILogger>().Object, Mock.Of<IRuntimeOperationIdProvider>())
                .WithType(VariableType.JsonString)
                .WithRawValue(json)
                .SetGlobal()
                .UpdateAsync(_ct);
            await h.Variables.PutAsync(name, holder, _ct);
        }

        [Fact]
        public async Task Find_First_Match_ReturnsProjectedValue_StoredAsInt()
        {
            var h = BuildHarness();
            var args = "source=" + Orders +
                       ", where=item.status == \"shipped\", select=item.id, match=first, variable=firstId";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("1", result);
            var stored = await h.GetStoredAsync("firstId", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("1", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task Find_All_Matches_ReturnsJsonArray()
        {
            var h = BuildHarness();
            var args = "source=" + Orders +
                       ", where=item.status == \"shipped\", select=item.id, match=all, variable=ids";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("[1,3]", result);
            var stored = await h.GetStoredAsync("ids", _ct);
            Assert.IsType<StringVariableHolder>(stored);
            Assert.Equal("[1,3]", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task Find_Count_ReturnsNumberOfMatches()
        {
            var h = BuildHarness();
            var args = "source=" + Orders +
                       ", where=item.status == \"shipped\", match=count, variable=shippedCount";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("2", result);
            var stored = await h.GetStoredAsync("shippedCount", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("2", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task Find_Last_Match_ReturnsLastProjectedValue()
        {
            var h = BuildHarness();
            var args = "source=" + Orders +
                       ", where=item.status == \"shipped\", select=item.id, match=last, variable=lastId";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("3", result);
        }

        [Fact]
        public async Task Find_Index_ReturnsNthMatch()
        {
            var h = BuildHarness();
            var args = "source=" + Orders +
                       ", where=item.status == \"shipped\", select=item.id, match=index:1, variable=secondShipped";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("3", result);
        }

        [Fact]
        public async Task Find_NoMatch_ReturnsDefault()
        {
            var h = BuildHarness();
            var args = "source=" + Orders +
                       ", where=item.status == \"cancelled\", select=item.id, match=first, default=NONE, variable=missing";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("NONE", result);
        }

        [Fact]
        public async Task Find_NumericPredicate_UsesGlobalVariable()
        {
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "threshold", "100");

            var args = "source=" + Orders +
                       ", where=item.total > ${threshold}, select=item.id, match=all, variable=bigOrders";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("[1,3]", result);
        }

        [Fact]
        public async Task Find_WholeObject_IsStoredNavigable()
        {
            var h = BuildHarness();
            var args = "source=" + Orders +
                       ", where=item.status == \"shipped\", match=first, variable=firstOrder";

            await h.Find.ExecuteAsync(args, SessionId, _ct);

            var stored = await h.GetStoredAsync("firstOrder", _ct);
            Assert.IsType<StringVariableHolder>(stored);

            // The stored object must remain path-navigable.
            var total = await h.Resolver.ResolvePlaceholdersAsync<string>("${firstOrder.total}", SessionId, _ct);
            Assert.Equal("150", total);
        }

        [Fact]
        public async Task Find_ItemBinding_IsRemovedAfterExecution()
        {
            var h = BuildHarness();
            var args = "source=" + Orders +
                       ", where=item.status == \"shipped\", select=item.id, match=first, variable=firstId";

            await h.Find.ExecuteAsync(args, SessionId, _ct);

            // No leaked internal binding variables should remain.
            var leaked = await h.GetStoredAsync("__find", _ct);
            Assert.Null(leaked);
        }

        [Fact]
        public async Task Find_InvalidSource_ReturnsDefault()
        {
            var h = BuildHarness();
            var args = "source=not-json, where=item.status == \"shipped\", select=item.id, default=fallback, variable=broken";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("fallback", result);
        }

        [Fact]
        public async Task Find_NestedPath_InPredicate_Works()
        {
            var h = BuildHarness();
            var data = "[{\"id\":1,\"company\":{\"name\":\"Acme\"}},{\"id\":2,\"company\":{\"name\":\"Globex\"}}]";
            var args = "source=" + data +
                       ", where=item.company.name == \"Globex\", select=item.id, match=first, variable=cid";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("2", result);
        }

        [Fact]
        public async Task Find_ThroughResolver_WithParensInData_Works()
        {
            var h = BuildHarness();
            var usersJson =
                "[{\"id\":1,\"username\":\"Bret\",\"phone\":\"1-770-736-8031 x56442\",\"company\":{\"name\":\"Romaguera-Crona\"}}," +
                "{\"id\":5,\"username\":\"Kamren\",\"phone\":\"(254)954-1289\",\"company\":{\"name\":\"Keebler LLC\"}}]";
            await PutGlobalJsonAsync(h, "users", usersJson);

            // Drive it through the full resolver, exactly like a config field would.
            var expr = "$find(source=${users}, where=item.company.name == \"Romaguera-Crona\", select=item.id, match=first, variable=matchedUser)";
            var result = await h.Resolver.ResolvePlaceholdersAsync<string>(expr, SessionId, _ct);

            Assert.Equal("1", result);
            var stored = await h.GetStoredAsync("matchedUser", _ct);
            Assert.NotNull(stored);
            Assert.Equal("1", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task RunAsync_ExecutesFindSideEffect_StoresVariable()
        {
            // This is exactly how a "before:" hook runs an expression: resolve for side-effects only.
            var h = BuildHarness();
            await PutGlobalJsonAsync(h, "users", "[{\"id\":7,\"username\":\"Bret\"}]");

            await h.IfEvaluator.RunAsync(
                "$find(source=${users}, where=item.username == \"Bret\", select=item.id, match=first, variable=uid)",
                SessionId, _ct);

            var stored = await h.GetStoredAsync("uid", _ct);
            Assert.NotNull(stored);
            Assert.Equal("7", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task Find_CachedSource_DoesNotCrossContaminate()
        {
            // Parsed-JSON cache is keyed by content: different bodies must not collide.
            var h = BuildHarness();
            var bodyA = "[{\"id\":1,\"name\":\"A\"}]";
            var bodyB = "[{\"id\":2,\"name\":\"B\"}]";

            var rA1 = await h.Find.ExecuteAsync("source=" + bodyA + ", select=item.id, match=first, variable=x", SessionId, _ct);
            var rB = await h.Find.ExecuteAsync("source=" + bodyB + ", select=item.id, match=first, variable=x", SessionId, _ct);
            var rA2 = await h.Find.ExecuteAsync("source=" + bodyA + ", select=item.id, match=first, variable=x", SessionId, _ct);

            Assert.Equal("1", rA1);
            Assert.Equal("2", rB);
            Assert.Equal("1", rA2);
        }

        [Fact]
        public async Task Find_RepeatedOverCachedSource_MatchAll_NotCorrupted()
        {
            // Guards the read-only invariant: match=all building a JArray must not mutate the cached tree.
            var h = BuildHarness();
            var body = "[{\"id\":1},{\"id\":2},{\"id\":3}]";
            var args = "source=" + body + ", where=item.id > 1, select=item.id, match=all, variable=ids";

            var r1 = await h.Find.ExecuteAsync(args, SessionId, _ct);
            var r2 = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("[2,3]", r1);
            Assert.Equal("[2,3]", r2);
        }

        [Fact]
        public async Task EvaluateAsync_ResultCache_StillRunsSideEffectsOnHit()
        {
            // The evaluator result-cache must NOT skip resolution: find (a Layer-1 side effect) must
            // re-run and re-store its variable even when the resolved result is served from cache.
            var h = BuildHarness();
            await PutGlobalJsonAsync(h, "users", "[{\"id\":7,\"username\":\"Bret\"}]");

            var expr = "$find(source=${users}, where=item.username == \"Bret\", select=item.id, match=first, variable=uid, isGlobal=true) == 7";

            var r1 = await h.IfEvaluator.EvaluateAsync(expr, SessionId, _ct);
            Assert.True(r1);
            Assert.Equal("7", await (await h.Variables.GetAsync("uid", _ct)).GetRawValueAsync(_ct));

            // Delete uid, then evaluate again. A cached result must NOT prevent find from re-storing uid.
            await h.Variables.RemoveVariableAsync("uid", _ct);
            var r2 = await h.IfEvaluator.EvaluateAsync(expr, SessionId, _ct);
            Assert.True(r2);

            var reStored = await h.Variables.GetAsync("uid", _ct);
            Assert.NotNull(reStored);
            Assert.Equal("7", await reStored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task Find_BindOnce_DoesNotLogCollisionWarnings()
        {
            // The item binding is registered once and updated in place per element; elements 2..N
            // must not hit the PutAsync collision branch ("already exists and will be overridden").
            var h = BuildHarness();
            var args = "source=" + Orders +
                       ", where=item.status == \"shipped\", select=item.id, match=all, variable=ids";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("[1,3]", result);
            h.Logger.Verify(
                x => x.LogAsync(It.IsAny<string>(), It.Is<string>(m => m.Contains("already exists")), It.IsAny<LPSLoggingLevel>(), It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Fact]
        public async Task Find_ReservedItemAlias_ProducesNoNotFoundWarnings()
        {
            // During the outer pre-resolution pass, ${item...} refs are deferred silently — they must
            // not produce "Variable 'item' not found" warnings.
            var h = BuildHarness();
            await PutGlobalJsonAsync(h, "users", "[{\"id\":7,\"username\":\"Bret\"}]");

            var expr = "$find(source=${users}, where=item.username == \"Bret\", select=item.id, match=first, variable=uid)";
            var result = await h.Resolver.ResolvePlaceholdersAsync<string>(expr, SessionId, _ct);

            Assert.Equal("7", result);
            h.Logger.Verify(
                x => x.LogAsync(It.IsAny<string>(), It.Is<string>(m => m.Contains("item") && m.Contains("not")), It.IsAny<LPSLoggingLevel>(), It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Fact]
        public async Task Find_SourceWithLiteralDollarText_IsNotCorrupted()
        {
            // 'source' is a placeholder resolved ONCE by find (methods self-resolve their args). The
            // resolved body's literal '$' text (e.g. "$99") is inserted and never re-scanned, so a single
            // pass leaves it intact.
            var h = BuildHarness();
            var body = "[{\"id\":1,\"note\":\"pay $99 now\"},{\"id\":2,\"note\":\"free\"}]";
            await PutGlobalJsonAsync(h, "usersBody", body);
            var args = "source=$usersBody, where=item.id == 1, select=item.note, match=first, variable=note";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("pay $99 now", result);
        }

        [Fact]
        public async Task Find_ItemBinding_ReflectsEachElement_NoStaleMemo()
        {
            // The holder is updated in place per element; a stale parsed-JSON memo would make every
            // element after the first evaluate against element #1's values.
            var h = BuildHarness();
            var body = "[{\"id\":1,\"v\":\"a\"},{\"id\":2,\"v\":\"b\"},{\"id\":3,\"v\":\"c\"}]";
            var args = "source=" + body + ", where=item.v == \"c\", select=item.id, match=first, variable=x";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("3", result);
        }

        [Fact]
        public async Task Find_ConcurrentExecutions_OverSameCachedBody_AreCorrect()
        {
            // Hammers one cached body from many tasks: shared read-only JToken (SelectTokens) plus
            // per-execution item bindings must not interfere across concurrent executions.
            var h = BuildHarness();
            var body = "[{\"id\":1,\"status\":\"shipped\"},{\"id\":2,\"status\":\"pending\"},{\"id\":3,\"status\":\"shipped\"}]";

            var tasks = Enumerable.Range(0, 16).Select(worker => Task.Run(async () =>
            {
                for (int i = 0; i < 50; i++)
                {
                    var args = "source=" + body +
                               ", where=item.status == \"shipped\", select=item.id, match=all, variable=ids_" + worker;
                    var result = await h.Find.ExecuteAsync(args, SessionId, _ct);
                    Assert.Equal("[1,3]", result);
                }
            })).ToArray();

            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task Find_JsonParseCache_StaysBounded_NoFullWipe()
        {
            var h = BuildHarness();
            var cacheField = typeof(FindMethod).GetField("JsonParseCache", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(cacheField);
            var cache = (System.Collections.ICollection)cacheField.GetValue(null);

            for (int i = 0; i < 80; i++)
            {
                var body = "[{\"id\":" + i + "}]";
                await h.Find.ExecuteAsync("source=" + body + ", select=item.id, match=first, variable=x", SessionId, _ct);
            }

            // Bounded (partial eviction may briefly overshoot by concurrent adds; here it's sequential)
            // and NOT wiped to zero/one entry the way Clear() did.
            Assert.InRange(cache.Count, 2, 33);
        }
    }
}
