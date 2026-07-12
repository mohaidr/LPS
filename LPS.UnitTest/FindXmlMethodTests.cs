using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.PlaceHolderService;
using LPS.Infrastructure.PlaceHolderService.Methods;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices.VariableHolders;
using LPS.Domain.Domain.Common.Enums;
using Moq;
using Xunit;

namespace LPS.UnitTest
{
    /// <summary>Tests for the $findxml declarative method (XPath over XML sources).</summary>
    public class FindXmlMethodTests
    {
        private const string SessionId = "findxml-session";
        private readonly CancellationToken _ct = CancellationToken.None;

        private const string Orders =
            "<orders>" +
            "<order><id>1</id><status>shipped</status><total>150</total></order>" +
            "<order><id>2</id><status>pending</status><total>50</total></order>" +
            "<order><id>3</id><status>shipped</status><total>300</total></order>" +
            "</orders>";

        private sealed class Harness
        {
            public FindXmlMethod Find;
            public VariableManager Variables;
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

            var find = new FindXmlMethod(pe, logger.Object, opId.Object, variables, lazyResolver);
            var processor = new PlaceholderProcessor(new IPlaceholderMethod[] { find }, sessions, variables, opId.Object, logger.Object);
            resolver = new PlaceholderResolverService(processor, opId.Object, logger.Object);

            return new Harness { Find = find, Variables = variables };
        }

        private async Task<string> RawAsync(Harness h, string name)
        {
            var holder = await h.Variables.GetAsync(name, _ct);
            return holder == null ? null : await holder.GetRawValueAsync(_ct);
        }

        [Fact]
        public async Task First_Match_ProjectsSelectedField()
        {
            var h = Build();
            var args = "source=" + Orders + ", path=/orders/order, where=status=\"shipped\", select=id, match=first, variable=firstId";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("1", result);
            Assert.Equal("1", await RawAsync(h, "firstId"));
        }

        [Fact]
        public async Task Last_Match_ReturnsLast()
        {
            var h = Build();
            var args = "source=" + Orders + ", path=/orders/order, where=status=\"shipped\", select=id, match=last";
            Assert.Equal("3", await h.Find.ExecuteAsync(args, SessionId, _ct));
        }

        [Fact]
        public async Task Count_ReturnsNumberOfMatches()
        {
            var h = Build();
            var args = "source=" + Orders + ", path=/orders/order, where=status=\"shipped\", match=count, variable=shippedCount";

            var result = await h.Find.ExecuteAsync(args, SessionId, _ct);

            Assert.Equal("2", result);
            var stored = await h.Variables.GetAsync("shippedCount", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("2", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task All_ReturnsJsonArrayOfProjectedValues()
        {
            var h = Build();
            var args = "source=" + Orders + ", path=/orders/order, where=status=\"shipped\", select=id, match=all";
            Assert.Equal("[\"1\",\"3\"]", await h.Find.ExecuteAsync(args, SessionId, _ct));
        }

        [Fact]
        public async Task Index_ReturnsNthMatch()
        {
            var h = Build();
            var args = "source=" + Orders + ", path=/orders/order, where=status=\"shipped\", select=id, match=index:1";
            Assert.Equal("3", await h.Find.ExecuteAsync(args, SessionId, _ct));
        }

        [Fact]
        public async Task Select_Attribute_Works()
        {
            var h = Build();
            var xml = "<items><item kind=\"a\">x</item><item kind=\"b\">y</item></items>";
            var args = "source=" + xml + ", path=/items/item, where=@kind=\"b\", select=@kind, match=first";
            Assert.Equal("b", await h.Find.ExecuteAsync(args, SessionId, _ct));
        }

        [Fact]
        public async Task NoMatch_ReturnsDefault()
        {
            var h = Build();
            var args = "source=" + Orders + ", path=/orders/order, where=status=\"cancelled\", select=id, match=first, default=NONE";
            Assert.Equal("NONE", await h.Find.ExecuteAsync(args, SessionId, _ct));
        }

        [Fact]
        public async Task InvalidXml_ReturnsDefault()
        {
            var h = Build();
            var args = "source=not-xml, path=/orders/order, match=first, default=NONE";
            Assert.Equal("NONE", await h.Find.ExecuteAsync(args, SessionId, _ct));
        }

        [Fact]
        public async Task NoSelect_ReturnsNodeInnerText()
        {
            var h = Build();
            var xml = "<root><name>Leanne</name></root>";
            var args = "source=" + xml + ", path=/root/name, match=first";
            Assert.Equal("Leanne", await h.Find.ExecuteAsync(args, SessionId, _ct));
        }
    }
}
