using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
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
    /// Tests for the numeric / comparison declarative methods (sum, min, max, multiply, average,
    /// divide, subtract, mod, pow, abs, floor, ceil, round, clamp, setvariable, greater/less/equal,
    /// the greaterthan/smallerthan family, and stringequals).
    /// </summary>
    public class NumericMethodsTests
    {
        private const string SessionId = "numeric-session";
        private readonly CancellationToken _ct = CancellationToken.None;

        private sealed class Harness
        {
            public PlaceholderResolverService Resolver;
            public PlaceholderProcessor Processor;
            public VariableManager Variables;
            public Dictionary<string, IPlaceholderMethod> Methods;

            public Task<string> Run(string name, string args) => Methods[name].ExecuteAsync(args, SessionId, CancellationToken.None);
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

            var variables = new VariableManager(opId.Object, logger.Object);
            var sessions = new SessionManager(opId.Object, logger.Object);

            PlaceholderResolverService resolver = null;
            var lazyResolver = new Lazy<IPlaceholderResolverService>(() => resolver);
            var pe = new ParameterExtractorService(lazyResolver, opId.Object, logger.Object);

            var methods = new IPlaceholderMethod[]
            {
                new SetVariableMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new SumMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new MinMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new MaxMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new MultiplyMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new AverageMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new DivideMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new SubtractMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new ModMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new PowMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new AbsMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new FloorMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new CeilMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new RoundMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new ClampMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new GreaterThanMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new SmallerThanMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new GreaterThanOrEqualMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new SmallerThanOrEqualMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new GreaterMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new LessMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new EqualMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
                new StringEqualsMethod(pe, logger.Object, opId.Object, variables, lazyResolver),
            };

            var processor = new PlaceholderProcessor(methods, sessions, variables, opId.Object, logger.Object);
            resolver = new PlaceholderResolverService(processor, opId.Object, logger.Object);

            return new Harness
            {
                Resolver = resolver,
                Processor = processor,
                Variables = variables,
                Methods = methods.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase),
            };
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

        private async Task<string> RawAsync(Harness h, string name)
        {
            var holder = await h.Variables.GetAsync(name, _ct);
            return holder == null ? null : await holder.GetRawValueAsync(_ct);
        }

        // ---------------- setvariable ----------------

        [Fact]
        public async Task SetVariable_StoresStringByDefault()
        {
            var h = BuildHarness();

            var result = await h.Run("setvariable", "variable=greeting, value=hello");

            Assert.Equal("hello", result);
            var stored = await h.Variables.GetAsync("greeting", _ct);
            Assert.IsType<StringVariableHolder>(stored);
            Assert.Equal("hello", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task SetVariable_AsInt_StoresNumber()
        {
            var h = BuildHarness();

            var result = await h.Run("setvariable", "variable=count, value=42, as=int");

            Assert.Equal("42", result);
            var stored = await h.Variables.GetAsync("count", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("42", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task SetVariable_SetAlias_Works()
        {
            var h = BuildHarness();

            var result = await h.Processor.ProcessPlaceholderAsync("set(variable=aliased, value=world)", SessionId, _ct);

            Assert.Equal("world", result);
            Assert.Equal("world", await RawAsync(h, "aliased"));
        }

        [Fact]
        public async Task SetVariable_AsInt_EvaluatesArithmetic()
        {
            var h = BuildHarness();

            await h.Run("setvariable", "variable=sum, value=2+3, as=int");

            var stored = await h.Variables.GetAsync("sum", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("5", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task SetVariable_AsDouble_EvaluatesArithmetic()
        {
            var h = BuildHarness();

            await h.Run("setvariable", "variable=ratio, value=1.5+1, as=double");

            var stored = await h.Variables.GetAsync("ratio", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("2.5", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task SetVariable_AsString_DoesNotEvaluateArithmetic()
        {
            var h = BuildHarness();

            await h.Run("setvariable", "variable=expr, value=2+3");

            var stored = await h.Variables.GetAsync("expr", _ct);
            Assert.IsType<StringVariableHolder>(stored);
            Assert.Equal("2+3", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task MethodArgument_IsAnotherMethod_ResolvesInnerThenOuter()
        {
            // A method's parameter is itself a method placeholder: setvariable's `value` is $sum(...).
            // Under Option A the outer method is dispatched with RAW args and resolves `value` itself,
            // which runs the inner $sum on demand — so method-in-method-arg works end to end.
            var h = BuildHarness();

            var result = await h.Resolver.ResolvePlaceholdersAsync<string>(
                "${setvariable(variable=total, value=$sum(source=[2,3]), as=int)}", SessionId, _ct);

            Assert.Equal("5", result);
            var stored = await h.Variables.GetAsync("total", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("5", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task MethodArgument_DeeplyNestedMethods_Resolve()
        {
            // Two levels of method nesting inside an arg: multiply([ sum([2,3]), 2 ]) = 10.
            var h = BuildHarness();

            var result = await h.Resolver.ResolvePlaceholdersAsync<string>(
                "${setvariable(variable=x, value=$multiply(source=[$sum(source=[2,3]), 2]), as=int)}", SessionId, _ct);

            Assert.Equal("10", result);
            var stored = await h.Variables.GetAsync("x", _ct);
            Assert.Equal("10", await stored.GetRawValueAsync(_ct));
        }

        // ---------------- aggregations ----------------

        [Fact]
        public async Task Sum_LiteralArray_ReturnsInt()
        {
            var h = BuildHarness();

            var result = await h.Run("sum", "source=[1, 2, 3, 4], variable=total");

            Assert.Equal("10", result);
            var stored = await h.Variables.GetAsync("total", _ct);
            Assert.IsType<NumberVariableHolder>(stored);
            Assert.Equal("10", await stored.GetRawValueAsync(_ct));
        }

        [Fact]
        public async Task Sum_ArrayWithPlaceholders_Resolves()
        {
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "x", "5");
            await PutGlobalStringAsync(h, "z", "7");

            var result = await h.Run("sum", "source=[${x}, 10, 4, ${z}]");

            Assert.Equal("26", result);
        }

        [Fact]
        public async Task Sum_VariableHoldingArray_Resolves()
        {
            var h = BuildHarness();
            await PutGlobalJsonAsync(h, "prices", "[1, 2, 3]");

            var result = await h.Run("sum", "source=${prices}");

            Assert.Equal("6", result);
        }

        [Fact]
        public async Task Sum_SkipsNonNumericElements()
        {
            var h = BuildHarness();

            var result = await h.Run("sum", "source=[1, abc, 3]");

            Assert.Equal("4", result);
        }

        [Fact]
        public async Task Sum_EmptySource_ReturnsEmpty()
        {
            var h = BuildHarness();

            var result = await h.Run("sum", "variable=none");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task Min_ReturnsSmallest()
        {
            var h = BuildHarness();
            Assert.Equal("2", await h.Run("min", "source=[8, 2, 5]"));
        }

        [Fact]
        public async Task Max_ReturnsLargest()
        {
            var h = BuildHarness();
            Assert.Equal("8", await h.Run("max", "source=[8, 2, 5]"));
        }

        [Fact]
        public async Task Multiply_ReturnsProduct()
        {
            var h = BuildHarness();
            Assert.Equal("24", await h.Run("multiply", "source=[2, 3, 4]"));
        }

        [Fact]
        public async Task Average_ReturnsDouble()
        {
            var h = BuildHarness();
            Assert.Equal("2.5", await h.Run("average", "source=[1, 2, 3, 4]"));
        }

        [Fact]
        public async Task Average_AvgAlias_Works()
        {
            var h = BuildHarness();
            var result = await h.Processor.ProcessPlaceholderAsync("avg(source=[2, 4, 6])", SessionId, _ct);
            Assert.Equal("4", result);
        }

        // ---------------- binary arithmetic ----------------

        [Fact]
        public async Task Divide_ReturnsDouble()
        {
            var h = BuildHarness();
            Assert.Equal("2.5", await h.Run("divide", "a=10, b=4"));
        }

        [Fact]
        public async Task Divide_ByZero_ReturnsEmpty()
        {
            var h = BuildHarness();
            Assert.Equal(string.Empty, await h.Run("divide", "a=10, b=0"));
        }

        [Fact]
        public async Task Subtract_ReturnsDifference()
        {
            var h = BuildHarness();
            Assert.Equal("3", await h.Run("subtract", "a=5, b=2"));
        }

        [Fact]
        public async Task Mod_ReturnsRemainder()
        {
            var h = BuildHarness();
            Assert.Equal("1", await h.Run("mod", "a=10, b=3"));
        }

        [Fact]
        public async Task Mod_ByZero_ReturnsEmpty()
        {
            var h = BuildHarness();
            Assert.Equal(string.Empty, await h.Run("mod", "a=10, b=0"));
        }

        [Fact]
        public async Task Pow_RaisesToExponent()
        {
            var h = BuildHarness();
            Assert.Equal("1024", await h.Run("pow", "base=2, exponent=10"));
        }

        // ---------------- unary ----------------

        [Fact]
        public async Task Abs_ReturnsAbsoluteValue()
        {
            var h = BuildHarness();
            Assert.Equal("5", await h.Run("abs", "source=-5"));
        }

        [Fact]
        public async Task Abs_Positional_Works()
        {
            var h = BuildHarness();
            Assert.Equal("7", await h.Run("abs", "-7"));
        }

        [Fact]
        public async Task Floor_RoundsDown()
        {
            var h = BuildHarness();
            Assert.Equal("2", await h.Run("floor", "source=2.7"));
        }

        [Fact]
        public async Task Ceil_RoundsUp()
        {
            var h = BuildHarness();
            Assert.Equal("3", await h.Run("ceil", "source=2.1"));
        }

        [Fact]
        public async Task Round_ToDigits()
        {
            var h = BuildHarness();
            Assert.Equal("3.14", await h.Run("round", "source=3.14159, digits=2"));
        }

        [Fact]
        public async Task Round_ZeroDigits_ReturnsInteger()
        {
            var h = BuildHarness();
            Assert.Equal("3", await h.Run("round", "source=2.6"));
        }

        [Fact]
        public async Task Clamp_BoundsValue()
        {
            var h = BuildHarness();
            Assert.Equal("10", await h.Run("clamp", "value=15, min=0, max=10"));
            Assert.Equal("0", await h.Run("clamp", "value=-4, min=0, max=10"));
            Assert.Equal("5", await h.Run("clamp", "value=5, min=0, max=10"));
        }

        // ---------------- comparisons (boolean + numberVariable) ----------------

        [Fact]
        public async Task GreaterThan_TrueAndStoresGreaterNumber()
        {
            var h = BuildHarness();

            var result = await h.Run("greaterthan", "a=8, b=3, variable=isBigger, numberVariable=peak");

            Assert.Equal("true", result);
            Assert.Equal("true", await RawAsync(h, "isBigger"));
            Assert.Equal("8", await RawAsync(h, "peak"));
        }

        [Fact]
        public async Task SmallerThan_TrueAndStoresLesserNumber()
        {
            var h = BuildHarness();

            var result = await h.Run("smallerthan", "a=3, b=8, variable=isSmaller, numberVariable=low");

            Assert.Equal("true", result);
            Assert.Equal("true", await RawAsync(h, "isSmaller"));
            Assert.Equal("3", await RawAsync(h, "low"));
        }

        [Fact]
        public async Task GreaterThanOrEqual_EqualValues_ReturnsTrue()
        {
            var h = BuildHarness();
            Assert.Equal("true", await h.Run("greaterthanorequal", "a=5, b=5"));
        }

        [Fact]
        public async Task SmallerThanOrEqual_LargerValue_ReturnsFalse()
        {
            var h = BuildHarness();
            Assert.Equal("false", await h.Run("smallerthanorequal", "a=9, b=5"));
        }

        [Fact]
        public async Task GreaterThan_MissingOperand_ReturnsFalse()
        {
            var h = BuildHarness();
            Assert.Equal("false", await h.Run("greaterthan", "a=8"));
        }

        // ---------------- greater / less / equal ----------------

        [Fact]
        public async Task Greater_ReturnsMax()
        {
            var h = BuildHarness();
            Assert.Equal("8", await h.Run("greater", "a=8, b=3"));
        }

        [Fact]
        public async Task Less_ReturnsMin()
        {
            var h = BuildHarness();
            Assert.Equal("3", await h.Run("less", "a=8, b=3"));
        }

        [Fact]
        public async Task Equal_ExactMatch_ReturnsTrue()
        {
            var h = BuildHarness();
            Assert.Equal("true", await h.Run("equal", "a=5, b=5"));
            Assert.Equal("false", await h.Run("equal", "a=5, b=6"));
        }

        [Fact]
        public async Task Equal_WithinTolerance_ReturnsTrue()
        {
            var h = BuildHarness();
            Assert.Equal("true", await h.Run("equal", "a=10, b=12, tolerance=3"));
            Assert.Equal("false", await h.Run("equal", "a=10, b=14, tolerance=3"));
        }

        // ---------------- stringequals ----------------

        [Fact]
        public async Task StringEquals_CaseSensitive_ByDefault()
        {
            var h = BuildHarness();
            Assert.Equal("false", await h.Run("stringequals", "a=Hello, b=hello"));
        }

        [Fact]
        public async Task StringEquals_IgnoreCase_ReturnsTrue()
        {
            var h = BuildHarness();
            Assert.Equal("true", await h.Run("stringequals", "a=Hello, b=hello, ignoreCase=true"));
        }

        [Fact]
        public async Task StringEquals_ResolvesPlaceholders()
        {
            var h = BuildHarness();
            await PutGlobalStringAsync(h, "status", "OK");

            var result = await h.Run("stringequals", "a=${status}, b=OK, variable=isOk");

            Assert.Equal("true", result);
            Assert.Equal("true", await RawAsync(h, "isOk"));
        }
    }
}
