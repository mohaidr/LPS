using LPS.UI.Common;
using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace LPS.UI.Core.UI.Build.Services
{
    public class CommandLineParser
    {
        public string[] CommandLineArgs { get; set; }

        public CommandLineParser()
        {

        }

        public void Parse(LPSTest.SetupCommand command)
        {
            int numberofRequests = CommandLineArgs.Count(arg => (arg == "-add" || arg == "-a"));

            if (numberofRequests == 0)
            {
                throw new ArgumentException("-Add property is required");
            }

            if (CommandLineArgs.Count(arg => arg != "-add") % 2 != 0)
                throw new ArgumentException("A valid property should start with - and followed by its value");

            var lpstestCommandLineParser = new LPSTestCommandLineParser();
            lpstestCommandLineParser.CommandLineArgs = CommandLineArgs;
            lpstestCommandLineParser.Parse(command);
        }
    }
}
