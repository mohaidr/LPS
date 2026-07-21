using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.PlaceHolderService;
using LPS.Infrastructure.PlaceHolderService.Methods;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices.VariableHolders;
using Moq;
using Xunit;

namespace LPS.UnitTest
{
    /// <summary>
    /// Tests for the typed <c>as</c> parameter added to jwtclaim, the boolean predicates, length and
    /// randomnumber. The inline return stays text; only the stored variable's type changes.
    /// </summary>
    public class TypedAsParameterTests
    {
        private const string SessionId = "typedas-session";
        private readonly CancellationToken _ct = CancellationToken.None;

        private sealed class Harness
        {
            public JwtClaimMethod JwtClaim;
            public ContainsMethod Contains;
            public LengthMethod Length;
            public RandomNumberMethod RandomNumber;
            public VariableManager Variables;
            public SessionManager Sessions;

            public async Task<IVariableHolder> GetStoredAsync(string name, CancellationToken token)
                => await Sessions.GetVariableAsync(SessionId, name, token) ?? await Variables.GetAsync(name, token);
        }

        private static Harness Build()
        {
            var logger = new Mock<ILogger>();
            logger.Setup(x => x.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LPSLoggingLevel>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

            var opId = new Mock<IRuntimeOperationIdProvider>();
            opId.Setup(x => x.OperationId).Returns("op-id");

            var variables = new VariableManager(opId.Object, logger.Object);
            var sessions = new SessionManager(opId.Object, logger.Object);

            PlaceholderResolverService resolver = null;
            var lazyResolver = new Lazy<IPlaceholderResolverService>(() => resolver);
            var pe = new ParameterExtractorService(lazyResolver, opId.Object, logger.Object);

            var jwtClaim = new JwtClaimMethod(pe, logger.Object, opId.Object, variables, lazyResolver, sessions);
            var contains = new ContainsMethod(pe, logger.Object, opId.Object, variables, lazyResolver, sessions);
            var length = new LengthMethod(pe, logger.Object, opId.Object, variables, lazyResolver, sessions);
            var randomNumber = new RandomNumberMethod(pe, logger.Object, opId.Object, variables, lazyResolver, sessions);

            var processor = new PlaceholderProcessor(
                new IPlaceholderMethod[] { jwtClaim, contains, length, randomNumber }, sessions, variables, opId.Object, logger.Object);
            resolver = new PlaceholderResolverService(processor, opId.Object, logger.Object);

            return new Harness
            {
                JwtClaim = jwtClaim,
                Contains = contains,
                Length = length,
                RandomNumber = randomNumber,
                Variables = variables,
                Sessions = sessions
            };
        }

        // Builds an unsigned JWT (header.payload.) with a standard-base64 payload, which is what jwtclaim decodes.
        private static string MakeJwt(string payloadJson)
        {
            string header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"));
            string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));
            return header + "." + payload + ".";
        }

        private const string Payload =
            "{\"sub\":\"user123\",\"exp\":1735689600,\"email_verified\":true,\"roles\":[\"admin\",\"editor\"]}";

        // ---------------- jwtclaim ----------------

        [Fact]
        public async Task JwtClaim_AsInt_StoresNumber_ReturnsText()
        {
            var h = Build();
            var jwt = MakeJwt(Payload);

            var result = await h.JwtClaim.ExecuteAsync($"token={jwt}, claim=exp, as=int, variable=exp", SessionId, _ct);

            Assert.Equal("1735689600", result); // inline return is text
            var stored = await h.GetStoredAsync("exp", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("1735689600", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task JwtClaim_AsBool_StoresBoolean()
        {
            var h = Build();
            var jwt = MakeJwt(Payload);

            await h.JwtClaim.ExecuteAsync($"token={jwt}, claim=email_verified, as=bool, variable=verified", SessionId, _ct);

            var stored = await h.GetStoredAsync("verified", _ct);
            Assert.IsType<BooleanVariableHolder>(stored);
            Assert.Equal("true", (await stored.GetRawValueAsync(_ct)).ToLowerInvariant());
        }

        [Fact]
        public async Task JwtClaim_AsJson_StoresNavigableArray()
        {
            var h = Build();
            var jwt = MakeJwt(Payload);

            await h.JwtClaim.ExecuteAsync($"token={jwt}, claim=roles, as=json, variable=roles", SessionId, _ct);

            var stored = await h.GetStoredAsync("roles", _ct);
            var holder = Assert.IsType<StringVariableHolder>(stored);
            Assert.Equal("[\"admin\",\"editor\"]", await holder.GetRawValueAsync(_ct));
            Assert.Equal("admin", await holder.GetValueAsync("[0]", SessionId, _ct));
        }

        [Fact]
        public async Task JwtClaim_DefaultString_Unchanged()
        {
            var h = Build();
            var jwt = MakeJwt(Payload);

            await h.JwtClaim.ExecuteAsync($"token={jwt}, claim=sub, variable=sub", SessionId, _ct);

            var stored = await h.GetStoredAsync("sub", _ct);
            Assert.IsType<StringVariableHolder>(stored);
            Assert.Equal("user123", await stored.GetRawValueAsync(_ct));
        }

        // ---------------- contains (representative predicate) ----------------

        [Fact]
        public async Task Contains_StoresBool_ReturnsText()
        {
            var h = Build();

            // contains has no `as` — it always stores a bool; the inline return stays text.
            var result = await h.Contains.ExecuteAsync("source=hello world, value=world, variable=hasWorld", SessionId, _ct);

            Assert.Equal("true", result);
            var stored = await h.GetStoredAsync("hasWorld", _ct);
            Assert.IsType<BooleanVariableHolder>(stored);
            Assert.Equal("true", await stored.GetRawValueAsync(_ct)); // lowercase => valid JSON
        }

        [Fact]
        public async Task Contains_False_StoresBool()
        {
            var h = Build();

            await h.Contains.ExecuteAsync("source=hello, value=zzz, variable=hasZ", SessionId, _ct);

            var stored = await h.GetStoredAsync("hasZ", _ct);
            Assert.IsType<BooleanVariableHolder>(stored);
            Assert.Equal("false", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task Contains_ValidationFallback_StoresBool()
        {
            var h = Build();

            // 'value' is missing => validation fallback also stores a bool false.
            await h.Contains.ExecuteAsync("source=hello, variable=missing", SessionId, _ct);

            var stored = await h.GetStoredAsync("missing", _ct);
            Assert.IsType<BooleanVariableHolder>(stored);
            Assert.Equal("false", await stored.GetRawValueAsync(_ct));
        }

        // ---------------- length / randomnumber ----------------

        [Fact]
        public async Task Length_StoresInt()
        {
            var h = Build();

            // length has no `as` — a count is always stored as an int.
            var result = await h.Length.ExecuteAsync("source=[1,2,3], variable=len", SessionId, _ct);

            Assert.Equal("3", result);
            var stored = await h.GetStoredAsync("len", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("3", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task Length_Fallback_StoresInt()
        {
            var h = Build();

            // Non-array source => fallback 0, still stored as an int.
            await h.Length.ExecuteAsync("source=notanarray, variable=len", SessionId, _ct);

            var stored = await h.GetStoredAsync("len", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("0", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task RandomNumber_StoresInt()
        {
            var h = Build();

            // randomnumber has no `as` — always stored as an int.
            var result = await h.RandomNumber.ExecuteAsync("min=5, max=5, variable=n", SessionId, _ct);

            Assert.Equal("5", result);
            var stored = await h.GetStoredAsync("n", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("5", await stored.GetRawValueAsync(_ct));
        }
    }
}
