using System;
using System.IO;
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

            var hmac = new HmacMethod(pe, logger.Object, opId.Object, variables, lazyResolver, sessions);
            var jwtsign = new JwtSignMethod(pe, logger.Object, opId.Object, variables, lazyResolver, sessions);

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
            // PS256 (RSA-PSS) is a real JWS algorithm we do not implement yet.
            var jwt = await jwtsign.ExecuteAsync("claims={\"sub\":\"1\"}, key=secret, algorithm=PS256", SessionId, _ct);
            Assert.Equal(string.Empty, jwt);
        }

        [Fact]
        public async Task JwtSign_MissingClaims_ReturnsEmpty()
        {
            var (_, jwtsign) = Build();
            var jwt = await jwtsign.ExecuteAsync("key=secret, algorithm=HS256", SessionId, _ct);
            Assert.Equal(string.Empty, jwt);
        }

        [Fact]
        public async Task JwtSign_Rs256_FromKeyPath_ProducesVerifiableToken()
        {
            var (_, jwtsign) = Build();

            using var rsa = RSA.Create(2048);
            var pemPath = Path.GetTempFileName();
            await File.WriteAllTextAsync(pemPath, rsa.ExportPkcs8PrivateKeyPem(), _ct);
            try
            {
                var jwt = await jwtsign.ExecuteAsync(
                    "claims={\"sub\":\"123\"}, path=" + pemPath + ", algorithm=RS256, expiresIn=300", SessionId, _ct);

                var parts = jwt.Split('.');
                Assert.Equal(3, parts.Length);

                var header = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
                Assert.Equal("RS256", (string)header["alg"]);

                var payload = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
                Assert.Equal("123", (string)payload["sub"]);
                Assert.NotNull(payload["exp"]);

                // Verify the signature over "header.payload" with the matching public key.
                var data = Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]);
                Assert.True(rsa.VerifyData(data, Base64UrlDecode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            }
            finally
            {
                File.Delete(pemPath);
            }
        }

        [Fact]
        public async Task JwtSign_Es256_FromKeyPath_ProducesVerifiableToken()
        {
            var (_, jwtsign) = Build();

            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var pemPath = Path.GetTempFileName();
            await File.WriteAllTextAsync(pemPath, ecdsa.ExportPkcs8PrivateKeyPem(), _ct);
            try
            {
                var jwt = await jwtsign.ExecuteAsync(
                    "claims={\"sub\":\"123\"}, path=" + pemPath + ", algorithm=ES256", SessionId, _ct);

                var parts = jwt.Split('.');
                Assert.Equal(3, parts.Length);
                Assert.Equal("ES256", (string)JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])))["alg"]);

                // ES256 signatures are IEEE P1363 (r||s) => 64 bytes for a P-256 key.
                var sig = Base64UrlDecode(parts[2]);
                Assert.Equal(64, sig.Length);

                var data = Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]);
                Assert.True(ecdsa.VerifyData(data, sig, HashAlgorithmName.SHA256));
            }
            finally
            {
                File.Delete(pemPath);
            }
        }

        [Fact]
        public async Task JwtSign_Rs256_FromInlinePem_ProducesVerifiableToken()
        {
            var (_, jwtsign) = Build();

            using var rsa = RSA.Create(2048);
            var pem = rsa.ExportPkcs8PrivateKeyPem();

            var jwt = await jwtsign.ExecuteAsync(
                "claims={\"sub\":\"1\"}, algorithm=RS256, key=" + pem, SessionId, _ct);

            var parts = jwt.Split('.');
            Assert.Equal(3, parts.Length);

            var data = Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]);
            Assert.True(rsa.VerifyData(data, Base64UrlDecode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }

        [Fact]
        public async Task JwtSign_Rs256_MissingKey_ReturnsEmpty()
        {
            var (_, jwtsign) = Build();
            var jwt = await jwtsign.ExecuteAsync("claims={\"sub\":\"1\"}, algorithm=RS256", SessionId, _ct);
            Assert.Equal(string.Empty, jwt);
        }

        [Fact]
        public async Task JwtSign_Hs256_KeyFromEnv_ProducesVerifiableToken()
        {
            var (_, jwtsign) = Build();
            const string envName = "LPS_TEST_JWT_HS_KEY";
            Environment.SetEnvironmentVariable(envName, "envsecret");
            try
            {
                // The HMAC secret is loaded from an environment variable via the shared SourceReader.
                var jwt = await jwtsign.ExecuteAsync(
                    "claims={\"sub\":\"1\"}, source=env, name=" + envName + ", algorithm=HS256", SessionId, _ct);

                var parts = jwt.Split('.');
                Assert.Equal(3, parts.Length);

                using var signer = new HMACSHA256(Encoding.UTF8.GetBytes("envsecret"));
                var expected = signer.ComputeHash(Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]));
                Assert.Equal(expected, Base64UrlDecode(parts[2]));
            }
            finally
            {
                Environment.SetEnvironmentVariable(envName, null);
            }
        }

        [Fact]
        public async Task JwtSign_Rs256_KeyFromEnv_ProducesVerifiableToken()
        {
            var (_, jwtsign) = Build();
            using var rsa = RSA.Create(2048);
            const string envName = "LPS_TEST_JWT_RS_KEY";
            Environment.SetEnvironmentVariable(envName, rsa.ExportPkcs8PrivateKeyPem());
            try
            {
                // The RSA private key PEM is loaded from an environment variable via SourceReader.
                var jwt = await jwtsign.ExecuteAsync(
                    "claims={\"sub\":\"1\"}, source=env, name=" + envName + ", algorithm=RS256", SessionId, _ct);

                var parts = jwt.Split('.');
                Assert.Equal(3, parts.Length);

                var data = Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]);
                Assert.True(rsa.VerifyData(data, Base64UrlDecode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            }
            finally
            {
                Environment.SetEnvironmentVariable(envName, null);
            }
        }
    }
}
