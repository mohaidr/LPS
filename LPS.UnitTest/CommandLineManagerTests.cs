using LPS.UI.Core.LPSCommandLine;

namespace LPS.UnitTest
{
    public class CommandLineManagerTests
    {
        [Theory]
        [InlineData("--version")]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("-?")]
        public void IsTestExecutionCommand_InformationalOption_ReturnsFalse(string option)
        {
            Assert.False(CommandLineManager.IsTestExecutionCommand([option]));
        }

        [Fact]
        public void IsTestExecutionCommand_RunCommand_ReturnsTrue()
        {
            Assert.True(CommandLineManager.IsTestExecutionCommand(["run", "plan.yaml"]));
        }
    }
}