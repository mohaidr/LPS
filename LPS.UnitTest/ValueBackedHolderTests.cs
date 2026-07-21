using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.VariableServices.VariableHolders;
using Moq;
using Xunit;

namespace LPS.UnitTest
{
    // Step 2 (value-backed holders): NumberVariableHolder/BooleanVariableHolder now carry the natural CLR
    // value alongside the cached string. These tests lock in BOTH the new typed accessors AND that the
    // string path (GetRawValueAsync, Type tag, invariant/lowercase formatting) is byte-for-byte unchanged.
    public class ValueBackedHolderTests
    {
        private readonly CancellationToken _ct = CancellationToken.None;

        private static (ILogger logger, IRuntimeOperationIdProvider op) Deps()
            => (new Mock<ILogger>().Object, Mock.Of<IRuntimeOperationIdProvider>());

        [Fact]
        public async Task Number_Int_CarriesTypedValue_And_StringUnchanged()
        {
            var (logger, op) = Deps();
            var holder = await new NumberVariableHolder.VMaintainer(logger, op)
                .WithRawValue(42)
                .UpdateAsync(_ct);

            var number = Assert.IsType<NumberVariableHolder>(holder);
            Assert.Equal(VariableType.Int, holder.Type);
            Assert.Equal("42", await holder.GetRawValueAsync(_ct)); // string path unchanged
            Assert.Equal(42L, number.AsInt64());
            Assert.Equal(42d, number.AsDouble());
            Assert.Equal(42m, number.AsDecimal());
        }

        [Fact]
        public async Task Number_Double_CarriesTypedValue_And_StringUnchanged()
        {
            var (logger, op) = Deps();
            var holder = await new NumberVariableHolder.VMaintainer(logger, op)
                .WithRawValue(3.5d)
                .UpdateAsync(_ct);

            var number = Assert.IsType<NumberVariableHolder>(holder);
            Assert.Equal(VariableType.Double, holder.Type);
            Assert.Equal("3.5", await holder.GetRawValueAsync(_ct)); // invariant formatting unchanged
            Assert.Equal(3.5d, number.AsDouble());
            Assert.Equal(3.5m, number.AsDecimal());
        }

        [Fact]
        public async Task Number_Float_CarriesTypedValue_And_StringUnchanged()
        {
            var (logger, op) = Deps();
            var holder = await new NumberVariableHolder.VMaintainer(logger, op)
                .WithRawValue(1.5f)
                .UpdateAsync(_ct);

            var number = Assert.IsType<NumberVariableHolder>(holder);
            Assert.Equal(VariableType.Float, holder.Type);
            Assert.Equal("1.5", await holder.GetRawValueAsync(_ct));
            Assert.Equal(1.5d, number.AsDouble());
        }

        [Fact]
        public async Task Number_Decimal_CarriesTypedValue_And_StringUnchanged()
        {
            var (logger, op) = Deps();
            var holder = await new NumberVariableHolder.VMaintainer(logger, op)
                .WithRawValue(2.5m)
                .UpdateAsync(_ct);

            var number = Assert.IsType<NumberVariableHolder>(holder);
            Assert.Equal(VariableType.Decimal, holder.Type);
            Assert.Equal("2.5", await holder.GetRawValueAsync(_ct));
            Assert.Equal(2.5m, number.AsDecimal());
            Assert.Equal(2.5d, number.AsDouble());
        }

        [Fact]
        public async Task Boolean_True_CarriesTypedValue_And_StringIsLowercase()
        {
            var (logger, op) = Deps();
            var holder = await new BooleanVariableHolder.VMaintainer(logger, op)
                .WithRawValue(true)
                .UpdateAsync(_ct);

            var boolean = Assert.IsType<BooleanVariableHolder>(holder);
            Assert.Equal(VariableType.Boolean, holder.Type);
            Assert.Equal("true", await holder.GetRawValueAsync(_ct)); // lowercase, valid JSON
            Assert.True(boolean.AsBool());
        }

        [Fact]
        public async Task Boolean_False_CarriesTypedValue_And_StringIsLowercase()
        {
            var (logger, op) = Deps();
            var holder = await new BooleanVariableHolder.VMaintainer(logger, op)
                .WithRawValue(false)
                .UpdateAsync(_ct);

            var boolean = Assert.IsType<BooleanVariableHolder>(holder);
            Assert.Equal(VariableType.Boolean, holder.Type);
            Assert.Equal("false", await holder.GetRawValueAsync(_ct));
            Assert.False(boolean.AsBool());
        }
    }
}
