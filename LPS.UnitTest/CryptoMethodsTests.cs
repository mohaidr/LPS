using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.PlaceHolderService;
using LPS.Infrastructure.PlaceHolderService.Methods;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LPS.UnitTest
{
    /// <summary>Tests for the crypto declarative methods: $hmac and $jwtsign.</summary>
    public class CryptoMethodsTests
    {
        private const string SessionId = "crypto-session";
        private readonly CancellationToken _ct = CancellationToken.None;

        private static (HmacMethod hmac, JwtSignMethod jwtsign) Build()
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

            var hmac = new HmacMethod(pe, logger.Object, opId.Object, variables, lazyResolver);
            var jwtsign = new JwtSignMethod(pe, logger.Object, opId.Object, variables, lazyResolver);

            var processor = new PlaceholderProcessor(new IPlaceholderMethod[] { hmac, jwtsign }, sessions, variables, opId.Object, logger.Object);
            resolver = new PlaceholderResolverService(processor, opId.Object, logger.Object);

            return (hmac, jwtsign);
        }

        private static byte[] Base64UrlDecode(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }

        // ---------------- hmac ----------------

        [Fact]
        public async Task Hmac_Sha256_Hex_MatchesComputedDigest()
        {
            var (hmac, _) = Build();

            var result = await hmac.ExecuteAsync("value=payload, key=secret, algorithm=SHA256", SessionId, _ct);

            using var expected = new HMACSHA256(Encoding.UTF8.GetBytes("secret"));
            var expectedHex = BitConverter.ToString(expected.ComputeHash(Encoding.UTF8.GetBytes("payload"))).Replace("-", "").ToLowerInvariant();

            Assert.Equal(expectedHex, result);
        }

        [Fact]
        public async Task Hmac_Base64Encoding_MatchesComputedDigest()
        {
            var (hmac, _) = Build();

            var result = await hmac.ExecuteAsync("value=payload, key=secret, algorithm=SHA256, encoding=base64", SessionId, _ct);

            using var expected = new HMACSHA256(Encoding.UTF8.GetBytes("secret"));
            var expectedB64 = Convert.ToBase64String(expected.ComputeHash(Encoding.UTF8.GetBytes("payload")));

            Assert.Equal(expectedB64, result);
        }

        [Fact]
        public async Task Hmac_MissingKey_ReturnsEmpty()
        {
            var (hmac, _) = Build();
            var result = await hmac.ExecuteAsync("value=payload, algorithm=SHA256", SessionId, _ct);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task Hmac_UnsupportedAlgorithm_ReturnsEmpty()
        {
            var (hmac, _) = Build();
            var result = await hmac.ExecuteAsync("value=payload, key=secret, algorithm=MD5", SessionId, _ct);
            Assert.Equal(string.Empty, result);
        }

        // ---------------- jwtsign ----------------

        [Fact]
        public async Task JwtSign_Hs256_ProducesVerifiableToken()
        {
            var (_, jwtsign) = Build();

            var jwt = await jwtsign.ExecuteAsync("claims={\"sub\":\"123\"}, key=secret, algorithm=HS256, expiresIn=300", SessionId, _ct);

            var parts = jwt.Split('.');
            Assert.Equal(3, parts.Length);

            // Header
            var header = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
            Assert.Equal("HS256", (string)header["alg"]);
            Assert.Equal("JWT", (string)header["typ"]);

            // Payload: original claim + auto iat/exp
            var payload = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
            Assert.Equal("123", (string)payload["sub"]);
            Assert.NotNull(payload["iat"]);
            Assert.NotNull(payload["exp"]);
            Assert.Equal((long)payload["iat"] + 300, (long)payload["exp"]);

            // Signature: recompute HMACSHA256 over "header.payload"
            using var signer = new HMACSHA256(Encoding.UTF8.GetBytes("secret"));
            var expectedSig = signer.ComputeHash(Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]));
            Assert.Equal(expectedSig, Base64UrlDecode(parts[2]));
        }

        [Fact]
        public async Task JwtSign_DoesNotOverrideExplicitExp()
        {
            var (_, jwtsign) = Build();

            var jwt = await jwtsign.ExecuteAsync("claims={\"sub\":\"1\",\"exp\":99}, key=secret, expiresIn=300", SessionId, _ct);
            var payload = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(jwt.Split('.')[1])));

            Assert.Equal(99, (long)payload["exp"]);
        }

        [Fact]
        public async Task JwtSign_UnsupportedAlgorithm_ReturnsEmpty()
        {
            var (_, jwtsign) = Build();
            var jwt = await jwtsign.ExecuteAsync("claims={\"sub\":\"1\"}, key=secret, algorithm=RS256", SessionId, _ct);
            Assert.Equal(string.Empty, jwt);
        }

        [Fact]
        public async Task JwtSign_MissingClaims_ReturnsEmpty()
        {
            var (_, jwtsign) = Build();
            var jwt = await jwtsign.ExecuteAsync("key=secret, algorithm=HS256", SessionId, _ct);
            Assert.Equal(string.Empty, jwt);
        }
    }
}
