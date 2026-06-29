using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.PlaceHolderService;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LPS.UnitTest
{
    public class VariablePlaceholderTests
    {
        private readonly Mock<IPlaceholderProcessor> _mockProcessor;
        private readonly Mock<IRuntimeOperationIdProvider> _mockOperationIdProvider;
        private readonly Mock<ILogger> _mockLogger;
        private readonly PlaceholderResolverService _resolver;
        private const string SessionId = "test-session";
        private readonly CancellationToken _cancellationToken = CancellationToken.None;

        public VariablePlaceholderTests()
        {
            _mockProcessor = new Mock<IPlaceholderProcessor>();
            _mockOperationIdProvider = new Mock<IRuntimeOperationIdProvider>();
            _mockLogger = new Mock<ILogger>();

            _mockOperationIdProvider.Setup(x => x.OperationId).Returns("test-op-id");

            _resolver = new PlaceholderResolverService(
                _mockProcessor.Object,
                _mockOperationIdProvider.Object,
                _mockLogger.Object);
        }

        // ==================== NESTED RESOLUTION CASES ====================

        [Fact]
        public async Task Resolve_SimpleNestedVariable_ResolvesSuccessfully()
        {
            // Case 1: ${${rps}}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("rps", SessionId, _cancellationToken))
                .ReturnsAsync("Metrics.Throughput.RPS");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("Metrics.Throughput.RPS", SessionId, _cancellationToken))
                .ReturnsAsync("1234.56");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${${rps}}", SessionId, _cancellationToken);

            Assert.Equal("1234.56", result);
            _mockProcessor.Verify(x => x.ProcessPlaceholderAsync(It.IsAny<string>(), SessionId, _cancellationToken), Times.Exactly(2));
        }

        [Fact]
        public async Task Resolve_TripleNestedVariable_ResolvesSuccessfully()
        {
            // Case 2: ${${${param}}}}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("param", SessionId, _cancellationToken))
                .ReturnsAsync("rps");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("rps", SessionId, _cancellationToken))
                .ReturnsAsync("Metrics.Throughput.RPS");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("Metrics.Throughput.RPS", SessionId, _cancellationToken))
                .ReturnsAsync("5000");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${${${param}}}", SessionId, _cancellationToken);

            Assert.Equal("5000", result);
            _mockProcessor.Verify(x => x.ProcessPlaceholderAsync(It.IsAny<string>(), SessionId, _cancellationToken), Times.Exactly(3));
        }

        [Fact]
        public async Task Resolve_ArrayIndexWithNestedVariable_ResolvesSuccessfully()
        {
            // Case 4: ${Variable[${index}]}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("index", SessionId, _cancellationToken))
                .ReturnsAsync("2");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("Variable[2]", SessionId, _cancellationToken))
                .ReturnsAsync("value-at-index-2");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${Variable[${index}]}", SessionId, _cancellationToken);

            Assert.Equal("value-at-index-2", result);
        }

        [Fact]
        public async Task Resolve_MethodWithNestedVariableArgument_ResolvesSuccessfully()
        {
            // Case 5: ${SomeMethod(${param})}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("param", SessionId, _cancellationToken))
                .ReturnsAsync("test-value");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("SomeMethod(test-value)", SessionId, _cancellationToken))
                .ReturnsAsync("method-result");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${SomeMethod(${param})}", SessionId, _cancellationToken);

            Assert.Equal("method-result", result);
        }

        [Fact]
        public async Task Resolve_MultipleNestedInExpression_ResolvesSuccessfully()
        {
            // Case 6: ${${rps}} < 50 & ${${rps}} != 0
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("rps", SessionId, _cancellationToken))
                .ReturnsAsync("Metrics.Throughput.RPS");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("Metrics.Throughput.RPS", SessionId, _cancellationToken))
                .ReturnsAsync("35");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${${rps}} < 50 & ${${rps}} != 0", SessionId, _cancellationToken);

            // Should resolve both occurrences
            Assert.Equal("35 < 50 & 35 != 0", result);
            _mockProcessor.Verify(x => x.ProcessPlaceholderAsync(It.IsAny<string>(), SessionId, _cancellationToken), Times.AtLeast(4));
        }

        [Fact]
        public async Task Resolve_NestedInObjectPath_ResolvesSuccessfully()
        {
            // Case 7: ${Variable.${propertyName}}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("propertyName", SessionId, _cancellationToken))
                .ReturnsAsync("Status");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("Variable.Status", SessionId, _cancellationToken))
                .ReturnsAsync("Active");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${Variable.${propertyName}}", SessionId, _cancellationToken);

            Assert.Equal("Active", result);
        }

        [Fact]
        public async Task Resolve_ComplexNestedLookup_ResolvesSuccessfully()
        {
            // Case 9: ${Data[${GetIndex(${iteration})}]}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("iteration", SessionId, _cancellationToken))
                .ReturnsAsync("5");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("GetIndex(5)", SessionId, _cancellationToken))
                .ReturnsAsync("12");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("Data[12]", SessionId, _cancellationToken))
                .ReturnsAsync("complex-result");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${Data[${GetIndex(${iteration})}]}", SessionId, _cancellationToken);

            Assert.Equal("complex-result", result);
        }

        // ==================== BACKWARD COMPATIBILITY CASES ====================

        [Fact]
        public async Task Resolve_SimplePlaceholder_ResolvesUnchanged()
        {
            // Case 10: ${Variable}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("Variable", SessionId, _cancellationToken))
                .ReturnsAsync("simple-value");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${Variable}", SessionId, _cancellationToken);

            Assert.Equal("simple-value", result);
        }

        [Fact]
        public async Task Resolve_SimpleMethod_ResolvesUnchanged()
        {
            // Case 11: ${SomeMethod()}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("SomeMethod()", SessionId, _cancellationToken))
                .ReturnsAsync("method-return");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${SomeMethod()}", SessionId, _cancellationToken);

            Assert.Equal("method-return", result);
        }

        [Fact]
        public async Task Resolve_DotNotationPath_ResolvesUnchanged()
        {
            // Case 12: ${Variable.Property.SubProperty}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("Variable.Property.SubProperty", SessionId, _cancellationToken))
                .ReturnsAsync("nested-value");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${Variable.Property.SubProperty}", SessionId, _cancellationToken);

            Assert.Equal("nested-value", result);
        }

        [Fact]
        public async Task Resolve_ArrayBracketSyntax_ResolvesUnchanged()
        {
            // Case 13: ${Variable[0]}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("Variable[0]", SessionId, _cancellationToken))
                .ReturnsAsync("first-item");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${Variable[0]}", SessionId, _cancellationToken);

            Assert.Equal("first-item", result);
        }

        [Fact]
        public async Task Resolve_AliasedMethods_ResolvesUnchanged()
        {
            // Case 15a: ${datetime()}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("datetime()", SessionId, _cancellationToken))
                .ReturnsAsync("2026-06-28");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${datetime()}", SessionId, _cancellationToken);

            Assert.Equal("2026-06-28", result);
        }

        [Fact]
        public async Task Resolve_VariableNotFound_ReturnsUnchanged()
        {
            // Case 16: ${UnknownVariable}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("UnknownVariable", SessionId, _cancellationToken))
                .ReturnsAsync("${UnknownVariable}");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${UnknownVariable}", SessionId, _cancellationToken);

            Assert.Equal("${UnknownVariable}", result);
        }

        [Fact]
        public async Task Resolve_MethodNotFound_ReturnsEmpty()
        {
            // Case 17: ${UnknownMethod()}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("UnknownMethod()", SessionId, _cancellationToken))
                .ReturnsAsync(string.Empty);

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${UnknownMethod()}", SessionId, _cancellationToken);

            Assert.Null(result);
        }

        [Fact]
        public async Task Resolve_DollarSignInReturnValue_NotReResolved()
        {
            // Case 18: ${SomeMethod()} returns "Price: $100" - should not re-resolve
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("SomeMethod()", SessionId, _cancellationToken))
                .ReturnsAsync("Price: $100");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("Cost: ${SomeMethod()}", SessionId, _cancellationToken);

            // The returned value is inserted back into string, not in ${} context, so no re-resolution
            Assert.Equal("Cost: Price: $100", result);
            _mockProcessor.Verify(x => x.ProcessPlaceholderAsync(It.IsAny<string>(), SessionId, _cancellationToken), Times.Once);
        }

        [Fact]
        public async Task Resolve_EscapedDollarSigns_TreatsAsLiteral()
        {
            // Case 19: $$ should become single $
            var result = await _resolver.ResolvePlaceholdersAsync<string>("Price: $$100", SessionId, _cancellationToken);

            Assert.Equal("Price: $100", result);
        }

        [Fact]
        public async Task Resolve_MixedPlaceholders_ResolvesEach()
        {
            // Case 20: "Value is ${var1} and ${var2}"
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("var1", SessionId, _cancellationToken))
                .ReturnsAsync("first");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("var2", SessionId, _cancellationToken))
                .ReturnsAsync("second");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("Value is ${var1} and ${var2}", SessionId, _cancellationToken);

            Assert.Equal("Value is first and second", result);
        }

        // ==================== EDGE CASES ====================

        [Fact]
        public async Task Resolve_CircularReferenceDetected_StopsAtMaxDepth()
        {
            // Case 21: Circular reference should not crash resolution.
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("rps", SessionId, _cancellationToken))
                .ReturnsAsync("${rps}");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("${rps}", SessionId, _cancellationToken))
                .ReturnsAsync("${rps}");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${${rps}}", SessionId, _cancellationToken);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task Resolve_EmptyResultFromInnerResolution_HandlesGracefully()
        {
            // Case 22: ${${emptyVar}} where emptyVar resolves to empty string
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("emptyVar", SessionId, _cancellationToken))
                .ReturnsAsync(string.Empty);
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync(string.Empty, SessionId, _cancellationToken))
                .ReturnsAsync(string.Empty);

            var result = await _resolver.ResolvePlaceholdersAsync<string>("Value: ${${emptyVar}}", SessionId, _cancellationToken);

            Assert.Equal("Value: ", result);
        }

        [Fact]
        public async Task Resolve_NonExistentInnerVariable_FailsGracefully()
        {
            // Case 23: ${${nonExistent}} where nonExistent doesn't exist
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("nonExistent", SessionId, _cancellationToken))
                .ReturnsAsync("${nonExistent}");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("${nonExistent}", SessionId, _cancellationToken))
                .ReturnsAsync("${nonExistent}");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${${nonExistent}}", SessionId, _cancellationToken);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task Resolve_NestedWithPath_ResolvesNestedThenAppliesPath()
        {
            // Case 24: ${${varName}.Property}
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("varName", SessionId, _cancellationToken))
                .ReturnsAsync("MyVariable");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("MyVariable.Property", SessionId, _cancellationToken))
                .ReturnsAsync("property-value");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("${${varName}.Property}", SessionId, _cancellationToken);

            Assert.Equal("property-value", result);
        }

        [Fact]
        public async Task Resolve_WhitespaceAndLineBreaks_ResolvesCorrectly()
        {
            // Edge case: input is trimmed inside resolver.
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("Variable", SessionId, _cancellationToken))
                .ReturnsAsync("value");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("  ${Variable}  ", SessionId, _cancellationToken);

            Assert.Equal("value", result);
        }

        [Fact]
        public async Task Resolve_EmptyInput_ReturnsNull()
        {
            // Edge case: empty string input
            var result = await _resolver.ResolvePlaceholdersAsync<string>(string.Empty, SessionId, _cancellationToken);

            Assert.Null(result);
        }

        [Fact]
        public async Task Resolve_NoPlaceholders_ReturnsUnchanged()
        {
            // Edge case: string with no placeholders
            var input = "This is plain text with no variables";
            var result = await _resolver.ResolvePlaceholdersAsync<string>(input, SessionId, _cancellationToken);

            Assert.Equal(input, result);
        }

        [Fact]
        public async Task Resolve_LegacyDollarSyntax_ResolvesUnchanged()
        {
            // Edge case: legacy $variable syntax (without braces)
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("varName", SessionId, _cancellationToken))
                .ReturnsAsync("legacy-value");

            var result = await _resolver.ResolvePlaceholdersAsync<string>("$varName", SessionId, _cancellationToken);

            Assert.Equal("legacy-value", result);
        }

        [Fact]
        public async Task Resolve_ComplexExpression_ResolvesAllPlaceholders()
        {
            // Integration: Complex real-world expression
            // ${${rps}} < 50 & ${Metrics.Throughput.RequestCount} > 100
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("rps", SessionId, _cancellationToken))
                .ReturnsAsync("Metrics.Throughput.RPS");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("Metrics.Throughput.RPS", SessionId, _cancellationToken))
                .ReturnsAsync("45");
            _mockProcessor
                .Setup(x => x.ProcessPlaceholderAsync("Metrics.Throughput.RequestCount", SessionId, _cancellationToken))
                .ReturnsAsync("250");

            var expression = "${${rps}} < 50 & ${Metrics.Throughput.RequestCount} > 100";
            var result = await _resolver.ResolvePlaceholdersAsync<string>(expression, SessionId, _cancellationToken);

            Assert.Equal("45 < 50 & 250 > 100", result);
        }
    }
}
