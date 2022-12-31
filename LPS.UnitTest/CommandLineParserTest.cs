using LPS.Domain;
using LPS.UI.Core.UI.Build.Services;
namespace LPS.UnitTest
{
    public class CommandLineParserTest
    {
        [Fact]
        public void CheckForException()
        {
            LPSTest.SetupCommand lpsTestCommand = new LPSTest.SetupCommand();
           string [] _args = new string[9];
            _args[0] = "-add";
            _args[1] = "-hm";
            _args[2] = "-get";
            _args[3] = "-url";
            _args[4] = "-https://www.example.com";
            _args[5] = "-r";
            _args[6] = "-5";
            _args[7] = "-tn";
            _args[8] = "-abc";

            var commandLineParser = new CommandLineParser();
            commandLineParser.CommandLineArgs = _args;
            commandLineParser.Parse(lpsTestCommand);

        }
    }
}