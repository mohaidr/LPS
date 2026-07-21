using System;
using System.IO;
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
    /// <summary>Tests for the $read declarative method, focused on the typed <c>as</c> parameter.</summary>
    public class ReadMethodTests
    {
        private const string SessionId = "read-session";
        private readonly CancellationToken _ct = CancellationToken.None;

        private sealed class Harness
        {
            public ReadMethod Read;
            public VariableManager Variables;
            public SessionManager Sessions;

            // $read stores session-scoped by default; check session first, then global.
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

            var read = new ReadMethod(sessions, pe, logger.Object, opId.Object, variables, lazyResolver);
            var processor = new PlaceholderProcessor(new IPlaceholderMethod[] { read }, sessions, variables, opId.Object, logger.Object);
            resolver = new PlaceholderResolverService(processor, opId.Object, logger.Object);

            return new Harness { Read = read, Variables = variables, Sessions = sessions };
        }

        private static string TempFileWith(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), $"read-{Guid.NewGuid():N}.txt");
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public async Task Read_DefaultsToString_KeepsHistoricalBehavior()
        {
            var h = Build();
            var path = TempFileWith("10");
            try
            {
                // No `as` => numeric-looking content is still stored as a plain string (unchanged behavior).
                var result = await h.Read.ExecuteAsync($"source=file, path={path}, variable=out", SessionId, _ct);

                Assert.Equal("10", result);
                var stored = await h.GetStoredAsync("out", _ct);
                Assert.IsType<StringVariableHolder>(stored);
                Assert.Equal("10", await stored.GetRawValueAsync(_ct));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public async Task Read_AsInt_StoresTypedNumber_ButReturnsText()
        {
            var h = Build();
            var path = TempFileWith("42");
            try
            {
                var result = await h.Read.ExecuteAsync($"source=file, path={path}, as=int, variable=out", SessionId, _ct);

                Assert.Equal("42", result); // inline return is always text
                var stored = await h.GetStoredAsync("out", _ct);
                Assert.IsType<NumberVariableHolder>(stored);
                Assert.Equal("42", await stored.GetRawValueAsync(_ct));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public async Task Read_AsBool_StoresBoolean()
        {
            var h = Build();
            var path = TempFileWith("true");
            try
            {
                await h.Read.ExecuteAsync($"source=file, path={path}, as=bool, variable=out", SessionId, _ct);

                var stored = await h.GetStoredAsync("out", _ct);
                Assert.IsType<BooleanVariableHolder>(stored);
                Assert.Equal("true", (await stored.GetRawValueAsync(_ct)).ToLowerInvariant());
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public async Task Read_AsJson_StoresNavigableJson()
        {
            var h = Build();
            var path = TempFileWith("{\"a\":{\"b\":7}}");
            try
            {
                await h.Read.ExecuteAsync($"source=file, path={path}, as=json, variable=out", SessionId, _ct);

                var stored = await h.GetStoredAsync("out", _ct);
                var holder = Assert.IsType<StringVariableHolder>(stored);
                // Stored as JSON => path-navigable.
                Assert.Equal("7", await holder.GetValueAsync("a.b", SessionId, _ct));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public async Task Read_NoVariable_ReturnsInlineOnly()
        {
            var h = Build();
            var path = TempFileWith("payload");
            try
            {
                var result = await h.Read.ExecuteAsync($"source=file, path={path}", SessionId, _ct);
                Assert.Equal("payload", result);
            }
            finally { File.Delete(path); }
        }
    }
}
