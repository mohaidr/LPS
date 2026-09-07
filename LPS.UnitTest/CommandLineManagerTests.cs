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

        [Theory]
        [InlineData("create")]
        [InlineData("cluster")]
        [InlineData("dashboard")]
        [InlineData("unknown")]
        public void IsTestExecutionCommand_LocalCommand_ReturnsFalse(string command)
        {
            Assert.False(CommandLineManager.IsTestExecutionCommand([command]));
        }

        [Fact]
        public void IsTestExecutionCommand_MasterCommand_ReturnsTrue()
        {
            Assert.True(CommandLineManager.IsTestExecutionCommand(["master", "plan.yaml"]));
        }

        [Fact]
        public void IsTestExecutionCommand_InlineRun_ReturnsTrue()
        {
            Assert.True(CommandLineManager.IsTestExecutionCommand(["--url", "https://example.com", "--requestcount", "1"]));
        }

        [Theory]
        [InlineData("run")]
        [InlineData("master")]
        public void IsTestExecutionCommand_ExecutionCommandHelp_ReturnsFalse(string command)
        {
            Assert.False(CommandLineManager.IsTestExecutionCommand([command, "--help"]));
        }

    }
}