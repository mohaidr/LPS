using LPS.Domain;
using LPS.UI.Core.UI.Build.Services;
namespace LPS.UnitTest
{
    public class CommandLineParserTest
    {
        [Fact]
        public void CheckForException()
        {
            LPSTestPlan.SetupCommand lpsTestPlanCommand = new LPSTestPlan.SetupCommand();
           string [] _args = new string[3];
            _args[0] = "run";
            _args[1] = "--testname";
            _args[2] = "testone";
            var commandLineParser = new CommandLineParser(null, lpsTestPlanCommand);
            commandLineParser.CommandLineArgs = _args;
            commandLineParser.Parse();
            Console.WriteLine(lpsTestPlanCommand.Name);

        }
    }
}